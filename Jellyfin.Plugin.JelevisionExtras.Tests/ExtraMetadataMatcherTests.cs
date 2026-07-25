using Jellyfin.Plugin.JelevisionExtras.Matching;
using MediaBrowser.Model.Entities;
using Xunit;

namespace Jellyfin.Plugin.JelevisionExtras.Tests;

public sealed class ExtraMetadataMatcherTests
{
    private readonly ExtraMetadataMatcher _matcher = new();

    [Fact]
    public void FinalDestinationRuntimeSetProducesConfidentMetadata()
    {
        var local = new[]
        {
            Local(1, "Final Destination_t01.mkv", 495.072),
            Local(2, "Final Destination_t02.mkv", 143.176),
            Local(3, "Final Destination_t03.mkv", 303.970),
            Local(4, "Final Destination_t04.mkv", 805.024),
            Local(5, "Final Destination_t05.mkv", 172.064),
            Local(6, "Final Destination_t06.mkv", 1180.992)
        };
        var candidate = Candidate(
            Extra(1, "Final Destination_t01.mkv", 495, "Play All", "DeletedScene"),
            Extra(2, "Final Destination_t02.mkv", 143, "Theatrical Trailer", "Trailer"),
            Extra(8, "Final Destination_t08.mkv", 303, "Alternate Ending", "DeletedScene"),
            Extra(9, "Final Destination_t09.mkv", 19, "Pregnancy Test", "DeletedScene"),
            Extra(10, "Final Destination_t10.mkv", 805, "The Perfect Souffle: Testing Final Destination", "Extra"),
            Extra(11, "Final Destination_t11.mkv", 172, "Alternate Love Scene", "DeletedScene"),
            Extra(12, "Final Destination_t12.mkv", 1180, "Premonitions", "Featurette"));

        var result = _matcher.Match(local, [candidate]);

        Assert.True(result.IsConfident, result.Reason);
        Assert.Collection(
            result.Assignments.OrderBy(value => value.Local.FileName),
            value => AssertMatch(value, "Play All", ExtraType.DeletedScene),
            value => AssertMatch(value, "Theatrical Trailer", ExtraType.Trailer),
            value => AssertMatch(value, "Alternate Ending", ExtraType.DeletedScene),
            value => AssertMatch(value, "The Perfect Souffle: Testing Final Destination", ExtraType.Featurette),
            value => AssertMatch(value, "Alternate Love Scene", ExtraType.DeletedScene),
            value => AssertMatch(value, "Premonitions", ExtraType.Featurette));
    }

    [Fact]
    public void EquallyPlausibleDifferentMappingsAreRejected()
    {
        var local = new[]
        {
            Local(1, "Movie_t01.mkv", 120.1),
            Local(2, "Movie_t02.mkv", 300.1)
        };
        var first = Candidate(
            Extra(1, "Movie_t01.mkv", 120, "Trailer", "Trailer"),
            Extra(2, "Movie_t02.mkv", 300, "Making Of", "Extra"));
        var second = Candidate(
            Extra(1, "Movie_t01.mkv", 120, "Deleted Scene", "DeletedScene"),
            Extra(2, "Movie_t02.mkv", 300, "Interview", "Interview"));

        var result = _matcher.Match(local, [first, second]);

        Assert.False(result.IsConfident);
        Assert.Contains("different metadata", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SingleRuntimeWithoutMatchingTitleNumberIsRejected()
    {
        var local = new[]
        {
            Local(1, "renamed-extra.mkv", 143.1)
        };
        var candidate = Candidate(
            Extra(2, "Movie_t02.mkv", 143, "Theatrical Trailer", "Trailer"));

        var result = _matcher.Match(local, [candidate]);

        Assert.False(result.IsConfident);
        Assert.Contains("single runtime", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SingleRuntimeWithMatchingTitleNumberIsAccepted()
    {
        var local = new[]
        {
            Local(1, "Movie_t02.mkv", 143.1)
        };
        var candidate = Candidate(
            Extra(2, "Movie_t02.mkv", 143, "Theatrical Trailer", "Trailer"));

        var result = _matcher.Match(local, [candidate]);

        Assert.True(result.IsConfident, result.Reason);
        AssertMatch(Assert.Single(result.Assignments), "Theatrical Trailer", ExtraType.Trailer);
    }

    [Fact]
    public void RuntimeOutsideAutomaticThresholdIsReportedButNotApplied()
    {
        var local = new[]
        {
            Local(1, "Movie_t01.mkv", 121.5),
            Local(2, "Movie_t02.mkv", 301.5)
        };
        var candidate = Candidate(
            Extra(1, "Movie_t01.mkv", 120, "Trailer", "Trailer"),
            Extra(2, "Movie_t02.mkv", 300, "Featurette", "Featurette"));

        var result = _matcher.Match(local, [candidate]);

        Assert.False(result.IsConfident);
        Assert.Equal(2, result.Assignments.Count);
        Assert.Contains("too large", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Trailer", "Anything", ExtraType.Trailer)]
    [InlineData("DeletedScene", "Anything", ExtraType.DeletedScene)]
    [InlineData("Extra", "A Production Diary", ExtraType.BehindTheScenes)]
    [InlineData("Extra", "Director Interview", ExtraType.Interview)]
    [InlineData("Extra", "Visual Effects Featurette", ExtraType.Featurette)]
    public void ExtraTypesAreMapped(
        string sourceType,
        string title,
        ExtraType expected)
    {
        Assert.Equal(expected, ExtraTypeMapper.Map(sourceType, title));
    }

    private static LocalExtra Local(int id, string fileName, double seconds)
    {
        return new LocalExtra(
            new Guid(id, 0, 0, new byte[8]),
            Path.GetFileNameWithoutExtension(fileName),
            fileName,
            seconds,
            ExtraType.Unknown,
            false);
    }

    private static DiscCandidate Candidate(params DiscExtra[] extras)
    {
        return new DiscCandidate(
            "9532",
            "release",
            "Release",
            "disc",
            "Disc",
            "HASH",
            extras);
    }

    private static DiscExtra Extra(
        int index,
        string comment,
        double seconds,
        string title,
        string type)
    {
        return new DiscExtra(index, comment, null, seconds, title, type);
    }

    private static void AssertMatch(
        ExtraAssignment assignment,
        string title,
        ExtraType type)
    {
        Assert.Equal(title, assignment.Disc.Title);
        Assert.Equal(type, assignment.ExtraType);
    }
}
