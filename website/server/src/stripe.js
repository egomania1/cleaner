import { createHmac, timingSafeEqual } from "node:crypto";

// Verifies the "Stripe-Signature" header of a webhook (https://docs.stripe.com/webhooks#verify-manually):
// header = "t=<unix seconds>,v1=<hex hmac>[,v1=<hex hmac>...]", hmac = HMAC-SHA256(secret, `${t}.${rawBody}`).
// The raw bytes of the body must be used: parsing and re-serialising it would change the signature.
export function verifyStripeSignature(rawBody, header, secret, { toleranceSeconds = 300, now = Date.now() } = {}) {
  if (!secret || typeof header !== "string" || header.length === 0) return false;

  let timestamp = null;
  const candidates = [];
  for (const part of header.split(",")) {
    const separator = part.indexOf("=");
    if (separator < 0) continue;
    const key = part.slice(0, separator).trim();
    const value = part.slice(separator + 1).trim();
    if (key === "t") timestamp = value;
    if (key === "v1") candidates.push(value);
  }

  if (!/^\d+$/.test(timestamp ?? "") || candidates.length === 0) return false;

  // A captured request cannot be replayed later.
  if (Math.abs(now / 1000 - Number(timestamp)) > toleranceSeconds) return false;

  const expected = createHmac("sha256", secret).update(`${timestamp}.`).update(rawBody).digest();
  return candidates.some((candidate) => {
    if (!/^[0-9a-f]+$/i.test(candidate) || candidate.length !== expected.length * 2) return false;
    return timingSafeEqual(Buffer.from(candidate, "hex"), expected);
  });
}

// Test helper: builds the header Stripe would send.
export function signStripePayload(rawBody, secret, timestampSeconds = Math.floor(Date.now() / 1000)) {
  const hmac = createHmac("sha256", secret).update(`${timestampSeconds}.`).update(rawBody).digest("hex");
  return `t=${timestampSeconds},v1=${hmac}`;
}
