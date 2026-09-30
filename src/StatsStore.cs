using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VoiceRecogniseBot;

/// <summary>
/// Persists message counters to stats.json.
/// </summary>
internal static class StatsStore
{
    private const string DayFormat = "yyyy-MM-dd";
    private const int RetentionDays = 30;

    private static readonly Lock Sync = new();

    public static void RecordMessage() => Update(day => day.Messages++, stats => stats.TotalMessages++);

    public static void RecordTranscription() => Update(day => day.Transcriptions++, stats => stats.TotalTranscriptions++);

    public static void Reset()
    {
        lock (Sync)
        {
            File.Delete(AppPaths.StatsFile);
        }
    }

    /// <summary>
    /// Builds the summary shown by the CLI and the web UI, with one zero-filled entry per day.
    /// </summary>
    public static StatsSummary GetSummary(int days = 14)
    {
        StatsData stats;
        lock (Sync)
        {
            stats = Read();
        }

        var today = DateTime.UtcNow.Date;
        var daily = Enumerable.Range(0, days)
            .Select(offset => today.AddDays(offset - days + 1))
            .Select(date =>
            {
                var counters = stats.Daily.GetValueOrDefault(ToKey(date)) ?? new DayCounters();
                return new DailyStats(ToKey(date), counters.Messages, counters.Transcriptions);
            })
            .ToList();

        return new StatsSummary(
            stats.TotalMessages,
            stats.TotalTranscriptions,
            daily.TakeLast(7).Sum(day => day.Messages),
            daily);
    }

    private static void Update(Action<DayCounters> updateDay, Action<StatsData> updateTotals)
    {
        lock (Sync)
        {
            try
            {
                var stats = Read();
                var today = DateTime.UtcNow.Date;
                var key = ToKey(today);

                if (!stats.Daily.TryGetValue(key, out var day))
                {
                    stats.Daily[key] = day = new DayCounters();
                }

                updateDay(day);
                updateTotals(stats);

                var oldestKept = ToKey(today.AddDays(-RetentionDays));
                foreach (var staleKey in stats.Daily.Keys.Where(k => string.CompareOrdinal(k, oldestKept) < 0).ToList())
                {
                    stats.Daily.Remove(staleKey);
                }

                Directory.CreateDirectory(AppPaths.DataDirectory);
                File.WriteAllText(AppPaths.StatsFile, JsonSerializer.Serialize(stats, ConfigStore.JsonOptions));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Statistics are best effort and must never break message handling.
                AppLog.Logger.Warn(ex, "Could not update {0}", AppPaths.StatsFile);
            }
        }
    }

    private static StatsData Read()
    {
        if (!File.Exists(AppPaths.StatsFile))
        {
            return new StatsData();
        }

        StatsData stats;
        try
        {
            stats = JsonSerializer.Deserialize<StatsData>(File.ReadAllText(AppPaths.StatsFile), ConfigStore.JsonOptions) ?? new StatsData();
        }
        catch (JsonException ex)
        {
            AppLog.Logger.Warn(ex, "Stats file {0} is corrupt; starting from empty counters", AppPaths.StatsFile);
            return new StatsData();
        }

        stats.Daily ??= [];

        // Files written before 2.2 stored one timestamp per message instead of per-day counters.
        if (stats.DailyMessages is { Count: > 0 } legacyDates)
        {
            foreach (var group in legacyDates.GroupBy(date => ToKey(date.Date)))
            {
                stats.Daily.TryAdd(group.Key, new DayCounters { Messages = group.Count() });
            }
        }

        stats.DailyMessages = null;
        return stats;
    }

    private static string ToKey(DateTime date) => date.ToString(DayFormat, CultureInfo.InvariantCulture);
}

internal sealed class StatsData
{
    public int TotalMessages { get; set; }
    public int TotalTranscriptions { get; set; }
    public Dictionary<string, DayCounters> Daily { get; set; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<DateTime>? DailyMessages { get; set; }
}

internal sealed class DayCounters
{
    public int Messages { get; set; }
    public int Transcriptions { get; set; }
}

internal sealed record DailyStats(string Date, int Messages, int Transcriptions);

internal sealed record StatsSummary(
    int TotalMessages,
    int TotalTranscriptions,
    int MessagesPast7Days,
    IReadOnlyList<DailyStats> Daily);
