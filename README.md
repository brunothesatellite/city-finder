# CityFinder

**La commune de chaque photo, ajoutée à son nom de fichier.**

CityFinder est une application Windows native en **C# / .NET 10 / WPF**. Elle lit les coordonnées GPS des photos, retrouve la commune avec **Photon/OpenStreetMap**, permet de vérifier le résultat sur une carte, puis renomme les fichiers sélectionnés.

> **État : MVP 0.1 fonctionnel.** L'interface WPF/XAML, la carte WebView2, la reprise de session et l'annulation des renommages sont implémentées et testées.

Aucune photo, aucun jeu de données personnel et aucun chemin local ne sont versionnés dans ce dépôt. La maquette publique utilise 110 illustrations SVG et des villes synthétiques.

## Fonctionnalités

- sélection d'un dossier avec le sélecteur Windows natif, ou glisser-déposer ;
- lecture EXIF/XMP : JPEG, MPO, TIFF, PNG, WebP ; HEIC selon les codecs Windows ;
- géocodage inverse Photon, cache local et limite de débit ;
- grille de photos avec vignettes orientées, ville éditable, texte optionnel, sélection ;
- détection prudente d'une ville déjà présente dans le nom, avec suggestion Photon ;
- carte OpenFreeMap (Leaflet + MapLibre) synchronisée avec la grille :
  - miniature → centrage et info-bulle ;
  - repère → sélection et défilement de la carte photo ;
  - clusters, thème clair/sombre, fond de secours hors ligne ;
- renommage partiel : seules les photos cochées sont traitées ;
- collision automatiquement numérotée ` (2)`, ` (3)`… sans jamais écraser un fichier ;
- reprise de session via `.cityfinder.json` dans le dossier photo ;
- annulation des renommages, y compris après redémarrage ;
- thème clair/sombre, raccourcis clavier, textes français.

## Format des noms

```text
<base> - <ville><extension>
<base> - <ville> , <texte optionnel><extension>
```

Exemples :

| Avant | Après |
|---|---|
| `IMG_0001.jpg` (Annecy) | `IMG_0001 - Annecy.jpg` |
| `IMG_0001.jpg` (Annecy, « lac ») | `IMG_0001 - Annecy , lac.jpg` |
| `IMG_0002 - Annecy.jpg` (Chambéry) | `IMG_0002 - Chambéry.jpg` |

La ville et le texte sont assainis (caractères interdits Windows, longueur). Les accents sont conservés. L'ancien séparateur espace reste lisible pour les fichiers déjà nommés.

## Démarrage rapide

### Version portable

1. Télécharger `CityFinder-0.1.0-win-x64.zip` depuis les [releases](https://github.com/brunothesatellite/city-finder/releases/latest).
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
3. Vérifier ou corriger la ville, ajouter un texte, cocher les photos à traiter.
4. Cliquer sur **Renommer** et vérifier l'aperçu avant confirmation.
5. En cas de doute, utiliser **Annuler les renommages** dans la bannière de reprise ou dans le résultat.

La grille reste utilisable pendant la recherche des communes. Une photo déjà renommée est décochée et verrouillée ; la modifier la déverrouille explicitement.

## Reprise et sécurité

- Le fichier `.cityfinder.json` est écrit **dans le dossier photo**, avec l'attribut caché Windows.
- Il contient le nom d'origine, le nom courant, la ville, le texte et l'identité du fichier (taille/date), pour restaurer les noms et reprendre une session.
- Si le dossier est en lecture seule, un repli est utilisé dans `%LOCALAPPDATA%\CityFinder\sessions`.
- Le fichier de session est supprimé quand il ne reste plus aucun renommage à annuler.
- Aucun écrasement : les collisions sont numérotées et les conflits d'annulation sont signalés.
- Le format `Nom - Ville , texte` est stable ; les fichiers déjà au bon nom sont ignorés.

## Confidentialité

- Les photos ne quittent jamais l'ordinateur.
- Seules les coordonnées arrondies (5 décimales) sont envoyées à Photon pour le géocodage inverse ; aucun nom de fichier, chemin ou contenu d'image n'est transmis.
- Le cache Photon et les vignettes restent locaux (`%LOCALAPPDATA%\CityFinder`).
- Le fond de carte OpenFreeMap ne reçoit que la zone affichée, sans préchargement.
- Aucun compte, aucune télémétrie, aucune publicité.

## Architecture

- **UI** : WPF/XAML, styles XAML sur mesure (sans dépendance WPF-UI), MVVM avec CommunityToolkit.Mvvm.
- **Métadonnées** : MetadataExtractor.
- **Géocodage** : Photon via `HttpClient`, cache JSON local, limiteur de débit.
- **Carte** : WebView2 local, Leaflet 1.9.4, Leaflet.markercluster 1.5.3, MapLibre GL 5.24.1, leaflet-maplibre-gl 0.1.4, styles OpenFreeMap.
- **Domaine** : planification du renommage, parseur de ville prudente, session, annulation, tests.
- **Session** : `.cityfinder.json` atomique, avec repli AppData.
- **Distribution** : `win-x64` autonome.

Détails : [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md).

```text
src/
  CityFinder.Core/            domaine, noms, session, planification
  CityFinder.Infrastructure/  EXIF, Photon, fichiers, session JSON, renommage
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
- L'interface est en français.

## Licences

- Code CityFinder : MIT, voir [`LICENSE`](LICENSE).
- Dépendances et fonds cartographiques : voir [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md).
- Les licences des bibliothèques JavaScript vendorisées sont incluses dans `maquette/vendor/` et `src/CityFinder.App/Map/vendor/`.
- Fond cartographique : © OpenStreetMap contributors · OpenFreeMap · OpenMapTiles.
