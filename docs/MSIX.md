# Distribuer l'application Windows

**État : non fait.** Il manque des informations que seul le propriétaire peut obtenir (identité éditeur, certificat),
et les outils d'empaquetage Windows ne sont pas installés sur ce PC. Rien ci-dessous n'a été testé ; c'est la marche à suivre.

## Les trois voies

| Voie | Coût | Signature | Remarques |
|---|---|---|---|
| **Microsoft Store** (recommandée pour démarrer) | compte particulier gratuit | faite par le Store | vérification d'identité (pièce + selfie) ; mises à jour automatiques ; règles du Store à respecter |
| MSIX signé par toi, vendu sur ton site | environ 150-300 $/an (certificat) ou Azure Artifact Signing (environ 10 $/mois, réservé à certains profils) | toi | SmartScreen se méfie jusqu'à ce que la réputation se construise |
| Zip ou installateur non signé | 0 | aucune | alerte SmartScreen presque systématique : à éviter pour vendre |

Le Store n'empêche pas de vendre sur ton site : le bouton « Télécharger » du site peut mener à la fiche du Store.
**Attention :** si la licence est vendue sur ton site mais l'app distribuée par le Store, relis les règles du Store sur
les achats hors Store avant de publier.

## Étapes pour le Store

1. Créer le compte développeur (Partner Center) et **réserver le nom** de l'application.
2. Récupérer dans Partner Center les trois valeurs d'identité : `Identity Name`, `Publisher` (de la forme `CN=...`) et `PublisherDisplayName`.
3. Passer le projet en application empaquetée :
   - ajouter un `Package.appxmanifest` avec ces valeurs, le nom « Clean », les logos (44, 150, 310 px et variantes) et la capacité `runFullTrust` ;
   - construire avec `WindowsPackageType=MSIX` (empaquetage dans le projet, outils fournis par le paquet NuGet `Microsoft.Windows.SDK.BuildTools` déjà référencé) ;
   - **supprimer la dépendance au Windows App Runtime séparé** en l'embarquant (`WindowsAppSDKSelfContained`), pour que l'installation ne demande rien d'autre.
4. Tester le paquet avec le « Windows App Certification Kit » (installé avec le Windows SDK ; à installer alors).
5. Préparer la fiche : description, captures d'écran, **adresse de la politique de confidentialité** (`site/legal/confidentialite.html` en ligne), classification d'âge, catégorie.
6. Soumettre et suivre la certification.

## Avant le premier envoi

- La clé publique de licence doit être dans `LicenseKeys.PublicKey` et l'adresse de la boutique dans `LicenseViewModel.PurchaseUrl`.
- Compiler en **Release** (le contournement `CLEAN_OWNER_ACCESS` n'existe que dans les versions Debug).
- Vérifier que `THIRD-PARTY-NOTICES.md` est complet.
- Vérifier le comportement sans droits administrateur et avec Windows Defender actif.
