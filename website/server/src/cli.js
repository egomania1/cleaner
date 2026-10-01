import { existsSync, readFileSync, writeFileSync } from "node:fs";
import { createPublicKey, createPrivateKey } from "node:crypto";
import { buildPayload, generateKeyPair, newLicenseId, PLANS, signToken } from "./license.js";

const [, , command, ...rest] = process.argv;
const options = Object.fromEntries(
  rest.reduce((pairs, argument, index, all) => (argument.startsWith("--") ? [...pairs, [argument.slice(2), all[index + 1]]] : pairs), []),
);

function fail(message) {
  console.error(message);
  process.exit(1);
}

if (command === "keygen") {
  const out = options.out ?? "license-private.pem";
  if (existsSync(out)) fail(`${out} already exists: refusing to overwrite a private key.`);
  const { privateKeyPem, publicKeyDerBase64 } = generateKeyPair();
  // 0o600: only the owner of the file can read it (effective on Linux and macOS servers).
  writeFileSync(out, privateKeyPem, { mode: 0o600 });
  const bytes = Buffer.from(publicKeyDerBase64, "base64");
  console.log(`Private key written to ${out}. Keep it on the server only, never in Git.`);
  console.log("\nPublic key for the app (src/Clean.Infrastructure/Licensing/LicenseKeys.cs):\n");
  console.log(`public static readonly byte[]? PublicKey = Convert.FromBase64String("${publicKeyDerBase64}");`);
  console.log(`\n(${bytes.length} bytes)`);
} else if (command === "issue") {
  const keyPath = options.key ?? "license-private.pem";
  if (!existsSync(keyPath)) fail(`Private key not found: ${keyPath}`);
  const plan = options.plan ?? PLANS.owner;
  const payload = buildPayload({
    licenseId: options.id ?? newLicenseId(),
    plan,
    deviceId: options.device ?? null,
  });
  // Checks that the key file is a usable private key before printing anything.
  createPublicKey(createPrivateKey(readFileSync(keyPath, "utf8")));
  console.log(signToken(payload, readFileSync(keyPath, "utf8")));
} else {
  fail("Usage:\n  node src/cli.js keygen [--out license-private.pem]\n  node src/cli.js issue [--plan owner|lifetime] [--key license-private.pem] [--device <id>] [--id <licenseId>]");
}
