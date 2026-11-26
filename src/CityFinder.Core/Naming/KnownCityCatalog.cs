namespace CityFinder.Core.Naming;

/// <summary>
/// Set of city names accepted when reading a city already present in a file
/// name. It includes a small built-in list, the communes returned by Photon for
/// the current folder, and the cities recorded in the session. Unknown text is
/// never treated as a city.
/// </summary>
public sealed class KnownCityCatalog
{
    private readonly Dictionary<string, string> _byKey;

    private KnownCityCatalog(Dictionary<string, string> byKey, IReadOnlyList<string> cities)
    {
        _byKey = byKey;
        Cities = cities;
    }

    public IReadOnlyList<string> Cities { get; }

    public static KnownCityCatalog Create(IEnumerable<string?> cities)
    {
        var byKey = new Dictionary<string, string>(StringComparer.Ordinal);
        var ordered = new List<string>();

        foreach (var city in cities)
        {
            var trimmed = city?.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                continue;
            }

            var key = NameText.Fold(trimmed);
            if (key.Length == 0 || !byKey.TryAdd(key, trimmed))
            {
                continue;
            }

            ordered.Add(trimmed);
        }

        return new KnownCityCatalog(byKey, ordered);
    }

    public bool TryGet(string? candidate, out string canonical)
    {
        canonical = string.Empty;
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        return _byKey.TryGetValue(NameText.Fold(candidate), out canonical!);
    }

    public bool Contains(string? candidate) => TryGet(candidate, out _);
}

/// <summary>
/// A compact list of well-known French communes used only to recognise an
/// existing city suffix before Photon answers. It is not a geocoding database.
/// </summary>
public static class KnownCities
{
    public static IReadOnlyList<string> France { get; } = new[]
    {
        "Paris", "Marseille", "Lyon", "Toulouse", "Nice", "Nantes", "Montpellier",
        "Strasbourg", "Bordeaux", "Lille", "Rennes", "Reims", "Toulon", "Saint-Étienne",
        "Le Havre", "Grenoble", "Dijon", "Angers", "Nîmes", "Villeurbanne", "Clermont-Ferrand",
        "Le Mans", "Aix-en-Provence", "Brest", "Tours", "Amiens", "Limoges", "Annecy",
        "Perpignan", "Besançon", "Metz", "Orléans", "Rouen", "Mulhouse", "Caen", "Nancy",
        "Argenteuil", "Montreuil", "Roubaix", "Tourcoing", "Nanterre", "Avignon", "Vitry-sur-Seine",
        "Créteil", "Dunkerque", "Poitiers", "Asnières-sur-Seine", "Courbevoie", "Versailles",
        "Colombes", "Aulnay-sous-Bois", "Saint-Pierre-des-Corps", "Rueil-Malmaison", "Aubervilliers",
        "Champigny-sur-Marne", "Antibes", "La Rochelle", "Cannes", "Calais", "Béziers",
        "Colmar", "Bourges", "Quimper", "Valence", "Ajaccio", "Bastia", "Bayonne", "Pau",
        "Tarbes", "Albi", "Carcassonne", "Narbonne", "Montauban", "Agen", "Périgueux",
        "Brive-la-Gaillarde", "Limoges", "Châteauroux", "Blois", "Bourges", "Nevers",
        "Auxerre", "Troyes", "Châlons-en-Champagne", "Charleville-Mézières", "Épinal",
        "Belfort", "Lorient", "Vannes", "Saint-Brieuc", "Laval", "Alençon", "Évreux",
        "Chartres", "Dreux", "Rambouillet", "Saint-Germain-en-Laye", "Boulogne-Billancourt",
        "Suresnes", "Levallois-Perret", "Neuilly-sur-Seine", "Clichy", "Saint-Denis",
        "Montrouge", "Charenton-le-Pont", "Vincennes", "Fontainebleau", "Meaux",
        "Melun", "Évry-Courcouronnes", "Pontoise", "Cergy", "Mantes-la-Jolie",
        "La Défense", "Sceaux", "Antony", "Montgeron", "Draveil", "Vigneux-sur-Seine",
        "Falaise", "Lisieux", "Bayeux", "Cherbourg-en-Cotentin", "Saint-Lô", "Coutances",
        "Granville", "Avranches", "Vire", "Argentan", "Flers", "Deauville", "Trouville-sur-Mer",
        "Honfleur", "Cabourg", "Ouistreham", "Courseulles-sur-Mer", "Arromanches-les-Bains",
        "Bagnoles-de-l'Orne", "Domfront", "Mortagne-au-Perche", "Nogent-le-Rotrou",
        "Chamonix-Mont-Blanc", "Megève", "Albertville", "Chambéry", "Aix-les-Bains",
        "Voiron", "Vienne", "Bourg-en-Bresse", "Oyonnax", "Roanne", "Saint-Chamond",
        "Montbrison", "Thonon-les-Bains", "Évian-les-Bains", "Annecy-le-Vieux",
        "Biarritz", "Saint-Jean-de-Luz", "Dax", "Mont-de-Marsan", "Arcachon",
        "La Teste-de-Buch", "Royan", "Saintes", "Cognac", "Angoulême", "Niort",
        "Fontenay-le-Comte", "Les Sables-d'Olonne", "Cholet", "Saumur", "Vendôme",
        "Chinon", "Loches", "Joué-lès-Tours", "Saint-Malo", "Fougères", "Vitré",
        "Redon", "Ploërmel", "Pontivy", "Concarneau", "Douarnenez", "Morlaix",
        "Quiberon", "Carnac", "La Baule-Escoublac", "Pornic", "Noirmoutier-en-l'Île",
        "Île de Ré", "Rochefort", "La Tremblade", "Royan", "Sarlat-la-Canéda",
        "Bergerac", "Libourne", "Villeneuve-sur-Lot", "Cahors", "Rodez", "Millau",
        "Mende", "Aurillac", "Le Puy-en-Velay", "Saint-Flour", "Vichy", "Moulins",
        "Montluçon", "Commentry", "Étampes", "Dourdan", "Rambouillet", "Houdan",
        "Marly-le-Roi", "Poissy", "Maisons-Laffitte", "Chatou", "Le Vésinet",
        "Lillebonne", "Fécamp", "Étretat", "Yvetot", "Barentin", "Eu", "Dieppe",
        "Le Tréport", "Berck", "Boulogne-sur-Mer", "Arras", "Lens", "Liévin",
        "Béthune", "Douai", "Valenciennes", "Cambrai", "Maubeuge", "Avesnes-sur-Helpe",
        "Saint-Quentin", "Laon", "Soissons", "Beauvais", "Compiègne", "Senlis",
        "Chantilly", "Creil", "Noyon", "Abbeville", "Péronne", "Montbéliard",
    }.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    public static KnownCityCatalog CreateCatalog(IEnumerable<string?> extraCities) =>
        KnownCityCatalog.Create(France.Concat(extraCities));
}
