# Licence tokens: contract between the website and the app

The website sells and signs. The app only verifies, offline, with a public key. Nothing in the app can create a valid token.

## Token

```
base64url(payload) + "." + base64url(signature)
```

- `payload`: UTF-8 JSON, camelCase, exactly these fields:

```json
{
  "version": 1,
  "licenseId": "LIC-2026-000123",
  "plan": "annual",
  "issuedAt": "2026-10-01T12:00:00+00:00",
  "expiresAt": "2027-10-01T12:00:00+00:00",
  "deviceId": null
}
```

- `licenseId` is an opaque id. Put no name, e-mail or address in the token.
- `deviceId` is `null` for a licence usable on any PC, or the 32-character id shown by the app (`DeviceIdentity.Current()`) to bind it to one PC.
- `signature`: ECDSA on curve P-256, SHA-256, over the raw payload bytes, in the fixed-size `r || s` form (64 bytes, "IEEE P1363"). In Node: `crypto.sign("sha256", payload, { key, dsaEncoding: "ieee-p1363" })`.
- base64url means base64 with `-` and `_`, without `=` padding.

## Keys

- Generate the P-256 key pair once, on the server side. The private key stays on the server (secret manager), never in Git and never in the app.
- The app embeds the **public** key as a SubjectPublicKeyInfo (DER) byte array. Rotating the key means shipping an app update, so plan a second public key before the first rotation.

## States the app understands (`LicenseVerifier`)

| State | When | Paid features |
|---|---|---|
| Valid | before `expiresAt` | yes |
| Grace | up to 3 days after `expiresAt` | yes |
| Expired | later | no |
| Invalid | bad signature, other PC, unknown version, unreadable | no |

## What the website must do

1. After a successful Stripe payment (verified webhook, idempotent), create the licence and sign a token.
2. Give the token to the customer in their account page and by e-mail. The app stores it in `%LOCALAPPDATA%\Clean\license.key`.
3. On renewal, sign a new token with a later `expiresAt`. On refund or chargeback, stop issuing new tokens; the current one ends at its `expiresAt` (keep tokens short, for example 35 days for a monthly plan, and let the app fetch the next one).
4. Offer cancellation in the account page and in the app (three clicks at most).

## Not done yet in the app

- No screen to paste or fetch a token, no paid/free feature gate, and the public key is not embedded: all of that waits for the website and for the decision on what is free and what is paid.
