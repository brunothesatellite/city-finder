namespace CityFinder.App.ViewModels;

public enum BulkEditKind
{
    City,
    Text,
}

public enum BulkEditScope
{
    Selected,
    Visible,
}

public sealed record BulkEditResult(BulkEditKind Kind, BulkEditScope Scope, string Value);
