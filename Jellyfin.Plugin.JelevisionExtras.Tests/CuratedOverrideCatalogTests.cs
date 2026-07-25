using Jellyfin.Plugin.JelevisionExtras.Matching;
using Jellyfin.Plugin.JelevisionExtras.Overrides;
using MediaBrowser.Model.Entities;
using Xunit;

namespace Jellyfin.Plugin.JelevisionExtras.Tests;

public sealed class CuratedOverrideCatalogTests
{
    private readonly CuratedOverrideCatalog _catalog = new();

    [Fact]
    public void StephenKingsItMatchesByImdbIdWithoutTmdbId()
    {
        var local = Local(1, 0, 340.298);

        var assignment = Assert.Single(_catalog.Match(null, "tt0099864", [local]));

        Assert.Equal(CuratedOverrideAction.HideTechnical, assignment.Rule.Action);
        Assert.Null(assignment.Rule.Title);
        Assert.Null(assignment.Rule.ExtraType);
        Assert.Equal(0, assignment.DurationDeltaSeconds, 3);
    }

    [Fact]
    public void ObsessionHidesFourTechnicalReelsAndNamesTheFeaturette()
    {
        var local = new[]
        {
            Local(1, 0, 325.325),
            Local(2, 2, 325.325),
            Local(3, 3, 125.375),
            Local(4, 4, 330.580),
            Local(5, 5, 1153.026)
        };

        var assignments = _catalog.Match("1339713", "tt37287335", local);

        Assert.Equal(5, assignments.Count);
        Assert.Equal(
            4,
            assignments.Count(value =>
                value.Rule.Action == CuratedOverrideAction.HideTechnical));
        var featurette = Assert.Single(
            assignments,
            value => value.Rule.Title is not null);
        Assert.Equal("Obsession Unleashed", featurette.Rule.Title);
        Assert.Equal(ExtraType.Featurette, featurette.Rule.ExtraType);
    }

    [Fact]
    public void DumbAndDumberMapsEveryExtractedSupplement()
    {
        var local = new[]
        {
            Local(1, 0, 340.340),
            Local(2, 2, 2035.433),
            Local(3, 3, 465.598),
            Local(4, 4, 1115.147),
            Local(5, 5, 129.152),
            Local(6, 6, 206.239),
            Local(7, 7, 122.155),
            Local(8, 8, 251.284),
            Local(9, 9, 324.357),
            Local(10, 10, 223.256),
            Local(11, 11, 283.316),
            Local(12, 12, 179.212),
            Local(13, 13, 140.173),
            Local(14, 14, 122.155)
        };

        var assignments = _catalog
            .Match("8467", "tt0109686", local)
            .OrderBy(value => value.Rule.TitleOrdinal)
            .ToList();

        Assert.Equal(14, assignments.Count);
        Assert.Equal(CuratedOverrideAction.HideTechnical, assignments[0].Rule.Action);
        Assert.Equal(
            new[]
            {
                "Additional Scenes — Play All",
                "Deliriously Dumb Moments — Play All",
                "Still Dumb After All These Years",
                "Theatrical Trailer",
                "The Box",
                "Hey, Get Me a Cracker!",
                "Somebody Else's Money",
                "Deleted Short Scenes and Shot Montage with Jeff Daniels",
                "Lloyd and Seabass",
                "R.I.P. Petey",
                "Alternate Ending #1",
                "The Toilet Scene",
                "Big Fire Stunt"
            },
            assignments.Skip(1).Select(value => value.Rule.Title));
        Assert.Equal(ExtraType.Trailer, assignments[4].Rule.ExtraType);
    }

    [Fact]
    public void StrongParentOrdinalAndRuntimeMustAllMatch()
    {
        var correct = Local(1, 0, 340.298);
        var wrongOrdinal = Local(2, 1, 340.298);
        var wrongRuntime = Local(3, 0, 344.0);

        Assert.Empty(_catalog.Match(null, "tt0000000", [correct]));
        Assert.Empty(_catalog.Match(null, "tt0099864", [wrongOrdinal]));
        Assert.Empty(_catalog.Match(null, "tt0099864", [wrongRuntime]));
    }

    [Fact]
    public void CuratedRulesHaveUniqueIdsAndValidActions()
    {
        Assert.Equal(
            _catalog.Rules.Count,
            _catalog.Rules.Select(value => value.RuleId).Distinct().Count());
        Assert.All(
            _catalog.Rules,
            rule =>
            {
                if (rule.Action == CuratedOverrideAction.HideTechnical)
                {
                    Assert.Null(rule.Title);
                    Assert.Null(rule.ExtraType);
                }
                else
                {
                    Assert.False(string.IsNullOrWhiteSpace(rule.Title));
                    Assert.NotNull(rule.ExtraType);
                }
            });
    }

    private static LocalExtra Local(int id, int ordinal, double seconds)
    {
        var fileName = $"title_t{ordinal:00}.mkv";
        return new LocalExtra(
            new Guid(id, 0, 0, new byte[8]),
            Path.GetFileNameWithoutExtension(fileName),
            fileName,
            seconds,
            ExtraType.Unknown,
            false);
    }
}
