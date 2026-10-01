// Writes test-fixtures/interop.json: tokens signed here (Node) that the C# tests must accept.
// The key pair is made for the occasion and its private half is thrown away: this key protects nothing.
import { mkdirSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { buildPayload, generateKeyPair, signToken } from "../src/license.js";

const { privateKeyPem, publicKeyDerBase64 } = generateKeyPair();
const issuedAt = new Date("2026-10-01T00:00:00Z");
const token = (fields) => signToken(buildPayload({ issuedAt, ...fields }), privateKeyPem);

const fixture = {
  note: "Test-only key pair. Regenerate with `node scripts/make-interop-fixture.js`.",
  publicKeyDerBase64,
  now: "2026-10-15T12:00:00+00:00",
  device: "interop-device",
  tokens: {
    lifetime: token({ licenseId: "LIC-INTEROP-1", plan: "lifetime" }),
    owner: token({ licenseId: "LIC-INTEROP-OWNER", plan: "owner" }),
    boundToDevice: token({ licenseId: "LIC-INTEROP-2", plan: "lifetime", deviceId: "interop-device" }),
    boundToAnotherDevice: token({ licenseId: "LIC-INTEROP-3", plan: "lifetime", deviceId: "someone-else" }),
    expired: token({ licenseId: "LIC-INTEROP-4", plan: "lifetime", expiresAt: new Date("2026-10-02T00:00:00Z") }),
  },
};

const folder = join(dirname(fileURLToPath(import.meta.url)), "..", "test-fixtures");
mkdirSync(folder, { recursive: true });
writeFileSync(join(folder, "interop.json"), JSON.stringify(fixture, null, 2) + "\n");
console.log("written", join(folder, "interop.json"));
