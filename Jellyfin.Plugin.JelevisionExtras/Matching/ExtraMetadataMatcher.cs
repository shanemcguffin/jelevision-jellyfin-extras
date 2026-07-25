using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.JelevisionExtras.Matching;

/// <summary>
/// Matches the complete set of local extras against candidate physical discs.
/// </summary>
public sealed partial class ExtraMetadataMatcher
{
    private const double MatchToleranceSeconds = 2.0;
    private const double AutomaticDeltaSeconds = 1.25;
    private const double CompetingPlanWindowSeconds = 1.5;
    private const double DummyCost = 1_000_000_000;
    private const double ForbiddenCost = 1_000_000_000_000;

    /// <summary>
    /// Finds a unique, high-confidence physical-disc match.
    /// </summary>
    /// <param name="localExtras">Local extras belonging to one movie.</param>
    /// <param name="candidates">Physical-disc candidates for that movie.</param>
    /// <returns>A match result.</returns>
    public MovieMatchResult Match(
        IReadOnlyList<LocalExtra> localExtras,
        IReadOnlyList<DiscCandidate> candidates)
    {
        if (localExtras.Count == 0)
        {
            return NoMatch("The movie has no eligible local extras.");
        }

        if (localExtras.Any(extra => extra.DurationSeconds <= 0))
        {
            return NoMatch("At least one local extra has no runtime.");
        }

        var plans = candidates
            .Select(candidate => BuildPlan(localExtras, candidate))
            .Where(plan => plan.Assignments.Count > 0)
            .OrderByDescending(plan => plan.Assignments.Count)
            .ThenBy(plan => plan.TotalDeltaSeconds)
            .ToList();

        if (plans.Count == 0)
        {
            return NoMatch("No TheDiscDb title runtimes matched.");
        }

        var best = plans[0];
        if (best.Assignments.Count != localExtras.Count)
        {
            return new MovieMatchResult(
                false,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Only {best.Assignments.Count} of {localExtras.Count} local extras matched one disc."),
                best.Candidate,
                best.Assignments);
        }

        if (best.Assignments.Any(assignment =>
                assignment.DurationDeltaSeconds > AutomaticDeltaSeconds))
        {
            return new MovieMatchResult(
                false,
                "A complete match was found, but at least one runtime difference was too large for automatic application.",
                best.Candidate,
                best.Assignments);
        }

        var contenders = plans
            .Where(plan =>
                plan.Assignments.Count == best.Assignments.Count
                && plan.TotalDeltaSeconds
                    <= best.TotalDeltaSeconds + CompetingPlanWindowSeconds)
            .ToList();

        var signatures = contenders
            .Select(BuildMappingSignature)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (signatures.Count > 1)
        {
            return new MovieMatchResult(
                false,
                "Multiple equally plausible disc layouts produced different metadata.",
                best.Candidate,
                best.Assignments);
        }

        if (localExtras.Count == 1
            && !best.Assignments[0].Disc.CommentOrdinalMatches(
                best.Assignments[0].Local.FileName))
        {
            return new MovieMatchResult(
                false,
                "A single runtime match is not enough without a matching MakeMKV title number.",
                best.Candidate,
                best.Assignments);
        }

        return new MovieMatchResult(
            true,
            string.Create(
                CultureInfo.InvariantCulture,
                $"Matched all {localExtras.Count} extras with a maximum runtime difference of {best.Assignments.Max(a => a.DurationDeltaSeconds):0.000}s."),
            best.Candidate,
            best.Assignments);
    }

    private static CandidatePlan BuildPlan(
        IReadOnlyList<LocalExtra> localExtras,
        DiscCandidate candidate)
    {
        var discExtras = candidate.Extras
            .Where(extra => ExtraTypeMapper.Map(extra.DiscDbType, extra.Title).HasValue)
            .ToList();

        if (discExtras.Count == 0)
        {
            return new CandidatePlan(candidate, [], 0);
        }

        var columnCount = Math.Max(
            localExtras.Count,
            discExtras.Count + localExtras.Count);
        var costs = new double[localExtras.Count, columnCount];

        for (var row = 0; row < localExtras.Count; row++)
        {
            for (var column = 0; column < columnCount; column++)
            {
                if (column >= discExtras.Count)
                {
                    costs[row, column] = DummyCost;
                    continue;
                }

                var delta = Math.Abs(
                    localExtras[row].DurationSeconds - discExtras[column].DurationSeconds);
                if (delta > MatchToleranceSeconds)
                {
                    costs[row, column] = ForbiddenCost;
                    continue;
                }

                var ordinalPenalty = discExtras[column].CommentOrdinalMatches(
                    localExtras[row].FileName)
                    ? 0
                    : 0.0001;
                costs[row, column] = delta + ordinalPenalty;
            }
        }

        var assignment = SolveMinimumCostAssignment(costs);
        var matches = new List<ExtraAssignment>();
        for (var row = 0; row < localExtras.Count; row++)
        {
            var column = assignment[row];
            if (column < 0 || column >= discExtras.Count)
            {
                continue;
            }

            var delta = Math.Abs(
                localExtras[row].DurationSeconds - discExtras[column].DurationSeconds);
            if (delta > MatchToleranceSeconds)
            {
                continue;
            }

            var mappedType = ExtraTypeMapper.Map(
                discExtras[column].DiscDbType,
                discExtras[column].Title);
            if (!mappedType.HasValue)
            {
                continue;
            }

            matches.Add(new ExtraAssignment(
                localExtras[row],
                discExtras[column],
                mappedType.Value,
                delta));
        }

        return new CandidatePlan(
            candidate,
            matches,
            matches.Sum(match => match.DurationDeltaSeconds));
    }

