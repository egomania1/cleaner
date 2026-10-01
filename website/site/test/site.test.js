import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { readdirSync, readFileSync, statSync, existsSync } from "node:fs";
import { dirname, join, relative, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { isValidSessionId, lookupLicense } from "../js/licence-lookup.js";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");

function htmlFiles(folder) {
  return readdirSync(folder).flatMap((name) => {
    const path = join(folder, name);
    if (statSync(path).isDirectory()) return name === "test" ? [] : htmlFiles(path);
    return name.endsWith(".html") ? [path] : [];
  });
}

const pages = htmlFiles(root);
const read = (path) => readFileSync(path, "utf8");
const name = (path) => relative(root, path).replaceAll("\\", "/");

describe("every page", () => {
  for (const page of pages) {
    const html = read(page);

    it(`${name(page)}: has the basics of a responsive, accessible page`, () => {
      assert.match(html, /^<!doctype html>/i);
      assert.match(html, /<html lang="fr">/);
      assert.match(html, /<meta name="viewport" content="width=device-width, initial-scale=1">/);
      assert.match(html, /<title>[^<]{5,}<\/title>/);
      assert.match(html, /<a class="skip-link" href="#contenu">/);
      assert.equal((html.match(/<main\b/g) ?? []).length, 1);
      assert.equal((html.match(/<h1\b/g) ?? []).length, 1, "exactly one h1");
      assert.match(html, /<header\b/);
      assert.match(html, /<footer\b/);
    });

    it(`${name(page)}: respects the strict content security policy`, () => {
      assert.ok(!/\sstyle="/.test(html), "no inline style attribute");
      assert.ok(!/<style\b/.test(html), "no style element");
      assert.ok(!/\son[a-z]+="/.test(html), "no inline event handler");
      assert.ok(!/<script(?![^>]*\bsrc=)[^>]*>/.test(html), "no inline script");
      assert.ok(!/(src|href)="https?:\/\//.test(html.replace(/<a [^>]*>/g, "")), "no external resource");
    });

    it(`${name(page)}: every image has an alternative text`, () => {
      for (const image of html.match(/<img\b[^>]*>/g) ?? []) assert.match(image, /\balt="/);
    });

    it(`${name(page)}: every local link and file points to something that exists`, () => {
      for (const [, target] of html.matchAll(/(?:href|src)="([^"#?]+)(?:[#?][^"]*)?"/g)) {
        if (/^(https?:|mailto:|ms-windows-store:)/.test(target)) continue;
        const file = target.startsWith("/") ? join(root, target) : resolve(dirname(page), target);
        assert.ok(existsSync(file) || existsSync(join(file, "index.html")), `${target} is missing`);
      }
    });
  }
});

describe("page content", () => {
  it("the legal templates warn loudly and stay out of search engines until completed", () => {
    for (const page of pages.filter((p) => name(p).startsWith("legal/"))) {
      const html = read(page);
      assert.match(html, /class="notice"/);
      assert.match(html, /\[À COMPLÉTER/);
      assert.match(html, /<meta name="robots" content="noindex">/);
    }
  });

  it("the thank-you page is not indexed and sends no referrer", () => {
    const html = read(join(root, "merci.html"));
    assert.match(html, /<meta name="robots" content="noindex">/);
    assert.match(html, /<meta name="referrer" content="no-referrer">/);
  });

  it("the home page shows the price, the trial and the withdrawal right, and never invents numbers", () => {
    const html = read(join(root, "index.html"));
    assert.match(html, /3,90&nbsp;€/);
    assert.match(html, /5 jours/);
    assert.match(html, /rétractation de 14 jours/);
    assert.ok(!/\d+\s?(millions?|%)\s+(d['e ]|de )?(utilisateurs|clients|PC)/i.test(html), "no invented statistic");
  });

  it("the site declares no cookie and loads nothing from another domain", () => {
    for (const file of ["js/site.js", "js/merci.js", "js/licence-lookup.js"]) {
      const code = read(join(root, file));
      assert.ok(!/document\.cookie|localStorage|sessionStorage/.test(code), `${file} stores nothing`);
    }
  });

  it("ships security headers with a strict policy", () => {
    const headers = read(join(root, "_headers"));
    assert.match(headers, /Content-Security-Policy: default-src 'none'/);
    assert.match(headers, /frame-ancestors 'none'/);
    assert.ok(!/unsafe-inline|unsafe-eval/.test(headers));
    assert.match(headers, /Strict-Transport-Security/);
  });
});

describe("licence lookup on the thank-you page", () => {
  const session = "cs_test_a1B2c3D4e5F6g7H8i9J0";
  const noSleep = async () => {};
  const reply = (status, body = {}, headers = {}) => ({
    status,
    json: async () => body,
    headers: { get: (key) => headers[key.toLowerCase()] ?? null },
  });
  const sequence = (...replies) => {
    const calls = [];
    return { calls, fetchFn: async (url) => { calls.push(url); const next = replies.shift(); if (next instanceof Error) throw next; return next; } };
  };

  it("validates the session id before calling anything", async () => {
    assert.equal(isValidSessionId(session), true);
    assert.equal(isValidSessionId("../x"), false);
    assert.equal(isValidSessionId(null), false);
    const { calls, fetchFn } = sequence();
    assert.deepEqual(await lookupLicense({ sessionId: "nope", fetchFn, sleep: noSleep }), { state: "invalid" });
    assert.equal(calls.length, 0);
  });

  it("returns the key as soon as the licence exists, waiting while the payment is not confirmed yet", async () => {
    const { calls, fetchFn } = sequence(reply(404), reply(404), reply(200, { token: "payload.signature", licenseId: "LIC-1" }));
    const result = await lookupLicense({ sessionId: session, fetchFn, sleep: noSleep });
    assert.deepEqual(result, { state: "active", token: "payload.signature", licenseId: "LIC-1" });
    assert.equal(calls.length, 3);
    assert.ok(calls[0].startsWith("/api/license?session_id="));
  });

  it("uses the configured API base", async () => {
    const { calls, fetchFn } = sequence(reply(200, { token: "a.b", licenseId: "L" }));
    await lookupLicense({ sessionId: session, fetchFn, sleep: noSleep, apiBase: "https://api.example.test" });
    assert.ok(calls[0].startsWith("https://api.example.test/api/license"));
  });

  it("reports a revoked licence and an invalid session at once", async () => {
    assert.deepEqual(await lookupLicense({ sessionId: session, fetchFn: sequence(reply(410)).fetchFn, sleep: noSleep }), { state: "revoked" });
    assert.deepEqual(await lookupLicense({ sessionId: session, fetchFn: sequence(reply(400)).fetchFn, sleep: noSleep }), { state: "invalid" });
  });

  it("gives up with a timeout when the payment never shows up, and with an error when the server is broken", async () => {
    const pending = Array.from({ length: 3 }, () => reply(404));
    assert.deepEqual(await lookupLicense({ sessionId: session, fetchFn: sequence(...pending).fetchFn, sleep: noSleep, attempts: 3 }), { state: "timeout" });
    const broken = Array.from({ length: 3 }, () => reply(500));
    assert.deepEqual(await lookupLicense({ sessionId: session, fetchFn: sequence(...broken).fetchFn, sleep: noSleep, attempts: 3 }), { state: "error" });
  });

  it("survives a network failure and tries again", async () => {
    const { fetchFn } = sequence(new Error("offline"), reply(200, { token: "a.b", licenseId: "L" }));
    assert.equal((await lookupLicense({ sessionId: session, fetchFn, sleep: noSleep })).state, "active");
  });

  it("waits as long as the server asks when rate limited", async () => {
    const waits = [];
    const { fetchFn } = sequence(reply(429, {}, { "retry-after": "60" }), reply(200, { token: "a.b", licenseId: "L" }));
    await lookupLicense({ sessionId: session, fetchFn, sleep: async (ms) => waits.push(ms) });
    assert.deepEqual(waits, [60_000]);
  });

  it("refuses a 200 answer that holds no usable key", async () => {
    assert.equal((await lookupLicense({ sessionId: session, fetchFn: sequence(reply(200, { token: 5 })).fetchFn, sleep: noSleep })).state, "error");
  });
});
