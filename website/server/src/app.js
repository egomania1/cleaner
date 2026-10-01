import { buildPayload, newLicenseId, PLANS, signToken } from "./license.js";
import { verifyStripeSignature } from "./stripe.js";

const MAX_BODY_BYTES = 1024 * 1024;
const SESSION_ID = /^cs_(test|live)_[A-Za-z0-9]{10,200}$/;

const SECURITY_HEADERS = {
  "Content-Type": "application/json; charset=utf-8",
  "Cache-Control": "no-store",
  "X-Content-Type-Options": "nosniff",
  "X-Frame-Options": "DENY",
  "Referrer-Policy": "no-referrer",
  "Content-Security-Policy": "default-src 'none'; frame-ancestors 'none'",
  "Strict-Transport-Security": "max-age=63072000; includeSubDomains",
};

// A mailer must never log the key or the address: `sendLicense` is the only place that sees them.
export const silentMailer = { async sendLicense() {} };

export function createApp({ store, config, privateKeyPem, mailer = silentMailer, now = () => new Date(), log = () => {} }) {
  const rateLimiter = createRateLimiter({ limit: config.rateLimitPerMinute ?? 20, windowMs: 60_000, now });

  async function handle(request, response) {
    try {
      const url = new URL(request.url ?? "/", "http://localhost");
      const route = `${request.method} ${url.pathname}`;

      if (route === "GET /api/health") return send(response, 200, { ok: true });
      if (route === "POST /stripe/webhook") return await handleWebhook(request, response);
      if (route === "GET /api/license") return handleLicenseLookup(request, response, url);

      const known = ["/api/health", "/stripe/webhook", "/api/license"];
      return send(response, known.includes(url.pathname) ? 405 : 404, { error: "not found" });
    } catch (error) {
      if (error instanceof HttpError) return send(response, error.status, { error: error.message });
      log("error", "unhandled error", { message: error.message });
      return send(response, 500, { error: "internal error" });
    }
  }

  async function handleWebhook(request, response) {
    const raw = await readBody(request, MAX_BODY_BYTES);
    if (!verifyStripeSignature(raw, request.headers["stripe-signature"], config.stripeWebhookSecret, { now: now().getTime() })) {
      return send(response, 400, { error: "invalid signature" });
    }

    let event;
    try {
      event = JSON.parse(raw.toString("utf8"));
    } catch {
      return send(response, 400, { error: "invalid json" });
    }
    if (typeof event?.id !== "string" || typeof event?.type !== "string") {
      return send(response, 400, { error: "invalid event" });
    }

    // Everything that changes the database happens in one transaction: a failure answers 500 and Stripe retries.
    let delivery = null;
    const isNew = store.transaction(() => {
      if (!store.recordEvent(event.id, event.type, now().toISOString())) return false;
      delivery = applyEvent(event);
      return true;
    });

    if (!isNew) return send(response, 200, { received: true, duplicate: true });

    if (delivery) {
      try {
        await mailer.sendLicense({ to: delivery.email, licenseId: delivery.licenseId, token: delivery.token });
      } catch (error) {
        // The licence exists and can still be fetched from the success page; the e-mail can be re-sent.
        log("error", "could not send the licence e-mail", { licenseId: delivery.licenseId, message: error.message });
      }
    }
    return send(response, 200, { received: true });
  }

  function applyEvent(event) {
    const object = event.data?.object ?? {};

    if (event.type === "checkout.session.completed" || event.type === "checkout.session.async_payment_succeeded") {
      if (object.payment_status !== "paid") return null;
      if (config.expectedAmount && (object.amount_total !== config.expectedAmount || object.currency !== config.expectedCurrency)) {
        log("warn", "payment ignored: unexpected amount", { event: event.id });
        return null;
      }
      if (typeof object.id !== "string" || !SESSION_ID.test(object.id)) return null;

      const licenseId = newLicenseId(now());
      const license = store.createLicense({
        id: licenseId,
        sessionId: object.id,
        paymentIntent: typeof object.payment_intent === "string" ? object.payment_intent : null,
        email: object.customer_details?.email ?? object.customer_email ?? null,
        plan: PLANS.lifetime,
        createdAt: now().toISOString(),
      });
      // The session may already have a licence from an earlier event: nothing new to deliver then.
      if (license.id !== licenseId) return null;
      return { email: license.email, licenseId: license.id, token: tokenFor(license) };
    }

    if (event.type === "charge.refunded" || event.type === "charge.dispute.created") {
      const reason = event.type === "charge.refunded" ? "refunded" : "dispute";
      store.revokeByPaymentIntent(object.payment_intent, reason, now().toISOString());
    }
    return null;
  }

  function handleLicenseLookup(request, response, url) {
    if (!rateLimiter.allow(clientAddress(request, config))) {
      response.setHeader("Retry-After", "60");
      return send(response, 429, { error: "too many requests" });
    }

    const sessionId = url.searchParams.get("session_id") ?? "";
    if (!SESSION_ID.test(sessionId)) return send(response, 400, { error: "invalid session" });

    const license = store.findBySession(sessionId);
    // Not there yet: the webhook may arrive a few seconds after the customer is sent back to the site.
    if (!license) return send(response, 404, { status: "pending" });
    if (license.status === "revoked") return send(response, 410, { status: "revoked" });
    return send(response, 200, { status: "active", licenseId: license.id, plan: license.plan, token: tokenFor(license) });
  }

  function tokenFor(license) {
    const payload = buildPayload({ licenseId: license.id, plan: license.plan, issuedAt: new Date(license.created_at) });
    return signToken(payload, privateKeyPem);
  }

  return { handle };
}

class HttpError extends Error {
  constructor(status, message) {
    super(message);
    this.status = status;
  }
}

function send(response, status, body) {
  response.writeHead(status, SECURITY_HEADERS);
  response.end(JSON.stringify(body));
}

async function readBody(request, limit) {
  const chunks = [];
  let size = 0;
  for await (const chunk of request) {
    size += chunk.length;
    if (size > limit) throw new HttpError(413, "body too large");
    chunks.push(chunk);
  }
  return Buffer.concat(chunks);
}

function clientAddress(request, config) {
  if (config.trustProxy) {
    const forwarded = request.headers["x-forwarded-for"];
    if (typeof forwarded === "string" && forwarded.length > 0) return forwarded.split(",")[0].trim();
  }
  return request.socket.remoteAddress ?? "unknown";
}

function createRateLimiter({ limit, windowMs, now }) {
  const hits = new Map();
  return {
    allow(key) {
      const current = now().getTime();
      const recent = (hits.get(key) ?? []).filter((time) => current - time < windowMs);
      if (recent.length >= limit) {
        hits.set(key, recent);
        return false;
      }
      recent.push(current);
      hits.set(key, recent);
      // Keeps the table small when many different addresses come by.
      if (hits.size > 10_000) for (const [address, times] of hits) if (times.every((time) => current - time >= windowMs)) hits.delete(address);
      return true;
    },
  };
}
