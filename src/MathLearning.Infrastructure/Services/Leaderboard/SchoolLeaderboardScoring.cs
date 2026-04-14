using MathLearning.Domain.Entities;

namespace MathLearning.Infrastructure.Services.Leaderboard;

public static class SchoolLeaderboardScoring
{
    public static void UpdateDerivedMetrics(SchoolScoreAggregate row)
    {
        row.AverageXpPerActiveStudent = row.ActiveStudents <= 0
            ? 0m
            : decimal.Round((decimal)row.XpTotal / row.ActiveStudents, 4);

        row.ParticipationRate = row.EligibleStudents <= 0
            ? 0m
            : decimal.Round((decimal)row.ActiveStudents / row.EligibleStudents, 6);

        row.WeightedXp = ComputeWeightedXp(row.XpTotal, row.EligibleStudents);
    }

    /// <summary>
    /// Fair ranking metric: total_xp / sqrt(eligible_student_count).
    /// Normalises school size so a 10-student school with high per-capita XP
    /// can compete against a 500-student school.
    /// </summary>
    public static double ComputeWeightedXp(int xpTotal, int eligibleStudentCount)
        => eligibleStudentCount <= 0 ? 0.0 : xpTotal / Math.Sqrt(eligibleStudentCount);

    public static void RecomputeScoresAndRanks(IList<SchoolScoreAggregate> rows)
    {
        if (rows.Count == 0)
        {
            return;
        }

        foreach (var row in rows)
        {
            UpdateDerivedMetrics(row);
        }

        var maxAverage = rows.Max(x => x.AverageXpPerActiveStudent);
        var maxLogTotal = rows.Max(x => Math.Log(x.XpTotal + 1d));

        foreach (var row in rows)
        {
            var averageComponent = maxAverage <= 0m ? 0m : row.AverageXpPerActiveStudent / maxAverage;
            var participationComponent = row.ParticipationRate;
            var totalComponent = maxLogTotal <= 0d ? 0m : (decimal)(Math.Log(row.XpTotal + 1d) / maxLogTotal);

            row.CompositeScore = decimal.Round(
                0.50m * averageComponent +
                0.30m * participationComponent +
                0.20m * totalComponent,
                6);
        }

        var ordered = rows
            .OrderByDescending(x => x.CompositeScore)
            .ThenByDescending(x => x.AverageXpPerActiveStudent)
            .ThenByDescending(x => x.ParticipationRate)
            .ThenByDescending(x => x.XpTotal)
            .ThenBy(x => x.SchoolId)
            .ToList();

        for (var i = 0; i < ordered.Count; i++)
        {
            ordered[i].Rank = i + 1;
        }
    }
}
