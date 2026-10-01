import { after, before, describe, it } from "node:test";
import assert from "node:assert/strict";
import { createServer } from "node:http";
import { createApp } from "../src/app.js";
import { buildPayload, generateKeyPair, signToken, verifyToken } from "../src/license.js";
import { signStripePayload, verifyStripeSignature } from "../src/stripe.js";
import { Store } from "../src/store.js";

const SECRET = "whsec_test_secret";
const SESSION = "cs_test_a1B2c3D4e5F6g7H8i9J0";
const keys = generateKeyPair();

function paidEvent(overrides = {}) {
  return {
    id: "evt_1",
    type: "checkout.session.completed",
    data: {
      object: {
        id: SESSION,
        payment_status: "paid",
        payment_intent: "pi_123",
        amount_total: 390,
        currency: "eur",
        customer_details: { email: "buyer@example.test" },
        ...overrides,
      },
    },
  };
}

async function start({ store = new Store(), config = {}, clock = () => new Date("2026-10-01T10:00:00Z") } = {}) {
  const sent = [];
  const logs = [];
  const app = createApp({
    store,
    config: { stripeWebhookSecret: SECRET, expectedAmount: 390, expectedCurrency: "eur", rateLimitPerMinute: 20, ...config },
    privateKeyPem: keys.privateKeyPem,
    mailer: { sendLicense: async (mail) => sent.push(mail) },
    now: clock,
    log: (level, message, fields) => logs.push({ level, message, ...fields }),
  });
  const server = createServer((request, response) => app.handle(request, response));
  await new Promise((resolve) => server.listen(0, "127.0.0.1", resolve));
  const base = `http://127.0.0.1:${server.address().port}`;
  const post = (event, { secret = SECRET, header, raw } = {}) => {
    const body = raw ?? JSON.stringify(event);
    return fetch(`${base}/stripe/webhook`, {
      method: "POST",
      body,
      headers: { "Stripe-Signature": header ?? signStripePayload(body, secret, Math.floor(clock().getTime() / 1000)) },
    });
  };
  return { base, post, sent, logs, store, stop: () => new Promise((resolve) => server.close(resolve)) };
}

describe("Stripe signature", () => {
  const body = '{"id":"evt_1"}';
  const now = Date.now();

  it("accepts a valid signature", () => {
    assert.equal(verifyStripeSignature(body, signStripePayload(body, SECRET, Math.floor(now / 1000)), SECRET, { now }), true);
  });

  it("rejects another secret, an edited body and a missing header", () => {
    const header = signStripePayload(body, SECRET, Math.floor(now / 1000));
    assert.equal(verifyStripeSignature(body, header, "whsec_other", { now }), false);
    assert.equal(verifyStripeSignature(body + " ", header, SECRET, { now }), false);
    assert.equal(verifyStripeSignature(body, undefined, SECRET, { now }), false);
    assert.equal(verifyStripeSignature(body, header, "", { now }), false);
  });

  it("rejects a request that is too old or from the future", () => {
    const old = signStripePayload(body, SECRET, Math.floor(now / 1000) - 3600);
    const future = signStripePayload(body, SECRET, Math.floor(now / 1000) + 3600);
    assert.equal(verifyStripeSignature(body, old, SECRET, { now }), false);
    assert.equal(verifyStripeSignature(body, future, SECRET, { now }), false);
  });

  it("rejects malformed headers and accepts any matching v1 among several", () => {
    const stamp = Math.floor(now / 1000);
    const good = signStripePayload(body, SECRET, stamp).split("v1=")[1];
    assert.equal(verifyStripeSignature(body, "garbage", SECRET, { now }), false);
    assert.equal(verifyStripeSignature(body, `t=abc,v1=${good}`, SECRET, { now }), false);
    assert.equal(verifyStripeSignature(body, `t=${stamp},v1=zz`, SECRET, { now }), false);
    assert.equal(verifyStripeSignature(body, `t=${stamp},v1=${"0".repeat(64)},v1=${good}`, SECRET, { now }), true);
  });
});

describe("licence tokens", () => {
  it("are signed with the private key and verified with the public key only", () => {
    const token = signToken(buildPayload({ licenseId: "LIC-1" }), keys.privateKeyPem);
    const payload = verifyToken(token, keys.publicKeyDerBase64);
    assert.equal(payload.licenseId, "LIC-1");
    assert.equal(payload.plan, "lifetime");
    assert.equal(payload.version, 1);
    assert.equal(payload.deviceId, null);
  });

  it("are rejected when edited or signed by another key", () => {
    const token = signToken(buildPayload({ licenseId: "LIC-1" }), keys.privateKeyPem);
    const [payload, signature] = token.split(".");
    const edited = Buffer.from(JSON.stringify({ ...JSON.parse(Buffer.from(payload, "base64url")), plan: "owner" })).toString("base64url");
    assert.equal(verifyToken(`${edited}.${signature}`, keys.publicKeyDerBase64), null);

    const other = generateKeyPair();
    assert.equal(verifyToken(signToken(buildPayload({ licenseId: "LIC-1" }), other.privateKeyPem), keys.publicKeyDerBase64), null);
  });

  it("refuse an unknown plan and an end date before the start", () => {
    assert.throws(() => buildPayload({ licenseId: "X", plan: "free-forever" }));
    assert.throws(() => buildPayload({ licenseId: "X", issuedAt: new Date("2026-01-02"), expiresAt: new Date("2026-01-01") }));
    assert.throws(() => buildPayload({}));
  });

  it("last about seventy years by default", () => {
    const payload = buildPayload({ licenseId: "X", issuedAt: new Date("2026-10-01T00:00:00Z") });
    const years = (new Date(payload.expiresAt) - new Date(payload.issuedAt)) / (365.25 * 24 * 3600 * 1000);
    assert.ok(years > 69.9 && years < 70.1);
  });
});

