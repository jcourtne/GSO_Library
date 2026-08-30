using System.Collections.Concurrent;
using System.Threading.Channels;

namespace GSO_Library.Services;

/// <summary>A request to pre-generate the "download all" share zip for a season.</summary>
public sealed record SeasonZipWarmupRequest(int SeasonId);

/// <summary>
/// In-memory queue of season-share zip warm-up requests. Producers (the seasons
/// controller) enqueue; <see cref="SeasonZipWarmupBackgroundService"/> is the single
/// consumer.
/// </summary>
public interface ISeasonZipWarmupQueue
{
    /// <summary>
    /// Queue a warm-up for the season. No-op if a warm-up for the same season is already
    /// pending, or if the queue is full (the public download path still generates lazily).
    /// </summary>
    void Enqueue(SeasonZipWarmupRequest request);

    ChannelReader<SeasonZipWarmupRequest> Reader { get; }

    /// <summary>Called by the consumer when it starts processing a request, to clear the dedupe entry.</summary>
    void MarkStarted(int seasonId);
}

public class SeasonZipWarmupQueue(ILogger<SeasonZipWarmupQueue> logger) : ISeasonZipWarmupQueue
{
    private readonly Channel<SeasonZipWarmupRequest> _channel =
        Channel.CreateBounded<SeasonZipWarmupRequest>(new BoundedChannelOptions(100)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropWrite,
        });

    private readonly ConcurrentDictionary<int, byte> _pending = new();

    public ChannelReader<SeasonZipWarmupRequest> Reader => _channel.Reader;

    public void Enqueue(SeasonZipWarmupRequest request)
    {
        if (!_pending.TryAdd(request.SeasonId, 0))
            return; // a warm-up for this season is already queued

        if (!_channel.Writer.TryWrite(request))
        {
            _pending.TryRemove(request.SeasonId, out _);
            logger.LogWarning(
                "Season zip warm-up queue full; dropping warm-up for season {SeasonId}",
                request.SeasonId);
        }
    }

    public void MarkStarted(int seasonId) => _pending.TryRemove(seasonId, out _);
}
