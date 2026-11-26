# Third-party notices

CityFinder est distribué sous licence MIT. Il utilise les composants suivants,
tous compatibles avec une distribution open-source permissive.

## Runtime et framework

| Composant | Licence | Source |
|---|---|---|
| .NET 10 runtime | MIT | https://github.com/dotnet/runtime |
| WPF (Windows Presentation Foundation) | MIT | https://github.com/dotnet/wpf |

## Bibliothèques .NET

| Composant | Version | Licence | Source |
|---|---|---|---|
| CommunityToolkit.Mvvm | 8.4.2 | MIT | https://github.com/CommunityToolkit/dotnet |
| MetadataExtractor | 2.9.3 | Apache-2.0 | https://github.com/drewnoakes/metadata-extractor-dotnet |
| XmpCore (dépendance de MetadataExtractor) | 6.1.10.1 | Licence BSD du SDK Adobe XMP | https://github.com/drewnoakes/xmp-core-dotnet |
| Microsoft.Web.WebView2 SDK | 1.0.4370.26 | BSD-3-Clause | https://learn.microsoft.com/microsoft-edge/webview2/ |

Le runtime WebView2 est un composant système Microsoft, installé séparément et
non redistribué dans l'archive.

## Tests

| Composant | Licence |
|---|---|
| xUnit.net | Apache-2.0 |
| xunit.runner.visualstudio | Apache-2.0 |
| Microsoft.NET.Test.Sdk | MIT |
| coverlet.collector | MIT |

## Bibliothèques JavaScript de la carte

| Composant | Version | Licence | Source |
|---|---|---|---|
| Leaflet | 1.9.4 | BSD-2-Clause | https://leafletjs.com |
| Leaflet.markercluster | 1.5.3 | MIT | https://github.com/Leaflet/Leaflet.markercluster |
| MapLibre GL JS | 5.24.1 | BSD-3-Clause | https://maplibre.org |
| leaflet-maplibre-gl | 0.1.4 | ISC | https://github.com/maplibre/maplibre-gl-leaflet |

Les textes complets de ces licences sont inclus dans
`src/CityFinder.App/Map/vendor/` et `maquette/vendor/`.

## Cartographie et géocodage

- **OpenFreeMap** : styles et tuiles vectorielles, https://openfreemap.org
- **OpenMapTiles** : schéma de tuiles, https://www.openmaptiles.org
- **OpenStreetMap** : données © OpenStreetMap contributors, ODbL,
  https://www.openstreetmap.org/copyright
- **Photon** (komoot) : API publique de géocodage inverse,
  https://photon.komoot.io

L'attribution OpenStreetMap / OpenFreeMap / OpenMapTiles est affichée en
permanence sous la carte.

## Polices

Segoe UI et Georgia sont des polices système de Windows. Elles ne sont pas
redistribuées dans l'archive.
