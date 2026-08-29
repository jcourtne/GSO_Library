namespace GSO_Library.Configuration;

public class SeasonZipCacheOptions
{
    /// <summary>Cached season-share zips older than this are eligible for cleanup.</summary>
    public int RetentionDays { get; set; } = 30;

    /// <summary>How often the background sweep runs.</summary>
    public int SweepIntervalHours { get; set; } = 6;
}
