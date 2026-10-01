// À remplir quand la boutique existe (voir A-FAIRE-PAR-TOI.md). Tant que ces adresses sont vides,
// les boutons « Acheter » et « Télécharger » affichent « Bientôt disponible » et ne mènent nulle part.
window.CLEAN_SITE = {
  // Lien de paiement Stripe (Payment Link) ou adresse de ta page de paiement.
  checkoutUrl: "",
  // Page de téléchargement (Microsoft Store ou installateur signé).
  downloadUrl: "",
  // Racine de l'API de licences. Vide = même domaine (recommandé : le reverse proxy envoie /api et /stripe au serveur).
  apiBase: "",
};
