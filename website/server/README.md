# Serveur de licences Clean

Petit serveur Node.js 24, **sans aucune dépendance**. Il fait trois choses :

1. reçoit les webhooks Stripe (signature vérifiée à la main, rejeu impossible, idempotent) ;
2. crée une licence par paiement et la signe avec la clé privée (ECDSA P-256, même format que l'app, voir `docs/LICENSING.md`) ;
3. donne la clé à la page « merci » du site (`GET /api/license?session_id=...`) et l'annule en cas de remboursement ou de litige.

L'app Windows ne parle jamais à ce serveur : elle vérifie le jeton **hors ligne** avec la clé publique.

## Lancer les tests

```
cd website/server
node --test          # 27 tests, aucun paquet à installer
```

Les tests de l'app (`dotnet test`) vérifient aussi que le code C# accepte des jetons signés par ce serveur
(`tests/Clean.Tests/Licensing/NodeInteropTests.cs`, fichier `test-fixtures/interop.json`, clé jetable).

## Mise en route (à faire par le propriétaire, voir `A-FAIRE-PAR-TOI.md`)

1. Générer la paire de clés, **sur le serveur** :
   ```
   node src/cli.js keygen --out /chemin/securise/license-private.pem
   ```
   Le fichier privé ne va jamais dans Git. La commande affiche la ligne à coller dans
   `src/Clean.Infrastructure/Licensing/LicenseKeys.cs` (clé publique).
2. Variables d'environnement :

   | Variable | Rôle |
   |---|---|
   | `STRIPE_WEBHOOK_SECRET` | secret du webhook Stripe (`whsec_...`) |
   | `LICENSE_PRIVATE_KEY_PATH` | chemin du fichier de clé privée |
   | `DATABASE_PATH` | fichier SQLite (défaut `./licenses.db`) ; à sauvegarder chaque jour |
   | `PORT` | port d'écoute (défaut 8787) |
   | `EXPECTED_AMOUNT`, `EXPECTED_CURRENCY` | montant en centimes et devise attendus (défaut 390 `eur`) |
   | `TRUST_PROXY` | `1` derrière un reverse proxy (HTTPS) pour lire la vraie adresse du client |

3. Lancer : `node src/main.js`, **derrière un reverse proxy HTTPS** (Caddy, Nginx...). Le serveur ne fait pas le HTTPS lui-même.
4. Dans Stripe : créer un webhook vers `https://ton-domaine/stripe/webhook` avec les événements
   `checkout.session.completed`, `checkout.session.async_payment_succeeded`, `charge.refunded`, `charge.dispute.created`.
5. Dans le lien de paiement / la Checkout Session, mettre l'adresse de succès
   `https://ton-domaine/merci.html?session_id={CHECKOUT_SESSION_ID}`.

## Ta propre licence « propriétaire »

```
node src/cli.js issue --plan owner --key /chemin/securise/license-private.pem
```

Colle le jeton affiché dans Paramètres > Licence. Valable sur tous tes PC, sans date de fin.

## Ce qui est volontairement simple

- **Aucun e-mail n'est envoyé** : `main.js` utilise `silentMailer`. Tant qu'aucun service d'envoi n'est branché,
  le client récupère sa clé sur la page « merci » (qui interroge l'API). À brancher avant la vente.
- La licence « lifetime » n'est pas liée à un PC et dure 70 ans. La liaison à un PC demanderait que le client
  donne l'identifiant de son PC à l'achat.
- Un remboursement annule la licence **côté serveur** : l'app, hors ligne, garde le jeton déjà installé.
  Si l'abus devient réel, on pourra ajouter une revalidation périodique.
- Données personnelles : seulement l'e-mail de l'acheteur. `Store.eraseEmail` répond à une demande d'effacement.
  Aucun journal ne contient d'e-mail ni de clé.

## Sécurité en bref

Signature Stripe en temps constant avec tolérance de 5 min, corps limité à 1 Mo, transaction par paiement
(échec = 500 et Stripe réessaie), limite de débit sur la recherche de clé, en-têtes de sécurité sur toutes les
réponses, aucune route d'administration exposée.
