import { createPrivateKey, createPublicKey, generateKeyPairSync, randomBytes, sign, verify } from "node:crypto";

// Token format, shared with the Windows app (docs/LICENSING.md):
//   base64url(JSON payload) + "." + base64url(ECDSA P-256 / SHA-256 signature, fixed-size r||s)
export const TOKEN_VERSION = 1;
export const PLANS = Object.freeze({ lifetime: "lifetime", owner: "owner" });

const SEVENTY_YEARS_MS = 70 * 365.25 * 24 * 3600 * 1000;

const toBase64Url = (buffer) => Buffer.from(buffer).toString("base64url");

export function generateKeyPair() {
  const { privateKey, publicKey } = generateKeyPairSync("ec", { namedCurve: "P-256" });
  return {
    privateKeyPem: privateKey.export({ type: "pkcs8", format: "pem" }),
    // The app embeds this: a SubjectPublicKeyInfo in DER form.
    publicKeyDerBase64: publicKey.export({ type: "spki", format: "der" }).toString("base64"),
  };
}

export function newLicenseId(now = new Date()) {
  return `LIC-${now.getUTCFullYear()}-${randomBytes(6).toString("hex").toUpperCase()}`;
}

export function buildPayload({ licenseId, plan = PLANS.lifetime, issuedAt = new Date(), expiresAt, deviceId = null }) {
  if (!licenseId) throw new Error("licenseId is required");
  if (!Object.values(PLANS).includes(plan)) throw new Error(`unknown plan: ${plan}`);
  const end = expiresAt ?? new Date(issuedAt.getTime() + SEVENTY_YEARS_MS);
  if (end <= issuedAt) throw new Error("expiresAt must be after issuedAt");
  return {
    version: TOKEN_VERSION,
    licenseId,
    plan,
    issuedAt: issuedAt.toISOString(),
    expiresAt: end.toISOString(),
    deviceId,
  };
}

export function signToken(payload, privateKeyPem) {
  const bytes = Buffer.from(JSON.stringify(payload), "utf8");
  const signature = sign("sha256", bytes, { key: createPrivateKey(privateKeyPem), dsaEncoding: "ieee-p1363" });
  return `${toBase64Url(bytes)}.${toBase64Url(signature)}`;
}

// Used by the tests and by the owner's tools; the app does the same check in C#.
export function verifyToken(token, publicKeyDerBase64) {
  const parts = String(token).split(".");
  if (parts.length !== 2) return null;
  const payload = Buffer.from(parts[0], "base64url");
  const signature = Buffer.from(parts[1], "base64url");
  const key = createPublicKey({ key: Buffer.from(publicKeyDerBase64, "base64"), format: "der", type: "spki" });
  return verify("sha256", payload, { key, dsaEncoding: "ieee-p1363" }, signature)
    ? JSON.parse(payload.toString("utf8"))
    : null;
}
