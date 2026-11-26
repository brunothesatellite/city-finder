# CityFinder

**Retrouvez la commune de chaque photo et ajoutez-la à son nom de fichier.**

CityFinder est une application Windows prévue en **C# / .NET 10 / WPF + WPF-UI**. Elle lit les coordonnées GPS des photos, identifie la commune avec **Photon/OpenStreetMap**, permet de vérifier le résultat sur une carte, puis renomme les fichiers sélectionnés en `Nom - Ville` ou `Nom - Ville , texte.extension`.

> **État : conception + maquette interactive. Le code applicatif C# reste à implémenter.**
> Aucune photo personnelle ni jeu de données réel n'est inclus dans ce dépôt. La maquette utilise 110 illustrations SVG et des données synthétiques.

## Fonctionnalités prévues

- choix d'un dossier de photos avec le sélecteur Windows natif ;
- lecture EXIF/XMP (JPEG, MPO GoPro, TIFF, PNG, WebP ; HEIC best-effort) ;
- reverse geocoding Photon avec cache, limitation de débit et sélection de la commune ;
- grille de photos avec vignettes, ville éditable, texte additionnel, sélection ;
- détection d'une ville déjà présente dans le nom, avec suggestion Photon ;
- carte OpenFreeMap synchronisée : miniature → repère, repère → miniature ;
- renommage partiel avec préflight, collisions auto-numérotées, journal et **annulation des renommages** ;
- **format `Nom - Ville , texte`** (texte optionnel) ;
- reprise de session via `.cityfinder.json` dans le dossier photo ;
- thème clair/sombre, accessibilité clavier, français par défaut.

## Maquette

La maquette HTML est la **référence visuelle et UX** :

- [`maquette/index.html`](maquette/index.html)
- [Voir la maquette](https://brunothesatellite.github.io/city-finder/maquette/) (GitHub Pages, si activé)

Pour la lancer en local, ouvrir `maquette/index.html` dans Chrome/Edge. Aucun build, aucun serveur. La carte utilise OpenFreeMap via MapLibre/Leaflet.

## Architecture cible

- **UI :** WPF/XAML + WPF-UI, MVVM ;
- **Photos :** MetadataExtractor, WIC/WPF pour les miniatures ;
- **Géocodage :** Photon via `HttpClient` C#, cache local, 1 req/s ;
- **Carte :** WebView2 + Leaflet + MapLibre + OpenFreeMap ;
- **Session :** `.cityfinder.json` dans le dossier photo, avec les noms d'origine pour l'annulation ;
- **Renommage :** `<base> - <ville> , <texte>`, préflight, auto-numérotation, journal, **annulation** ;
- **Distribution :** `win-x64` autonome.

Détails : [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md).

## Pourquoi pas uniquement du HTML `file://` ?

Chrome/Edge exposent `showDirectoryPicker` et `FileSystemFileHandle.move`, mais Photon est bloqué en CORS depuis `file://`, les permissions de dossier dépendent du navigateur et de la session, et le renommage en place n'est pas une garantie inter-navigateurs. L'application finale sera donc un exécutable C# ; la maquette HTML reste le démonstrateur.

## Licences

- Code CityFinder : MIT (voir `LICENSE`).
- Leaflet 1.9.4 : BSD-2 ; Leaflet.markercluster 1.5.3 : MIT ; MapLibre GL JS 5.24.1 : BSD-3 ; leaflet-maplibre-gl 0.1.4 : ISC.
- Fond : © OpenStreetMap contributors / OpenFreeMap / OpenMapTiles.
- Photon : API publique.
- Illustrations : générées pour ce dépôt, aucune photo réelle.
