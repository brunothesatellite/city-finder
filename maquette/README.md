# Maquette CityFinder

Démonstrateur HTML statique de l'expérience CityFinder. **Aucune photo personnelle, aucun jeu de données réel** : les 110 photos sont des illustrations SVG de remplacement et les villes/coordonnées sont synthétiques.

## Ouvrir

Double-cliquer `index.html` dans Chrome ou Edge. Aucun serveur requis.

- `index.html` — accueil
- `index.html#analyse` — analyse
- `index.html#resultats` — résultats
- `index.html#carte` — résultats + carte
- `index.html#renommage` — confirmation
- `index.html#succes` — succès
- `index.html#detail` — fiche photo
- `index.html#reprise` — reprise partielle simulée
- `index.html?theme=dark#resultats` — thème sombre

La carte charge le fond vectoriel OpenFreeMap (style Positron/Fiord) via MapLibre + Leaflet ; seuls les styles et tuiles du viewport sont demandés, sans préchargement. Sans réseau, les repères restent affichés sur un fond quadrillé.

La maquette n'est pas l'application finale : le renommage, la session et les appels Photon y sont simulés. L'application de production est prévue en WPF/XAML, avec une carte WebView2.