describe("webhook", () => {
  let ctx;
  before(async () => {
    ctx = await start();
  });
  after(() => ctx.stop());

  it("rejects a request with a bad signature and creates nothing", async () => {
    const response = await ctx.post(paidEvent({ id: SESSION }), { secret: "whsec_wrong" });
    assert.equal(response.status, 400);
    assert.equal(ctx.store.findBySession(SESSION), null);
  });

  it("rejects an invalid json body even when signed", async () => {
    assert.equal((await ctx.post(null, { raw: "{not json" })).status, 400);
    assert.equal((await ctx.post(null, { raw: '{"nothing":true}' })).status, 400);
  });

  it("creates one licence and e-mails a token that the app can verify", async () => {
    const response = await ctx.post(paidEvent());
    assert.equal(response.status, 200);

    const license = ctx.store.findBySession(SESSION);
    assert.equal(license.status, "active");
    assert.equal(license.plan, "lifetime");
    assert.equal(ctx.sent.length, 1);
    assert.equal(ctx.sent[0].to, "buyer@example.test");
    assert.equal(verifyToken(ctx.sent[0].token, keys.publicKeyDerBase64).licenseId, license.id);
  });

  it("ignores a replay of the same event", async () => {
    const response = await ctx.post(paidEvent());
    assert.equal((await response.json()).duplicate, true);
    assert.equal(ctx.sent.length, 1);
  });

  it("creates no second licence for the same session reported by another event", async () => {
    const response = await ctx.post({ ...paidEvent(), id: "evt_other", type: "checkout.session.async_payment_succeeded" });
    assert.equal(response.status, 200);
    assert.equal(ctx.sent.length, 1);
  });

  it("ignores unknown event types", async () => {
    assert.equal((await ctx.post({ id: "evt_x", type: "customer.created", data: { object: {} } })).status, 200);
  });

  it("does not log the e-mail address or the token", () => {
    const text = JSON.stringify(ctx.logs);
    assert.ok(!text.includes("buyer@example.test"));
    assert.ok(!text.includes(ctx.sent[0].token));
  });
});

describe("payments that must not give a licence", () => {
  it("unpaid sessions and unexpected amounts", async () => {
    const ctx = await start();
    await ctx.post({ ...paidEvent({ payment_status: "unpaid" }), id: "evt_unpaid" });
    await ctx.post({ ...paidEvent({ id: "cs_test_zzzzzzzzzzzzzzzz1", amount_total: 1 }), id: "evt_cheap" });
    await ctx.post({ ...paidEvent({ id: "cs_test_zzzzzzzzzzzzzzzz2", currency: "usd" }), id: "evt_usd" });
    await ctx.post({ ...paidEvent({ id: "not-a-session-id" }), id: "evt_badid" });
    assert.equal(ctx.sent.length, 0);
    assert.equal(ctx.store.findBySession(SESSION), null);
    await ctx.stop();
  });
});

describe("refunds", () => {
  it("revoke the licence, which the lookup then reports as gone", async () => {
    const ctx = await start();
    await ctx.post(paidEvent());
    const refund = await ctx.post({ id: "evt_refund", type: "charge.refunded", data: { object: { payment_intent: "pi_123" } } });
    assert.equal(refund.status, 200);
    assert.equal(ctx.store.findBySession(SESSION).status, "revoked");
    assert.equal((await fetch(`${ctx.base}/api/license?session_id=${SESSION}`)).status, 410);

    await ctx.post({ id: "evt_dispute", type: "charge.dispute.created", data: { object: { payment_intent: "pi_unknown" } } });
    await ctx.stop();
  });
});

