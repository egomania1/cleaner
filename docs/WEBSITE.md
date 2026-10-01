# Le site de vente

Dossier `website/` : le site (`site/`, statique) et le serveur de licences (`server/`, Node.js 24 sans dépendance).
Le paiement se fait sur le site, via Stripe. L'application Windows ne parle jamais au serveur.

```
Client ──► site statique (HTML/CSS/JS, aucun tiers)
   │            │ bouton « Acheter » ──► Stripe Checkout (page de paiement hébergée par Stripe)
   │            ▼
   │      merci.html?session_id=cs_...  ──► GET /api/license ──► serveur de licences ──► jeton signé
   │                                             ▲
   └──────────── Stripe ── webhook signé ────────┘   (crée la licence, annule en cas de remboursement)

App Windows : colle le jeton → vérification hors ligne avec la clé publique (aucun appel réseau).
```

## Ce qui est prêt

| Élément | État |
|---|---|
| Pages : accueil, merci, mentions légales, CGV, confidentialité, 404 | écrites ; **les 3 pages légales sont des modèles à compléter et à faire valider** |
| Mise en page mobile / tablette / ordinateur | vérifiée dans Chrome à 320, 360, 768 et 1280 px : aucun défilement horizontal, cibles tactiles d'au moins 44 px |
| Accessibilité | contrastes mesurés (texte AA dans les deux modes, bordures de contrôles ≥ 3:1), focus visible, lien d'évitement, mode sombre et clair, `prefers-reduced-motion` |
| Vie privée | aucun cookie, aucun traceur, aucune police ni script externe |
| Sécurité du site | politique de sécurité stricte (`_headers`), aucun style ni script en ligne (vérifié par test) |
| Serveur de licences | 27 tests : signature Stripe, rejeu, remboursement, échec de base de données, limite de débit |
| Compatibilité app / serveur | test croisé : l'app C# accepte un jeton signé par le serveur Node |

## Ce qui reste à faire (voir `A-FAIRE-PAR-TOI.md`)

Compte Stripe, domaine, hébergement, HTTPS, clés réelles, envoi d'e-mails, textes légaux complétés et validés.
Lighthouse n'a pas été lancé (non disponible ici) : à passer une fois le site en ligne, objectif 90 et plus.

## Configurer le site

Éditer `site/js/config.js` :

| Clé | Valeur |
|---|---|
| `checkoutUrl` | lien de paiement Stripe (Payment Link) à 3,90 € |
| `downloadUrl` | page de téléchargement (Microsoft Store ou installateur signé) |
| `apiBase` | vide si le reverse proxy envoie `/api` et `/stripe` au serveur de licences (recommandé) |

Tant que `checkoutUrl` et `downloadUrl` sont vides, les boutons affichent « Bientôt disponible ».
Seules les adresses `https:` (et `ms-windows-store:`) sont acceptées.

## Mettre le site en ligne

1. Héberger `site/` (n'importe quel hébergement statique, en UE de préférence) avec HTTPS obligatoire.
2. Appliquer les en-têtes de `site/_headers` (Cloudflare Pages et Netlify le lisent tel quel). Pour Caddy :

   ```
   ton-domaine.fr {
     root * /var/www/clean-site
     encode gzip
     file_server
     header {
       Content-Security-Policy "default-src 'none'; script-src 'self'; style-src 'self'; img-src 'self' data:; font-src 'self'; connect-src 'self'; form-action 'none'; base-uri 'none'; frame-ancestors 'none'"
       Strict-Transport-Security "max-age=63072000; includeSubDomains"
       X-Content-Type-Options nosniff
       X-Frame-Options DENY
       Referrer-Policy no-referrer
       Permissions-Policy "camera=(), microphone=(), geolocation=(), payment=()"
     }
     handle /api/* { reverse_proxy 127.0.0.1:8787 }
     handle /stripe/* { reverse_proxy 127.0.0.1:8787 }
     handle_errors { rewrite * /404.html
       file_server }
   }
   ```

   Caddy obtient et renouvelle seul le certificat HTTPS. Avec ce réglage, lance le serveur avec `TRUST_PROXY=1`.
3. Lancer le serveur de licences (`website/server/README.md`) comme service (systemd, par exemple), avec un utilisateur sans droits,
   le fichier de clé privée en lecture seule pour lui, et une sauvegarde quotidienne du fichier SQLite.
4. Remplacer `ton-domaine` dans `site/robots.txt`, et ajouter un `sitemap.xml` si tu veux le référencement.

## Paiement Stripe

1. Créer le produit « Clean, licence » à 3,90 € TTC, **paiement unique** (pas d'abonnement).
2. Créer un **Payment Link** (ou une Checkout Session) avec :
   - adresse de succès `https://ton-domaine.fr/merci.html?session_id={CHECKOUT_SESSION_ID}` ;
   - collecte de l'e-mail ;
   - case d'acceptation des conditions de vente, avec un texte qui recueille **le consentement exprès à la fourniture
     immédiate de la clé et la reconnaissance de la perte du droit de rétractation** (voir l'article 8 des CGV).
     Les Payment Links n'offrent qu'une case de ce type : le texte doit donc couvrir les deux points. **À faire valider par un juriste.**
3. Créer le webhook vers `https://ton-domaine.fr/stripe/webhook` avec les événements
   `checkout.session.completed`, `checkout.session.async_payment_succeeded`, `charge.refunded`, `charge.dispute.created`.
4. Tester d'abord en **mode test** de Stripe (clés `cs_test_...`), puis passer en production.
5. Facturation et TVA : voir avec ton comptable (statut de l'éditeur, TVA du pays du client, guichet unique OSS).

## Contrôle de sécurité (audit, section 3)

| Point | Où |
|---|---|
| Aucun mot de passe ni compte à protéger | pas de comptes : la clé s'affiche via l'identifiant de session Stripe (impossible à deviner) |
| Webhooks signés et vérifiés | `server/src/stripe.js`, testé |
| Licence vérifiée côté client sans moyen de la fabriquer | signature ECDSA, clé privée uniquement sur le serveur |
| XSS | le site n'insère aucune donnée dans le HTML (la clé va dans un `textarea` via `.value`) ; CSP stricte |
| CSRF | aucune session, aucun cookie, aucune action d'écriture déclenchée par un navigateur (le webhook exige la signature Stripe) |
| Injection SQL | requêtes préparées partout (`server/src/store.js`) |
| Limitation de débit | recherche de clé : 20 par minute et par adresse |
| Journalisation sans données personnelles | testé : ni e-mail ni clé dans les journaux |
| Sauvegardes | **à mettre en place** : copie quotidienne du fichier SQLite et de la clé privée (chiffrée, hors serveur) ; tester la restauration |
| Mise à jour des dépendances | aucune dépendance côté serveur ni site |
