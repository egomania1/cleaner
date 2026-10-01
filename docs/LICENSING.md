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
3. The product is a **one-time purchase (3,90 €)**: sign a token with `plan: "lifetime"` and an `expiresAt` far in the future (for example 70 years). On refund within the withdrawal period, the website records the refund and the token is no longer re-issued; a short-lived token with a check-in can be added later if abuse appears.
4. Show the price (3,90 € TTC, one-time) and the 14-day withdrawal terms before payment; ask for the express consent to immediate delivery and the waiver of withdrawal if the licence is delivered at once.

## In the app today

- Trial: 5 full days from the first launch (`trial.json`), then analysis, history and restoring stay free; moving or removing files needs a licence (`LicenseEvaluator`, `LicensedCleaner`, `LicensedFileRemover`).
- Settings page: licence card, key field, PC id, log folder.
- Still missing: the real public key (`LicenseKeys.PublicKey`, null for now, so activation is refused), the shop URL (`LicenseViewModel.PurchaseUrl`, empty, so the buy button is hidden).
