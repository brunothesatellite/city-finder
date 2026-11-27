# CityFinder

**La commune de chaque photo, ajoutée à son nom de fichier.**

CityFinder est une application Windows native en **C# / .NET 10 / WPF**. Elle lit les coordonnées GPS des photos, retrouve la commune avec **Photon/OpenStreetMap**, permet de vérifier le résultat sur une carte, puis renomme les fichiers sélectionnés.

> **État : MVP 0.2.1 fonctionnel.** L'interface WPF/XAML, la carte WebView2, la reprise de session, l'annulation des renommages et la mise à jour automatique sont implémentées et testées. Les confirmations et messages utilisent des fenêtres entièrement thémées, sans boîte système.

Aucune photo, aucun jeu de données personnel et aucun chemin local ne sont versionnés dans ce dépôt. La maquette publique utilise 110 illustrations SVG et des villes synthétiques.

## Fonctionnalités

- sélection d'un dossier avec le sélecteur Windows natif, ou glisser-déposer ;
- lecture EXIF/XMP : JPEG, MPO, TIFF, PNG, WebP ; HEIC selon les codecs Windows ;
- géocodage inverse Photon, cache local et limite de débit ;
- grille de photos avec vignettes orientées, ville éditable, texte optionnel, sélection ;
- filtres Toutes / Avec GPS / Sans ville / Sans GPS / Sans texte additionnel / À traiter / Renommées, avec **Tout cocher / Tout décocher** sur la vue courante ;
- rafraîchissement automatique des filtres « Sans ville » et « Sans texte additionnel » après validation d'une ville ou d'un texte ;
- édition groupée de la **ville** ou du **texte additionnel** sur les photos sélectionnées ou visibles, avec aperçu du nom cible et confirmation avant tout remplacement ;
- fiche photo (double-clic sur une vignette ou depuis la carte) : grande image, métadonnées, ville et texte éditables, inclusion dans le renommage ;
- détection prudente d'une ville déjà présente dans le nom, avec suggestion Photon ;
- carte Leaflet synchronisée avec la grille :
  - fond OpenStreetMap par défaut (dernier fond choisi mémorisé), léger assombrissement en thème sombre ;
  - repère isolé → pastille circulaire avec la miniature de la photo ; icône d'appareil photo si l'aperçu est indisponible ;
  - miniature → centrage et info-bulle avec vignette de la photo et accès à la fiche ;
  - repère → sélection et défilement de la carte photo ;
  - clusters numérotés ; un clic déplie jusqu'à 12 photos en éventail avec leurs vignettes, au-delà la carte zoome d'abord ;
  - menu **Calque** avec aperçu local de chaque fond : plan OpenFreeMap (clair/sombre), OpenStreetMap, satellite Esri et relief OpenTopoMap. Aucune clé API n'est nécessaire ;
- vérification des mises à jour GitHub au démarrage (au plus une fois par 24 h) et via un bouton dédié : téléchargement de l'archive portable, vérification SHA-256, puis installation après redémarrage ;
- renommage partiel : seules les photos cochées sont traitées ;
- collision automatiquement numérotée ` (2)`, ` (3)`… sans jamais écraser un fichier ;
- reprise de session via `.cityfinder.json` dans le dossier photo ;
- annulation des renommages, y compris après redémarrage ;
- thème clair/sombre, raccourcis clavier, textes français ;
- confirmations, résultats et messages dans des fenêtres WPF au thème de l'application, avec barre de titre maison.

## Format des noms

```text
<base> - <ville><extension>
<base> - <ville>, <texte optionnel><extension>
```

Exemples :

| Avant | Après |
|---|---|
| `IMG_0001.jpg` (Annecy) | `IMG_0001 - Annecy.jpg` |
| `IMG_0001.jpg` (Annecy, « lac ») | `IMG_0001 - Annecy, lac.jpg` |
| `IMG_0002 - Annecy.jpg` (Chambéry) | `IMG_0002 - Chambéry.jpg` |

La ville et le texte sont assainis (caractères interdits Windows, longueur). Les accents sont conservés. Les anciens formats `" - Ville , texte"` et `" - Ville texte"` restent lus pour les fichiers déjà nommés, mais les nouveaux noms utilisent `", "`.

## Démarrage rapide

### Version portable

