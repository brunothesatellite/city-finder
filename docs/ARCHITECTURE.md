# CityFinder — architecture

## Décisions

| Sujet | Décision |
|---|---|
| Interface | **WPF/XAML natif**, styles XAML sur mesure, MVVM. Pas de WPF-UI pour garder la direction visuelle de la maquette. |
| Carte | **WebView2** local hébergeant Leaflet + MapLibre + leaflet-maplibre-gl. Seul composant web de l'application. Fonds sans clé API : OpenFreeMap, OpenStreetMap, Esri World Imagery, OpenTopoMap. |
| Runtime | **.NET 10 LTS**, cible `net10.0-windows`, publication `win-x64` autonome. |
| Métadonnées | **MetadataExtractor** (EXIF/XMP), miniatures WPF/WIC. |
| Géocodage | **Photon** via `HttpClient` C#, cache JSON local, limitation à ~1 requête/seconde. |
| Ville existante | **Manifeste + parseur prudent** : une ville n'est reconnue que si elle figure dans la liste des communes connues ou dans les résultats Photon du dossier. Aucune devinette. |
| Conflits | Numérotation automatique ` (2)`, ` (3)`…, aucun écrasement. |
| Session | **`.cityfinder.json`** dans le dossier photo, écriture atomique, attribut caché, repli `%LOCALAPPDATA%` si le dossier est en lecture seule. |
| Annulation | Restauration des noms d'origine enregistrés dans la session, par lot ou pour toute la session. |
| Reprise | Les fichiers déjà renommés sont décochés et verrouillés ; ils ne sont **jamais** renommés à nouveau par défaut. |
| Texte | Séparateur `", "` entre la ville et le texte optionnel. |
| Langue | Français, dates `fr-FR`, interface clavier. |
| Distribution | Dossier autonome `win-x64` + archive ZIP ; runtime WebView2 système. |

## Structure

```text
CityFinder.slnx
src/
  CityFinder.Core/
    Models/             Photo, GeoPlace, RenamePlan, SessionDocument, statistiques
    Naming/             assainissement, parseur, planificateur, catalogue de communes
    Selection/          état initial (sélection, ville existante, suggestion)
    Session/            rapprochement session ↔ fichiers
  CityFinder.Infrastructure/
    Metadata/           lecture EXIF/XMP, orientation, dimensions
    Scanning/           énumération et construction des Photo
    Geocoding/          client Photon, cache local, limiteur
    Session/            JsonSessionStore atomique + repli AppData
    Renaming/           préflight, exécution, session, annulation
    Updates/            release GitHub, téléchargement, SHA-256, extraction
  CityFinder.App/
    ViewModels/         MainViewModel, PhotoViewModel, filtres, édition groupée
    Views/              confirmation, résultat, fiche photo, mise à jour
    Services/           carte WebView2, miniatures, thème, réglages, dialogues, installateur
    Themes/             palette clair/sombre et styles
    Map/                page Leaflet/MapLibre locale + vendor
tests/
  CityFinder.Core.Tests/
  CityFinder.Infrastructure.Tests/
maquette/               référence HTML statique (données synthétiques)
```

## Pipeline

1. L'utilisateur choisit un dossier (sélecteur natif, argument de ligne de commande ou glisser-déposer).
2. `JsonSessionStore.LoadAsync` lit `.cityfinder.json` (ou le repli AppData).
3. `PhotoScanner.ScanAsync` énumère les extensions compatibles, lit les métadonnées GPS, la date, l'orientation, les dimensions et l'appareil.
4. `SessionMatcher.Match` rapproche les entrées par nom courant + taille + date de modification.
5. `InitialStatePolicy.Apply` :
   - session → ville/texte de la session, photo verrouillée et décochée ;
   - suffixe de ville reconnu → ville conservée, suggestion Photon si différente ;
   - sinon ville Photon (ou vide), sélection si GPS et ville disponible.
