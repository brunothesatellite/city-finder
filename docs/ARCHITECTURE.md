# CityFinder — architecture retenue

## Décision

- **UI :** WPF/XAML complet + WPF-UI, MVVM. La maquette HTML est la référence de design, pas l'UI de production.
- **Carte :** WebView2 hébergeant Leaflet + MapLibre + OpenFreeMap. C'est le seul composant web.
- **Géocodage :** Photon via `HttpClient` C#, cache local, 1 requête/seconde.
- **Session :** `.cityfinder.json` dans le dossier photo, avec repli dans `%LOCALAPPDATA%` si le dossier est en lecture seule.
- **Conflits :** renommage automatique ` (2)`, ` (3)`… sans écrasement.
- **Ville existante :** manifeste exact + parseur prudent (communes connues / ville Photon), pas de devinette.
- **UI :** français, thème clair/sombre, clavier, WCAG AA.
- **Cible :** Windows 10 22H2+ / Windows 11, .NET 10 LTS, `win-x64` autonome.

## Pipeline

1. Sélection du dossier (sélecteur natif).
2. Énumération des photos (JPEG/MPO/TIFF/PNG/WebP ; HEIC best-effort).
3. Lecture EXIF GPS + orientation (MetadataExtractor), validation.
4. Miniatures WPF/WIC 640 px, cache disque, chargement progressif.
5. Déduplication des coordonnées, reverse geocoding Photon, cache, choix de la ville.
6. Construction des cartes : ville existante, texte, sélection, état de session.
7. Édition ville/texte, filtres, recherche, carte synchronisée.
8. Préflight de renommage : noms cibles, collisions, longueur, déjà nommés.
9. Exécution séquentielle, numérotation automatique des collisions, journal.
10. Mise à jour de `.cityfinder.json` après chaque fichier ; reprise au prochain scan.
11. Annulation des renommages enregistrés dans `.cityfinder.json` (noms d'origine conservés).

## Session `.cityfinder.json`

- Stocké dans le dossier photo, attribut caché Windows, écriture atomique.
- Contient la version, l'identifiant du dossier, et une entrée par fichier renommé :
  nom d'origine, nom courant, ville, texte, date, taille/mtime.
- Au scan, rapprochement par nom + taille + date ; les fichiers déjà renommés sont décochés et verrouillés.
- Si le fichier de session est absent ou invalide, l'app repart d'un scan normal ; elle ne renomme jamais automatiquement.
- Repli AppData si le dossier photo est en lecture seule.

## Renommage

Forme : `<base_sans_ville> - <ville>[ , <texte>]<extension>`.

- Ville et texte assainis (caractères Windows interdits, longueur).
- Texte optionnel séparé par `" , "` (espace, virgule, espace).
- Si le nom se termine déjà par la ville/texte cible, il est ignoré.
- Collision cible existante ou double cible : suffixe ` (2)`, ` (3)`…, journalisé.
- Préflight avant exécution ; aucun écrasement.
- **Annulation** : bouton « Annuler les renommages » ; restauration inverse des noms d'origine, avec vérification que le fichier courant correspond au nom cible.
- Journal CSV optionnel.

## Carte

- WebView2 local (`appassets.local`), page Leaflet + MapLibre GL + leaflet-maplibre-gl.
- Styles OpenFreeMap Positron (clair) / Fiord (sombre), attribution OpenStreetMap/OpenFreeMap/OpenMapTiles.
- 1 repère par photo géolocalisée, clusters, liste de cluster au zoom maximal.
- Miniature → flyTo + popup ; repère → carte photo mise en évidence et défilement.
- Aucun préchargement ; cache WebView2 ; fallback quadrillé si le fond échoue.
- Pont JSON `postMessage` avec WPF.

## Dépendances

| Composant | Licence |
|---|---|
| .NET 10 / WPF | MIT |
| WPF-UI | MIT |
| CommunityToolkit.Mvvm | MIT |
| MetadataExtractor | Apache-2.0 |
| WebView2 SDK | BSD-3 |
| Leaflet | BSD-2 |
| Leaflet.markercluster | MIT |
| MapLibre GL JS | BSD-3 |
| leaflet-maplibre-gl | ISC |
| Photon / OpenFreeMap / OSM | services/données, attribution |

## Prochaines étapes

1. Scaffold solution + navigation WPF.
2. Scan + EXIF + miniatures.
3. Photon + cache.
4. Grille + édition + session + parseur ville existante.
5. Carte WebView2.
6. Préflight + renommage + annulation.
7. Tests, packaging, documentation.
