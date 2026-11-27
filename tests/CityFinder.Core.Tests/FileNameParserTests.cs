using CityFinder.Core.Naming;

namespace CityFinder.Core.Tests;

public class FileNameParserTests
{
    private static readonly KnownCityCatalog Catalog = KnownCityCatalog.Create(new[]
    {
        "Annecy", "Chambéry", "Aix-en-Provence", "Paris",
    });

    [Fact]
    public void ParsesCityOnly()
    {
        var parsed = FileNameParser.Parse("IMG_0001 - Annecy.jpg", Catalog);
        Assert.NotNull(parsed);
        Assert.Equal("IMG_0001", parsed!.BaseName);
        Assert.Equal("Annecy", parsed.City);
        Assert.Equal(string.Empty, parsed.Text);
    }

    [Fact]
    public void ParsesCityAndText()
    {
        var parsed = FileNameParser.Parse("IMG_0001 - Annecy, été 2023.jpg", Catalog);
        Assert.NotNull(parsed);
        Assert.Equal("IMG_0001", parsed!.BaseName);
        Assert.Equal("Annecy", parsed.City);
        Assert.Equal("été 2023", parsed.Text);
    }

    [Fact]
    public void ParsesLegacyCityAndTextSeparator()
    {
        var parsed = FileNameParser.Parse("IMG_0001 - Annecy , été 2023.jpg", Catalog);
        Assert.NotNull(parsed);
        Assert.Equal("IMG_0001", parsed!.BaseName);
        Assert.Equal("Annecy", parsed.City);
        Assert.Equal("été 2023", parsed.Text);
    }

    [Fact]
    public void UsesLastSeparatorAndKeepsDashesInBase()
    {
        var parsed = FileNameParser.Parse("Photo - Paris - Annecy.jpg", Catalog);
        Assert.Equal("Photo - Paris", parsed!.BaseName);
        Assert.Equal("Annecy", parsed.City);
    }

    [Fact]
    public void IgnoresUnknownCity()
    {
        Assert.Null(FileNameParser.Parse("IMG_0001 - Village-Inconnu.jpg", Catalog));
    }

    [Fact]
    public void IgnoresUnknownCityEvenWithText()
    {
        Assert.Null(FileNameParser.Parse("IMG_0001 - Village-Inconnu , lac.jpg", Catalog));
    }

    [Fact]
    public void ComparisonIgnoresCaseAndAccents()
    {
        var parsed = FileNameParser.Parse("IMG_0001 - aix-en-provence.jpg", Catalog);
        Assert.NotNull(parsed);
        Assert.Equal("Aix-en-Provence", parsed!.City);
    }

    [Fact]
    public void AcceptsLegacySpaceSeparatorForKnownCity()
    {
        var parsed = FileNameParser.Parse("IMG_0001 - Annecy vue du château.jpg", Catalog);
        Assert.NotNull(parsed);
        Assert.Equal("Annecy", parsed!.City);
        Assert.Equal("vue du château", parsed.Text);
    }

    [Fact]
    public void KeepsDashesInsideText()
    {
        var parsed = FileNameParser.Parse("IMG_0001 - Annecy , vue - mer.jpg", Catalog);
        Assert.NotNull(parsed);
        Assert.Equal("Annecy", parsed!.City);
        Assert.Equal("vue - mer", parsed.Text);
    }

    [Fact]
    public void KeepsCommasInsideText()
    {
        var parsed = FileNameParser.Parse("IMG_0001 - Annecy , vue, mer.jpg", Catalog);
        Assert.Equal("vue, mer", parsed!.Text);
    }

    [Fact]
    public void RequiresNonEmptyBase()
    {
        Assert.Null(FileNameParser.Parse(" - Annecy.jpg", Catalog));
    }

    [Fact]
    public void ReturnsNullWithoutSeparator()
    {
        Assert.Null(FileNameParser.Parse("Annecy.jpg", Catalog));
    }
}
