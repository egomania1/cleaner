import { lookupLicense } from "./licence-lookup.js";

const config = window.CLEAN_SITE || {};
const status = document.getElementById("statut");
const result = document.getElementById("resultat");
const keyBox = document.getElementById("cle");
const copyButton = document.getElementById("copier");
const copyNote = document.getElementById("copie-ok");

const messages = {
  invalid: "Ce lien de confirmation n'est pas valide. Ouvre-le depuis l'e-mail ou la page de paiement, ou écris-nous avec ta preuve d'achat.",
  revoked: "Cette licence n'est plus active (remboursement ou litige). Écris-nous si tu penses à une erreur.",
  timeout: "Ton paiement n'est pas encore confirmé. Recharge cette page dans quelques minutes : ta clé s'affichera ici dès que Stripe aura confirmé.",
  error: "Impossible de récupérer ta clé pour le moment. Recharge la page. Si ça persiste, écris-nous avec ta preuve d'achat.",
};

const sessionId = new URLSearchParams(window.location.search).get("session_id");

status.textContent = "Confirmation du paiement en cours…";

const outcome = await lookupLicense({
  sessionId,
  fetchFn: (url, options) => fetch(url, options),
  apiBase: config.apiBase || "",
  sleep: (ms) => new Promise((resolve) => setTimeout(resolve, ms)),
});

if (outcome.state === "active") {
  status.textContent = "Paiement confirmé. Voici ta clé de licence.";
  keyBox.value = outcome.token;
  result.hidden = false;
  keyBox.focus();
} else {
  status.textContent = messages[outcome.state] || messages.error;
}

copyButton.addEventListener("click", async () => {
  try {
    await navigator.clipboard.writeText(keyBox.value);
    copyNote.textContent = "Clé copiée.";
  } catch {
    keyBox.select();
    copyNote.textContent = "Copie impossible ici : la clé est sélectionnée, copie-la avec Ctrl+C.";
  }
});
