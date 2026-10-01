// Logique de la page « merci » : va chercher la licence créée par le webhook Stripe.
// Séparée de l'affichage pour être testée sans navigateur (website/site/test).

const SESSION_ID = /^cs_(test|live)_[A-Za-z0-9]{10,200}$/;

export function isValidSessionId(value) {
  return typeof value === "string" && SESSION_ID.test(value);
}

// Retourne { state, ... } avec state parmi: invalid, active, revoked, timeout, error.
// Le paiement est confirmé par Stripe quelques secondes après le retour du client sur le site : tant que la
// licence n'existe pas (404 « pending »), on réessaie.
export async function lookupLicense({ sessionId, fetchFn, apiBase = "", sleep, attempts = 20, delayMs = 3000 }) {
  if (!isValidSessionId(sessionId)) return { state: "invalid" };

  let lastError = false;
  for (let attempt = 0; attempt < attempts; attempt++) {
    try {
      const response = await fetchFn(`${apiBase}/api/license?session_id=${encodeURIComponent(sessionId)}`, {
        headers: { Accept: "application/json" },
        cache: "no-store",
      });

      if (response.status === 200) {
        const body = await response.json();
        if (typeof body.token === "string" && body.token.includes(".")) {
          return { state: "active", token: body.token, licenseId: body.licenseId };
        }
        return { state: "error" };
      }
      if (response.status === 410) return { state: "revoked" };
      if (response.status === 400) return { state: "invalid" };
      if (response.status === 429) {
        await sleep(Math.max(1, Number(response.headers.get("retry-after")) || 60) * 1000);
        continue;
      }
      lastError = response.status !== 404;
    } catch {
      lastError = true;
    }
    if (attempt < attempts - 1) await sleep(delayMs);
  }
  return { state: lastError ? "error" : "timeout" };
}
