using CityFinder.Core.Naming;

namespace CityFinder.Core.Tests;

public class FileNameToolsTests
{
    [Fact]
    public void BuildsNameWithoutText()
    {
        var name = FileNameTools.BuildTargetName("IMG_0001", "Annecy", "", ".jpg");
        Assert.Equal("IMG_0001 - Annecy.jpg", name);
    }

    [Fact]
    public void SeparatesCityAndTextWithCommaSpaces()
    {
        var name = FileNameTools.BuildTargetName("IMG_0001", "Annecy", "été 2023", ".jpg");
        Assert.Equal("IMG_0001 - Annecy , été 2023.jpg", name);
    }

    [Fact]
    public void SanitizesForbiddenCharacters()
    {
        var name = FileNameTools.BuildTargetName("IMG:0001", "Le / Havre", "vue ?", ".jpg");
        Assert.Equal("IMG-0001 - Le Havre , vue.jpg", name);
    }

    [Fact]
    public void KeepsAccentsInOutput()
    {
        var name = FileNameTools.BuildTargetName("IMG_0001", "Aix-en-Provence", "château", ".JPG");
        Assert.Equal("IMG_0001 - Aix-en-Provence , château.JPG", name);
    }

    [Fact]
    public void RemovesCommasFromCityButKeepsThemInText()
    {
        Assert.Equal("Aix Provence", NameText.SanitizeCity("Aix, Provence"));
        Assert.Equal("vue, mer", NameText.SanitizeText("vue, mer"));
    }

    [Fact]
    public void NumbersCollisionsBeforeExtension()
    {
        var name = FileNameTools.BuildTargetName("A", "Paris", "", ".jpg", 2);
        Assert.Equal("A - Paris (2).jpg", name);
    }

    [Fact]
    public void AvoidsReservedDeviceName()
    {
        Assert.Equal("_CON", NameText.SanitizeBaseName("CON"));
    }

    [Fact]
    public void TruncatesLongNameButKeepsCityAndText()
    {
        var name = FileNameTools.BuildTargetName(new string('a', 400), "Annecy", "un texte", ".jpg");
        Assert.True(FileNameTools.StripExtension(name).Length <= FileNameTools.MaxStemLength);
        Assert.EndsWith(" - Annecy , un texte.jpg", name);
    }

    [Fact]
    public void ReturnsEmptyWithoutCity()
    {
        Assert.Equal(string.Empty, FileNameTools.BuildTargetName("IMG_1", " ", "text", ".jpg"));
    }
}
