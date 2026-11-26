using CityFinder.Core.Models;
using CityFinder.Core.Naming;

namespace CityFinder.Core.Tests;

public class RenamePlannerTests
{
    private static RenameCandidate Candidate(string name, string city, string text = "", string? baseName = null) =>
        new(name, @"C:\photos\" + name, name, ".jpg",
            baseName ?? Path.GetFileNameWithoutExtension(name), city, text);

    [Fact]
    public void PlansSimpleRename()
    {
        var plan = RenamePlanner.CreatePlan(new[] { Candidate("IMG_0001.jpg", "Annecy") },
            new[] { "IMG_0001.jpg" });

        var item = Assert.Single(plan.Items);
        Assert.Equal(RenameItemStatus.Ready, item.Status);
        Assert.Equal("IMG_0001 - Annecy.jpg", item.TargetName);
        Assert.False(item.Renumbered);
    }

    [Fact]
    public void SkipsFileAlreadyNamed()
    {
        var plan = RenamePlanner.CreatePlan(
            new[] { Candidate("IMG_0001 - Annecy.jpg", "Annecy", baseName: "IMG_0001") },
            new[] { "IMG_0001 - Annecy.jpg" });

        var item = Assert.Single(plan.Items);
        Assert.Equal(RenameItemStatus.AlreadyNamed, item.Status);
        Assert.Equal(0, plan.ReadyCount);
    }

    [Fact]
    public void AddsNumberWhenTargetExists()
    {
        var plan = RenamePlanner.CreatePlan(new[] { Candidate("IMG_0001.jpg", "Annecy") },
            new[] { "IMG_0001.jpg", "IMG_0001 - Annecy.jpg" });

        var item = Assert.Single(plan.Items);
        Assert.Equal("IMG_0001 - Annecy (2).jpg", item.TargetName);
        Assert.True(item.Renumbered);
    }

    [Fact]
    public void NumberingSkipsExistingNumbers()
    {
        var plan = RenamePlanner.CreatePlan(new[] { Candidate("IMG_0001.jpg", "Annecy") },
            new[]
            {
                "IMG_0001.jpg",
                "IMG_0001 - Annecy.jpg",
                "IMG_0001 - Annecy (2).jpg",
            });

        Assert.Equal("IMG_0001 - Annecy (3).jpg", plan.Items[0].TargetName);
    }

    [Fact]
    public void NumbersTwoCandidatesWithSameTarget()
    {
        var plan = RenamePlanner.CreatePlan(
            new[]
            {
                Candidate("A.jpg", "Annecy", baseName: "Photo"),
                Candidate("B.jpg", "Annecy", baseName: "Photo"),
            },
            new[] { "A.jpg", "B.jpg" });

        Assert.Equal("Photo - Annecy.jpg", plan.Items[0].TargetName);
        Assert.Equal("Photo - Annecy (2).jpg", plan.Items[1].TargetName);
    }

    [Fact]
    public void FlagsMissingCity()
    {
        var plan = RenamePlanner.CreatePlan(new[] { Candidate("A.jpg", "  ") }, new[] { "A.jpg" });
        Assert.Equal(RenameItemStatus.MissingCity, plan.Items[0].Status);
        Assert.Equal(1, plan.MissingCityCount);
    }

    [Fact]
    public void BuildsTargetWithText()
    {
        var plan = RenamePlanner.CreatePlan(
            new[] { Candidate("A.jpg", "Annecy", "été 2023", "IMG") },
            new[] { "A.jpg" });

        Assert.Equal("IMG - Annecy , été 2023.jpg", plan.Items[0].TargetName);
    }
}