6. La grille s'affiche immédiatement. Les vignettes se chargent en arrière-plan et Photon complète les villes progressivement.
7. L'utilisateur corrige les villes, ajoute un texte, coche/décoche, filtre ou recherche. **Tout cocher** agit sur la vue courante ; **Ville groupée** et **Texte groupé** appliquent une valeur aux photos sélectionnées ou visibles, avec confirmation avant remplacement. La carte se synchronise avec la vue filtrée.
8. « Renommer » construit un `RenamePlan`, affiche un aperçu, puis exécute les renommages séquentiellement.
9. Après chaque fichier, la session est mise à jour de façon atomique.
10. En cas d'insatisfaction, « Annuler ces renommages » ou la bannière de reprise restaure les noms d'origine.
11. Si la session devient vide, le fichier `.cityfinder.json` est supprimé.

## Noms de fichiers

```text
<base_sans_ville> - <ville>[, <texte>]<extension>
```

Règles :

- `base_sans_ville` provient du nom d'origine, débarrassé d'un suffixe ville reconnu ;
- ville et texte sont normalisés en Unicode, les accents sont conservés ;
- les caractères interdits Windows sont retirés/remplacés, les espaces sont compactés ;
- les noms réservés (`CON`, `PRN`, `NUL`, `COM1`…) sont neutralisés ;
- la longueur du radical est plafonnée ; le manifeste `longPathAware` autorise les chemins longs ;
- l'extension d'origine est conservée telle quelle ;
- les noms cibles sont uniques dans le lot et vis-à-vis des fichiers présents ;
- une collision ajoute ` (2)`, ` (3)`… avant l'extension ;
- un fichier déjà au nom cible n'est pas déplacé.

Le parseur ne reconnaît une ville que si elle appartient au catalogue connu (liste de communes + villes Photon + villes de session). Il accepte le séparateur actuel `", "`, l'ancien `" , "` et l'ancien séparateur espace pour les fichiers déjà nommés. Il utilise le dernier séparateur `" - "` dont le suffixe est reconnu, ce qui autorise les tirets dans le texte.

## Session `.cityfinder.json`

```json
{
  "version": 1,
  "folderPath": "D:\\Photos\\Voyage",
  "createdUtc": "2026-11-26T21:00:00Z",
  "updatedUtc": "2026-11-26T21:04:12Z",
  "entries": [
    {
      "id": "identifiant",
      "originalName": "IMG_0001.jpg",
      "currentName": "IMG_0001 - Annecy, lac.jpg",
      "city": "Annecy",
      "text": "lac",
      "photonCity": null,
      "length": 123456,
      "lastWriteUtc": "2026-08-14T16:20:00Z",
      "takenAt": null,
      "renamedUtc": "2026-11-26T21:04:12Z",
      "batchId": "20261126-220412-abcd"
    }
  ]
}
```

- Écriture dans un fichier temporaire puis remplacement atomique.
- Attribut caché Windows après écriture.
- Repli déterministe dans `%LOCALAPPDATA%\CityFinder\sessions\<hash-dossier>.json` si le dossier photo est en lecture seule.
- Le rapprochement au scan se fait par **nom courant + taille + date de modification** (tolérance 2 s). Un fichier modifié hors application n'est pas considéré comme déjà renommé.
- L'annulation vérifie que le fichier courant existe et que le nom d'origine est libre. Aucun écrasement.
- Les entrées sont restaurées dans l'ordre chronologique pour gérer les chaînes de renommages.
- Le fichier est supprimé lorsqu'il ne reste aucune entrée.

## Carte WebView2

- Page locale servie par `SetVirtualHostNameToFolderMapping` (`cityfinder.local`), aucun serveur externe.
- Leaflet 1.9.4 + Leaflet.markercluster 1.5.3 + MapLibre GL 5.24.1 + leaflet-maplibre-gl 0.1.4.
- Fonds sans clé API, choisis dans le menu **Calque** avec un aperçu local de chaque option :
  - **OpenStreetMap standard par défaut** (raster), légèrement assombri en thème sombre ;
  - plan OpenFreeMap Positron / Fiord (vectoriel, thème clair/sombre) ;
  - satellite Esri World Imagery + libellés Esri ;
  - relief OpenTopoMap.
