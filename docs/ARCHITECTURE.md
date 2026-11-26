# CityFinder — architecture

## Décisions

| Sujet | Décision |
|---|---|
| Interface | **WPF/XAML natif**, styles XAML sur mesure, MVVM. Pas de WPF-UI pour garder la direction visuelle de la maquette. |
| Carte | **WebView2** local hébergeant Leaflet + MapLibre + leaflet-maplibre-gl. Seul composant web de l'application. |
| Runtime | **.NET 10 LTS**, cible `net10.0-windows`, publication `win-x64` autonome. |
| Métadonnées | **MetadataExtractor** (EXIF/XMP), miniatures WPF/WIC. |
| Géocodage | **Photon** via `HttpClient` C#, cache JSON local, limitation à ~1 requête/seconde. |
| Ville existante | **Manifeste + parseur prudent** : une ville n'est reconnue que si elle figure dans la liste des communes connues ou dans les résultats Photon du dossier. Aucune devinette. |
| Conflits | Numérotation automatique ` (2)`, ` (3)`…, aucun écrasement. |
| Session | **`.cityfinder.json`** dans le dossier photo, écriture atomique, attribut caché, repli `%LOCALAPPDATA%` si le dossier est en lecture seule. |
| Annulation | Restauration des noms d'origine enregistrés dans la session, par lot ou pour toute la session. |
| Reprise | Les fichiers déjà renommés sont décochés et verrouillés ; ils ne sont **jamais** renommés à nouveau par défaut. |
| Texte | Séparateur `" , "` entre la ville et le texte optionnel. |
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
  CityFinder.App/
    ViewModels/         MainViewModel, PhotoViewModel, filtres
    Views/              confirmation et résultat de renommage
    Services/           carte WebView2, miniatures, thème, réglages, dialogues
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
7. L'utilisateur corrige les villes, ajoute un texte, coche/décoche, filtre ou recherche. La carte se synchronise avec la vue filtrée.
8. « Renommer » construit un `RenamePlan`, affiche un aperçu, puis exécute les renommages séquentiellement.
9. Après chaque fichier, la session est mise à jour de façon atomique.
10. En cas d'insatisfaction, « Annuler ces renommages » ou la bannière de reprise restaure les noms d'origine.
11. Si la session devient vide, le fichier `.cityfinder.json` est supprimé.

## Noms de fichiers

```text
<base_sans_ville> - <ville>[ , <texte>]<extension>
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

Le parseur ne reconnaît une ville que si elle appartient au catalogue connu (liste de communes + villes Photon + villes de session). Il accepte le séparateur actuel `" , "` et l'ancien séparateur espace pour les fichiers déjà nommés. Il utilise le dernier séparateur `" - "` dont le suffixe est reconnu, ce qui autorise les tirets dans le texte.

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
      "currentName": "IMG_0001 - Annecy , lac.jpg",
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
- Styles OpenFreeMap Positron (clair) / Fiord (sombre), attribution permanente.
- Repères regroupés en clusters ; clic repère → message `photoFocused` vers WPF ; clic carte photo → `focusPhoto(id)`.
- `setPhotos(points, fit)` met à jour les repères sans reconstruire la couche quand seul l'état change (préserve une info-bulle ouverte).
- Ajustement automatique aux points au premier affichage et lors du redimensionnement du panneau.
- Repli : si le style vectoriel échoue, les repères restent utilisables sur un fond quadrillé et un message discret apparaît.
- La carte seule est un composant web ; fichiers, EXIF, Photon, session et renommage restent en C#.

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
- Résultats : en-tête statistiques, recherche, filtres (Toutes / À traiter / Sans GPS / Renommées), grille de cartes, panneau carte repliable.
- Carte photo : vignette orientée, ville, suggestion Photon, texte, aperçu du nom cible, état.
- Bannière de reprise : nombre de fichiers déjà renommés et accès à l'annulation.
- Dialogues : confirmation avec aperçu des noms, résultat avec échecs/avertissements et annulation.
- Recherche et édition restent utilisables pendant le géocodage ; les mises à jour de statistiques sont regroupées.
- Thèmes clair/sombre persistés dans `%LOCALAPPDATA%\CityFinder\settings.json`.
- Raccourcis : `Ctrl+O` (dossier), `F5` (actualiser).

## Tests

- **Core** : assainissement, parseur de ville, planification, collisions, sélection initiale, session, correspondance.
- **Infrastructure** : aller-retour `.cityfinder.json`, repli, suppression du fichier vide, renommage réel sur dossier temporaire, collision tardive, refus d'écrasement à l'annulation.
- Vérifications manuelles : parcours complet avec renommage, reprise après redémarrage, annulation, suggestion Photon, texte `" , "`, carte claire/sombre, redimensionnement.

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