1. Télécharger `CityFinder-0.2.1-win-x64.zip` depuis les [releases](https://github.com/brunothesatellite/city-finder/releases/latest).
2. Décompresser le dossier.
3. Lancer `CityFinder.exe`. Aucun droit administrateur n'est requis.

Prérequis : Windows 10 22H2 ou Windows 11, avec le **runtime Microsoft Edge WebView2** (présent par défaut sur Windows 11 et la plupart des Windows 10 récents). Si la carte ne démarre pas, l'application propose le lien d'installation et reste utilisable sans carte.

### Depuis les sources

Prérequis : [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```powershell
dotnet build CityFinder.slnx
dotnet run --project src/CityFinder.App/CityFinder.App.csproj
```

Publier une version autonome :

```powershell
dotnet publish src/CityFinder.App/CityFinder.App.csproj `
  -c Release -r win-x64 --self-contained true `
  -o dist/CityFinder-win-x64
```

Tests :

```powershell
dotnet test CityFinder.slnx
```

## Utilisation

1. Choisir un dossier. Les photos sans GPS restent visibles mais ne sont pas cochées.
2. Attendre la proposition des communes (mise en cache ; environ une requête Photon par seconde au premier passage).
3. Vérifier ou corriger la ville, ajouter un texte, cocher les photos à traiter. **Tout cocher** traite la vue courante ; **Ville groupée** et **Texte groupé** évitent la saisie répétée et demandent confirmation avant d'écraser une valeur existante.
4. Double-cliquer sur une vignette (ou utiliser **Voir la fiche** sur la carte) pour ouvrir la fiche détaillée : image, métadonnées, ville et texte restent modifiables.
5. Cliquer sur **Renommer** et vérifier l'aperçu avant confirmation.
6. En cas de doute, utiliser **Annuler les renommages** dans la bannière de reprise ou dans le résultat.

La grille reste utilisable pendant la recherche des communes. Une photo déjà renommée est décochée et verrouillée ; la modifier la déverrouille explicitement.

## Reprise et sécurité

- Le fichier `.cityfinder.json` est écrit **dans le dossier photo**, avec l'attribut caché Windows.
- Il contient le nom d'origine, le nom courant, la ville, le texte et l'identité du fichier (taille/date), pour restaurer les noms et reprendre une session.
- Si le dossier est en lecture seule, un repli est utilisé dans `%LOCALAPPDATA%\CityFinder\sessions`.
- Le fichier de session est supprimé quand il ne reste plus aucun renommage à annuler.
- Aucun écrasement : les collisions sont numérotées et les conflits d'annulation sont signalés.
- Le format `Nom - Ville, texte` est stable ; les fichiers déjà au bon nom sont ignorés.

## Confidentialité

- Les photos ne quittent jamais l'ordinateur.
- Seules les coordonnées arrondies (5 décimales) sont envoyées à Photon pour le géocodage inverse ; aucun nom de fichier, chemin ou contenu d'image n'est transmis.
- Le cache Photon et les vignettes restent locaux (`%LOCALAPPDATA%\CityFinder`).
- Le fond de carte sélectionné (OpenFreeMap, OpenStreetMap, Esri ou OpenTopoMap) ne reçoit que la zone affichée, sans préchargement. Les vignettes ne sont jamais envoyées au service de cartes.
- La vérification de mise à jour contacte l'API GitHub et télécharge l'archive officielle depuis GitHub. Aucune photo, aucun chemin et aucun nom de fichier ne sont transmis. L'installation est refusée si l'empreinte SHA-256 publiée ne correspond pas.
- Aucun compte, aucune télémétrie, aucune publicité.

## Architecture

- **UI** : WPF/XAML, styles XAML sur mesure (sans dépendance WPF-UI), MVVM avec CommunityToolkit.Mvvm.
- **Métadonnées** : MetadataExtractor.
- **Géocodage** : Photon via `HttpClient`, cache JSON local, limiteur de débit.
- **Carte** : WebView2 local, Leaflet 1.9.4, Leaflet.markercluster 1.5.3, MapLibre GL 5.24.1, leaflet-maplibre-gl 0.1.4 ; fonds OpenFreeMap, OpenStreetMap, Esri World Imagery et OpenTopoMap.
- **Domaine** : planification du renommage, parseur de ville prudente, session, annulation, tests.
- **Session** : `.cityfinder.json` atomique, avec repli AppData.
- **Mises à jour** : API GitHub, archive portable vérifiée SHA-256, script PowerShell de remplacement après fermeture puis redémarrage.
- **Distribution** : `win-x64` autonome.

Détails : [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md).

```text
src/
  CityFinder.Core/            domaine, noms, session, planification
  CityFinder.Infrastructure/  EXIF, Photon, fichiers, session JSON, renommage, mises à jour
  CityFinder.App/             WPF, MVVM, carte WebView2, ressources
tests/
  CityFinder.Core.Tests/
  CityFinder.Infrastructure.Tests/
maquette/                     maquette HTML de référence
```

## Maquette

La maquette HTML reste la référence visuelle et UX :

- [`maquette/index.html`](maquette/index.html)
- [Voir la maquette en ligne](https://brunothesatellite.github.io/city-finder/maquette/)

La maquette utilise des données synthétiques et des illustrations de remplacement.

## Limites connues

- Le premier géocodage est volontairement limité à environ une requête par seconde ; les scans suivants utilisent le cache.
- Photon est un service public : sans réseau, les villes restent modifiables manuellement.
- HEIC et WebP dépendent des codecs installés dans Windows ; les fichiers non décodables restent listés et renommables.
- La carte nécessite le runtime WebView2 ; le reste de l'application fonctionne sans carte.
- Google Maps n'est pas proposé : ses tuiles ne peuvent pas être utilisées hors des API Google payantes, et aucune clé n'est demandée. Les fonds libres retenus n'ont pas cette contrainte ; leur disponibilité dépend de leurs fournisseurs respectifs.
- Les tuiles OpenStreetMap et OpenTopoMap sont des services publics soumis à leurs conditions d'usage ; l'application reste à faible volume et met en cache les requêtes Photon.
- La mise à jour automatique nécessite un dossier d'installation accessible en écriture. Dans un dossier protégé (Program Files, partage réseau), l'application le signale et invite à remplacer l'archive manuellement.
- L'installation aligne le dossier de l'application sur l'archive de la release (miroir). Ne stockez pas de fichiers personnels dans ce dossier ; les photos et la session `.cityfinder.json` restent dans vos dossiers photo.
- La vérification de mise à jour dépend de l'API GitHub (hors ligne ou quota atteint : message explicite lors d'une vérification manuelle, silence au démarrage).
- L'interface est en français.

## Licences

- Code CityFinder : MIT, voir [`LICENSE`](LICENSE).
- Dépendances et fonds cartographiques : voir [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md).
- Les licences des bibliothèques JavaScript vendorisées sont incluses dans `maquette/vendor/` et `src/CityFinder.App/Map/vendor/`.
- Fonds cartographiques : © OpenStreetMap contributors · OpenFreeMap · OpenMapTiles · Esri · OpenTopoMap. Attribution affichée dans la carte.