    private static string BuildMappingSignature(CandidatePlan plan)
    {
        var builder = new StringBuilder();
        foreach (var assignment in plan.Assignments.OrderBy(value => value.Local.Id))
        {
            builder.Append(assignment.Local.Id.ToString("N", CultureInfo.InvariantCulture));
            builder.Append('=');
            builder.Append(assignment.ExtraType);
            builder.Append(':');
            builder.Append(NormalizeTitle(assignment.Disc.Title));
            builder.Append(';');
        }

        return builder.ToString();
    }

    private static string NormalizeTitle(string title)
    {
        return WhitespaceRegex().Replace(title.Trim(), " ").ToUpperInvariant();
    }

    private static MovieMatchResult NoMatch(string reason)
    {
        return new MovieMatchResult(false, reason, null, []);
    }

    // Hungarian algorithm for rectangular minimum-cost assignment where columns >= rows.
    private static int[] SolveMinimumCostAssignment(double[,] costs)
    {
        var rowCount = costs.GetLength(0);
        var columnCount = costs.GetLength(1);
        var rowPotentials = new double[rowCount + 1];
        var columnPotentials = new double[columnCount + 1];
        var matchedRowByColumn = new int[columnCount + 1];
        var previousColumn = new int[columnCount + 1];

        for (var row = 1; row <= rowCount; row++)
        {
            matchedRowByColumn[0] = row;
            var currentColumn = 0;
            var minimumValues = Enumerable.Repeat(double.PositiveInfinity, columnCount + 1).ToArray();
            var used = new bool[columnCount + 1];

            do
            {
                used[currentColumn] = true;
                var currentRow = matchedRowByColumn[currentColumn];
                var delta = double.PositiveInfinity;
                var nextColumn = 0;

                for (var column = 1; column <= columnCount; column++)
                {
                    if (used[column])
                    {
                        continue;
                    }

                    var current = costs[currentRow - 1, column - 1]
                        - rowPotentials[currentRow]
                        - columnPotentials[column];
                    if (current < minimumValues[column])
                    {
                        minimumValues[column] = current;
                        previousColumn[column] = currentColumn;
                    }

                    if (minimumValues[column] < delta)
                    {
                        delta = minimumValues[column];
                        nextColumn = column;
                    }
                }

                for (var column = 0; column <= columnCount; column++)
                {
                    if (used[column])
                    {
                        rowPotentials[matchedRowByColumn[column]] += delta;
                        columnPotentials[column] -= delta;
                    }
                    else
                    {
                        minimumValues[column] -= delta;
                    }
                }

                currentColumn = nextColumn;
            }
            while (matchedRowByColumn[currentColumn] != 0);

            do
            {
                var nextColumn = previousColumn[currentColumn];
                matchedRowByColumn[currentColumn] = matchedRowByColumn[nextColumn];
                currentColumn = nextColumn;
            }
            while (currentColumn != 0);
        }

        var result = Enumerable.Repeat(-1, rowCount).ToArray();
        for (var column = 1; column <= columnCount; column++)
        {
            var row = matchedRowByColumn[column];
            if (row > 0)
            {
                result[row - 1] = column - 1;
            }
        }

        return result;
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();

    private sealed record CandidatePlan(
        DiscCandidate Candidate,
        IReadOnlyList<ExtraAssignment> Assignments,
        double TotalDeltaSeconds);
}

internal static partial class MatchModelExtensions
{
    /// <summary>
    /// Tests whether a local filename and a disc title share a MakeMKV title number.
    /// </summary>
    /// <param name="discExtra">Disc title.</param>
    /// <param name="localFileName">Local filename.</param>
    /// <returns>True when both contain the same trailing title number.</returns>
    public static bool CommentOrdinalMatches(this DiscExtra discExtra, string localFileName)
    {
        var localMatch = TitleOrdinalRegex().Match(localFileName);
        if (!localMatch.Success)
        {
            return false;
        }

        var localOrdinal = int.Parse(
            localMatch.Groups[1].Value,
            NumberStyles.None,
            CultureInfo.InvariantCulture);
        if (localOrdinal == discExtra.Index)
        {
            return true;
        }

        var commentMatch = TitleOrdinalRegex().Match(discExtra.Comment ?? string.Empty);
        return commentMatch.Success
            && int.TryParse(
                commentMatch.Groups[1].Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var commentOrdinal)
            && commentOrdinal == localOrdinal;
    }

    [GeneratedRegex(@"_t(\d+)(?:\.[^.]+)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TitleOrdinalRegex();
}
