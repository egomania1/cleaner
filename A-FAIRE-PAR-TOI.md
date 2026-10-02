# À faire par toi

Ce que je ne peux pas faire à ta place (comptes, argent, identité, décisions légales, tests sur ton matériel).
Je coche et je complète cette liste au fur et à mesure. Date de dernière mise à jour : voir `git log`.

## Bloquants pour vendre

- [ ] **Statut juridique** : micro-entreprise ou société, obtenir le SIRET. Il faut le nom légal, l'adresse, l'e-mail de contact et, si applicable, le numéro de TVA pour les mentions légales et les CGV.
- [ ] **Remplacer les `[À COMPLÉTER]`** dans `website/legal/` (mentions légales, CGV, confidentialité) puis **faire relire par un juriste** ou un service spécialisé. Je ne peux pas valider un texte juridique.
- [ ] **Médiateur de la consommation** : adhérer à un médiateur (obligatoire en vente aux particuliers) et mettre ses coordonnées dans les CGV.
- [ ] **Compte Stripe** : créer le compte, activer le paiement, créer le produit « Clean » à 3,90 € TTC (paiement unique), récupérer la clé secrète et le secret du webhook. Ne jamais les mettre dans Git.
- [ ] **Nom de domaine et hébergement** (UE de préférence) pour le site et le petit serveur de licences. Mettre en place HTTPS.
- [ ] **Générer les clés de licence réelles** (voir `website/server/README.md`) : la clé privée reste sur le serveur, la clé publique est à coller dans `LicenseKeys.PublicKey`, et l'adresse de la boutique dans `LicenseViewModel.PurchaseUrl`.
- [ ] **Héberger le serveur de licences** (`website/server`, Node 24, derrière un reverse proxy HTTPS, sauvegarde quotidienne du fichier SQLite) et créer le webhook Stripe : étapes détaillées dans `website/server/README.md`.
- [ ] **Envoi d'e-mails** (clé de licence, reçu, rappel) : choisir un service d'envoi (compatible RGPD) et le brancher.
- [ ] **Signature de code / distribution** : soit Microsoft Store (compte développeur particulier gratuit, vérification d'identité), soit certificat de signature (environ 150-300 $/an) ou Azure Artifact Signing. Voir `docs/MSIX.md`.
- [ ] **Vérifier le nom « Clean »** (INPI, noms de domaine, Microsoft Store) : il est très générique.

## À tester sur ton matériel

- [ ] **Narrateur** (lecteur d'écran Windows) : parcourir chaque page de l'app.
- [ ] **Mise à l'échelle Windows à 125 %, 150 %, 200 %** et grandes polices : vérifier qu'aucun texte n'est coupé.
- [ ] **Un vrai disque en FAT/exFAT** (clé USB) : vérifier le message quand les droits de l'archive ne peuvent pas être restreints.
- [ ] **Un vrai nettoyage de bout en bout** avec un navigateur ouvert puis fermé, et une restauration.
- [ ] **Le site sur un téléphone et une tablette** une fois en ligne (je l'ai testé seulement en simulation).

## À décider

- [ ] Fonctions payantes : j'ai appliqué « analyse gratuite, nettoyage payant, restauration toujours gratuite ». À confirmer ou changer.
- [ ] Prix : 3,90 € en achat unique. Confirmer s'il est TTC et s'il y aura des offres (famille, plusieurs PC).
- [ ] Nombre de PC par licence (aujourd'hui : une licence peut être liée à un PC ou libre).
- [ ] Langues : tout est en français aujourd'hui.
- [ ] Support client : adresse e-mail et délai de réponse annoncé dans les CGV.

## Fonctions que je n'ai pas faites sans ton avis (face à CCleaner et consorts)

Chacune touche à ton système ou demande une décision de produit. Dis-moi lesquelles tu veux, je les ferai une par une.

- [x] **Désinstalleur qui supprime les restes** : fait (page Applications). À essayer par toi sur une petite application dont tu ne te sers plus.
- [x] **Maintenance automatique silencieuse** : faite, désactivée par défaut (Paramètres, interrupteur). À tester par toi : l'activer, puis lancer la tâche « Clean - Maintenance » depuis le Planificateur de tâches Windows et lire le rapport dans Paramètres.
- [ ] **Allègement de Windows 11** (applications préinstallées, télémétrie), réversible : risqué, à cadrer précisément.
- [ ] **Test de compatibilité Windows 11** pour les PC sous Windows 10.
- [x] **Page Développeur** (dossiers `node_modules`, `bin/obj`…) : faite. Essaie-la sur tes disques de projets (H:, E:).
- [ ] **Démarrage : durée en secondes** : Windows ne la donne pas sans droits administrateur, donc la page n'en affiche aucune plutôt que d'en inventer.
- [x] **Point de restauration Windows** avant un nettoyage : fait, désactivé par défaut (Paramètres). À tester par toi : il demande l'autorisation administrateur de Windows.

## Plus tard

- [ ] Captures d'écran et vidéo de démonstration pour le site.
- [ ] Textes complets des licences tierces dans `THIRD-PARTY-NOTICES.md`.
- [ ] Vérifier les droits des images et idées de design réutilisées (liste dans `docs/PROGRESS.md`).
