import { createServer } from "node:http";
import { readFileSync } from "node:fs";
import { createApp, silentMailer } from "./app.js";
import { Store } from "./store.js";

function required(name) {
  const value = process.env[name];
  if (!value) {
    console.error(`Missing environment variable ${name}. See website/server/README.md.`);
    process.exit(1);
  }
  return value;
}

const config = {
  stripeWebhookSecret: required("STRIPE_WEBHOOK_SECRET"),
  // 390 = 3,90 EUR in cents. Leave both unset to accept any amount.
  expectedAmount: process.env.EXPECTED_AMOUNT ? Number(process.env.EXPECTED_AMOUNT) : 390,
  expectedCurrency: process.env.EXPECTED_CURRENCY ?? "eur",
  trustProxy: process.env.TRUST_PROXY === "1",
  rateLimitPerMinute: 20,
};

const privateKeyPem = readFileSync(required("LICENSE_PRIVATE_KEY_PATH"), "utf8");
const store = new Store(process.env.DATABASE_PATH ?? "./licenses.db");

const log = (level, message, fields = {}) => console.log(JSON.stringify({ time: new Date().toISOString(), level, message, ...fields }));

// To do before selling: plug a real mailer here (see A-FAIRE-PAR-TOI.md). Until then the key is only
// available from the success page of the site.
const app = createApp({ store, config, privateKeyPem, mailer: silentMailer, log });

const port = Number(process.env.PORT ?? 8787);
createServer((request, response) => app.handle(request, response)).listen(port, () => log("info", "licence server started", { port }));