describe("failures", () => {
  it("answer 500 and leave no trace when the database fails, so Stripe's retry works", async () => {
    const store = new Store();
    const original = store.createLicense.bind(store);
    let failures = 1;
    store.createLicense = (license) => {
      if (failures-- > 0) throw new Error("disk full");
      return original(license);
    };
    const ctx = await start({ store });

    assert.equal((await ctx.post(paidEvent())).status, 500);
    assert.equal(store.findBySession(SESSION), null);

    assert.equal((await ctx.post(paidEvent())).status, 200);
    assert.equal(store.findBySession(SESSION).status, "active");
    assert.equal(ctx.sent.length, 1);
    await ctx.stop();
  });

  it("keep the licence when only the e-mail fails", async () => {
    const store = new Store();
    const app = createApp({
      store,
      config: { stripeWebhookSecret: SECRET, expectedAmount: 390, expectedCurrency: "eur" },
      privateKeyPem: keys.privateKeyPem,
      mailer: { sendLicense: async () => { throw new Error("smtp down"); } },
      now: () => new Date("2026-10-01T10:00:00Z"),
    });
    const server = createServer((request, response) => app.handle(request, response));
    await new Promise((resolve) => server.listen(0, "127.0.0.1", resolve));
    const body = JSON.stringify(paidEvent());
    const response = await fetch(`http://127.0.0.1:${server.address().port}/stripe/webhook`, {
      method: "POST",
      body,
      headers: { "Stripe-Signature": signStripePayload(body, SECRET, Math.floor(new Date("2026-10-01T10:00:00Z").getTime() / 1000)) },
    });
    assert.equal(response.status, 200);
    assert.equal(store.findBySession(SESSION).status, "active");
    await new Promise((resolve) => server.close(resolve));
  });

  it("refuse a body above one megabyte", async () => {
    const ctx = await start();
    const response = await fetch(`${ctx.base}/stripe/webhook`, { method: "POST", body: "x".repeat(1024 * 1024 + 10) });
    assert.equal(response.status, 413);
    await ctx.stop();
  });
});

describe("licence lookup", () => {
  let ctx;
  before(async () => {
    ctx = await start();
    await ctx.post(paidEvent());
  });
  after(() => ctx.stop());

  it("returns a verifiable token for a paid session", async () => {
    const response = await fetch(`${ctx.base}/api/license?session_id=${SESSION}`);
    assert.equal(response.status, 200);
    const body = await response.json();
    assert.equal(body.status, "active");
    assert.equal(verifyToken(body.token, keys.publicKeyDerBase64).licenseId, body.licenseId);
  });

  it("says pending for an unknown session and 400 for a malformed one", async () => {
    assert.equal((await fetch(`${ctx.base}/api/license?session_id=cs_test_unknownunknown12`)).status, 404);
    assert.equal((await fetch(`${ctx.base}/api/license?session_id=../etc/passwd`)).status, 400);
    assert.equal((await fetch(`${ctx.base}/api/license`)).status, 400);
  });

  it("answers with security headers and no cache", async () => {
    const response = await fetch(`${ctx.base}/api/health`);
    assert.equal(response.headers.get("cache-control"), "no-store");
    assert.equal(response.headers.get("x-content-type-options"), "nosniff");
    assert.equal(response.headers.get("x-frame-options"), "DENY");
    assert.match(response.headers.get("strict-transport-security"), /max-age=/);
    assert.match(response.headers.get("content-security-policy"), /default-src 'none'/);
  });

  it("knows its routes", async () => {
    assert.equal((await fetch(`${ctx.base}/nothing`)).status, 404);
    assert.equal((await fetch(`${ctx.base}/stripe/webhook`)).status, 405);
  });
});

describe("rate limit", () => {
  it("stops a client that asks for licences too often, then lets it back in a minute later", async () => {
    let current = new Date("2026-10-01T10:00:00Z");
    const ctx = await start({ clock: () => current, config: { rateLimitPerMinute: 3 } });
    const call = () => fetch(`${ctx.base}/api/license?session_id=cs_test_unknownunknown12`);

    assert.deepEqual([await call(), await call(), await call()].map((r) => r.status), [404, 404, 404]);
    const blocked = await call();
    assert.equal(blocked.status, 429);
    assert.equal(blocked.headers.get("retry-after"), "60");

    current = new Date("2026-10-01T10:01:30Z");
    assert.equal((await call()).status, 404);
    await ctx.stop();
  });
});

describe("store", () => {
  it("erases an e-mail address on request and keeps the licence", () => {
    const store = new Store();
    store.createLicense({ id: "LIC-1", sessionId: SESSION, paymentIntent: "pi_1", email: "a@example.test", plan: "lifetime", createdAt: "2026-10-01T00:00:00Z" });

    assert.equal(store.eraseEmail("a@example.test"), 1);
    const license = store.findBySession(SESSION);
    assert.equal(license.email, null);
    assert.equal(license.status, "active");
  });

  it("purges old events", () => {
    const store = new Store();
    store.recordEvent("evt_old", "x", "2020-01-01T00:00:00Z");
    store.recordEvent("evt_new", "x", "2026-10-01T00:00:00Z");
    assert.equal(store.purgeEventsBefore("2026-01-01T00:00:00Z"), 1);
    assert.equal(store.recordEvent("evt_new", "x", "2026-10-02T00:00:00Z"), false);
  });
});
