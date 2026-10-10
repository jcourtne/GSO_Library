namespace GSO_Library.Configuration;

public class SeasonZipCacheOptions
{
    /// <summary>Cached season-share zips older than this are eligible for cleanup.</summary>
    public int RetentionDays { get; set; } = 30;

    /// <summary>How often the background sweep runs.</summary>
    public int SweepIntervalHours { get; set; } = 6;

    /// <summary>
    /// Stored zips with no DB row are deleted by the sweep once older than this. Must comfortably exceed
    /// the time to generate a zip, since the file is saved before its row is written. Floored at 30.
    /// </summary>
    public int OrphanGraceMinutes { get; set; } = 60;
}