- L'attribution est mise à jour à chaque changement de fond ; le choix est enregistré dans `settings.json` (`mapBasemap`) et signalé à WPF par `basemapChanged`. Une préférence existante n'est jamais écrasée par le défaut.
- Repères regroupés en clusters numérotés ; clic repère → message `photoFocused` vers WPF ; clic carte photo → `focusPhoto(id)`.
- Un repère isolé est une pastille circulaire de 40 px contenant la miniature de la photo (ou une icône d'appareil photo tant que l'aperçu n'est pas disponible).
- Les vignettes des repères sont demandées à WPF uniquement pour les repères réellement visibles ; les photos regroupées sont demandées au moment du dépliage. Cache local `state.thumbnails`.
- Clic sur un cluster de 12 photos ou moins : dépliage immédiat en éventail (positions calculées pour 48 px d'espacement). Au-delà de 12 : zoom sur les limites ; si le zoom maximal est déjà atteint, dépliage.
- Après dépliage, la carte se recentre légèrement si l'éventail dépasse du panneau, sauf si une popup est ouverte.
- Une mise à jour `setPhotos` compare la coordonnée d'origine (`_preSpiderfyLatlng`) pour ne pas prendre la position temporaire du dépliage pour un déplacement de photo et reconstruire le cluster.
- L'info-bulle d'un repère demande sa vignette à WPF (`requestThumbnail`) ; `setThumbnail(id, dataUrl)` la remplace sans précharger toutes les images et met à jour le repère. Le bouton **Voir la fiche** envoie `showPhotoDetails`.
- `setPhotos(points, fit)` met à jour les repères sans reconstruire la couche quand seul l'état change (préserve une info-bulle ouverte).
- Ajustement automatique aux points au premier affichage et lors du redimensionnement du panneau.
- Le message « fond indisponible » n'est affiché qu'après un échec réel ou un délai sans couche utilisable ; il est masqué dès qu'un fond ou des tuiles sont chargés (évite les faux positifs).
- La carte seule est un composant web ; fichiers, EXIF, Photon, session et renommage restent en C#.
- Les liens d'attribution sont ouverts dans le navigateur par défaut ; la page locale n'est jamais remplacée.

## Géocodage Photon

- Endpoint `https://photon.komoot.io/reverse`, `lang=fr`, `limit=3`.
- La commune est choisie parmi `city`, `town`, `village`, `municipality`, `locality`, puis `name` si l'objet est un lieu.
- Les coordonnées sont arrondies à 5 décimales pour la clé de cache.
- Cache JSON local `%LOCALAPPDATA%\CityFinder\cache\photon-v1.json`.
- Espacement minimum ~1,05 s entre requêtes ; une nouvelle tentative sur 429/5xx.
- Après trois échecs consécutifs, la recherche s'arrête et l'utilisateur peut saisir les villes manuellement.
- Aucune image, aucun chemin et aucun nom de fichier ne sont envoyés.

## Interface WPF

- Fenêtre chrome personnalisée (`WindowChrome`), boutons accessibles, maximize contraint à la zone de travail du moniteur.
- Accueil : dossier, reprise du dernier dossier, explication du parcours, note de confidentialité.
- Résultats : en-tête statistiques, recherche, filtres (Toutes / Avec GPS / Sans ville / Sans GPS / Sans texte additionnel / À traiter / Renommées), bouton Tout cocher/décocher, actions groupées, grille de cartes, panneau carte repliable.
- « Sans ville » cible les photos GPS dont la commune est vide ; « Sans texte additionnel » cible toutes les photos dont le texte est vide. La validation d'une ville ou d'un texte (perte de focus, suggestion, effacement) rafraîchit automatiquement la vue filtrée sans retirer la carte pendant la saisie.
- Carte photo : vignette orientée et découpée aux coins de la carte, ville, suggestion Photon, texte, aperçu du nom cible, état ; double-clic sur la vignette → fiche photo.
- Fiche photo : grande image chargée à la demande, badge GPS, métadonnées (coordonnées, date, appareil, dimensions, pays/région, commune Photon), ville et texte éditables, aperçu du nom cible, inclusion dans le renommage, lien OpenStreetMap.
- Édition groupée : ville ou texte, portée sélectionnées/visibles, aperçu du nom cible, avertissement et confirmation avant remplacement d'une valeur existante.
- Bannière de reprise : nombre de fichiers déjà renommés et accès à l'annulation.
- Dialogues : fenêtres WPF sans barre de titre système, avec barre maison et icône applicative.
  - confirmation avec aperçu des noms ;
  - résultat avec échecs/avertissements et annulation ;
  - confirmation d'annulation et messages d'information/erreur dans le même thème clair/sombre.
- Aucun `MessageBox` système n'est utilisé dans le fonctionnement normal ; un secours technique reste possible si les ressources WPF ne peuvent pas se charger.
- Recherche et édition restent utilisables pendant le géocodage ; les mises à jour de statistiques sont regroupées.
- Thèmes clair/sombre persistés dans `%LOCALAPPDATA%\CityFinder\settings.json`.
- Raccourcis : `Ctrl+O` (dossier), `F5` (actualiser).

## Mises à jour

- `UpdateService` interroge `https://api.github.com/repos/brunothesatellite/city-finder/releases/latest` avec un User-Agent dédié.
- La version est comparée à celle de l'assembly ; les tags `v0.2.0` et les suffixes pré-release sont acceptés.
- L'asset `*-win-x64.zip` est retenu ; l'empreinte SHA-256 publiée dans la note de release est obligatoire.
- Le téléchargement se fait en streaming avec progression, puis l'archive est vérifiée avant extraction. Une empreinte incorrecte supprime le fichier et refuse l'installation.
- Le ZIP est extrait dans `%LOCALAPPDATA%\CityFinder\updates\staging\<version>` ; `CityFinder.exe` est localisé même si l'archive contient un dossier racine.
- L'installation génère un script PowerShell dans `%LOCALAPPDATA%\CityFinder\updates`, attend la fermeture du processus, miroir le dossier avec `robocopy /MIR`, puis redémarre l'exécutable. Le script et les journaux restent locaux.
- Vérification automatique au démarrage au plus une fois par 24 h (`settings.json`, `lastUpdateCheckUtc`), silencieuse en cas d'échec. Un bouton manuel dans la barre de titre affiche l'état (« à jour » ou erreur).
- « Plus tard » mémorise `skippedUpdateTag` pour ne pas réafficher la même release à chaque démarrage ; une vérification manuelle la propose à nouveau.
- Le dossier d'installation doit être accessible en écriture ; sinon l'application l'indique sans tenter de remplacement.

## Tests

- **Core** : assainissement, parseur de ville, planification, collisions, sélection initiale, session, correspondance.
- **Infrastructure** : aller-retour `.cityfinder.json`, repli, suppression du fichier vide, renommage réel sur dossier temporaire, collision tardive, refus d'écrasement à l'annulation.
- **Mises à jour** : parsing de release, sélection de l'asset Windows, comparaison de versions, téléchargement et vérification SHA-256, refus d'une empreinte incorrecte, extraction imbriquée.
- Vérifications manuelles : parcours complet avec renommage, reprise après redémarrage, annulation, suggestion Photon, texte `", "`, carte claire/sombre, redimensionnement.

## Distribution

- Publication `win-x64` autonome, sans installation.
- Inclure `LICENSE`, `THIRD-PARTY-NOTICES.md` et le dossier `Map/` à côté de l'exécutable.
- Le runtime WebView2 Evergreen reste un prérequis système ; l'application détecte son absence et propose le lien officiel.
- Les fichiers de données personnelles et les vignettes ne font pas partie du dépôt.

## Limites et évolutions

- Le géocodage initial est volontairement limité ; un cache partagé et un fournisseur configurable pourraient être ajoutés.
- HEIC/WebP dépendent des codecs Windows.
- L'interface est en français uniquement.
- Une liste plus complète des communes françaises pourrait améliorer la reconnaissance des noms externes.
- Les tests E2E UI pourraient être automatisés avec WinAppDriver/Appium.

## Dépendances et licences

Voir [`THIRD-PARTY-NOTICES.md`](../THIRD-PARTY-NOTICES.md). Toutes les dépendances
sont sous MIT, Apache-2.0, BSD-2/3, ISC ou licence BSD du SDK Adobe XMP.
Les fonds cartographiques proviennent d'OpenFreeMap, OpenStreetMap contributors,
Esri (World Imagery / World Boundaries and Places) et OpenTopoMap ; aucune tuile
Google n'est utilisée.
