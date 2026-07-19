namespace Weft.Concurrency;

/// <summary>
/// Lifecycle options of the <see cref="DocumentBroker"/>: when to evict idle documents,
/// how many to keep active at once (LRU eviction when exceeded) and how to persist before evicting.
/// Immutable after the broker is constructed.
/// </summary>
public sealed class DocumentBrokerOptions
{
    /// <summary>
    /// Time without activity after which an active document becomes a candidate for eviction. A document is
    /// then reopened from its persisted state (via the <c>loader</c> of <see cref="DocumentBroker.OpenAsync"/>).
    /// </summary>
    public TimeSpan IdleEviction { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Maximum of simultaneously active documents. When exceeded, the least recently used
    /// (LRU) one is evicted to bound memory (SC-006).
    /// </summary>
    public int MaxActiveDocuments { get; init; } = 1024;

    /// <summary>
    /// Hook invoked before releasing an evicted document, with its exported state, to persist it.
    /// The eviction waits for it to finish. It is not invoked when the document is evicted due to an actor
    /// fault (potentially invalid state). <c>null</c> = do not persist (unsaved changes are lost
    /// on eviction).
    /// </summary>
    public Func<string, byte[], CancellationToken, ValueTask>? OnEvicting { get; init; }

    /// <summary>
    /// Cadence of the idle sweep. The broker periodically checks whether there are documents exceeding
    /// <see cref="IdleEviction"/>. By default, a third of <see cref="IdleEviction"/> (bounded to [1s, 60s]).
    /// </summary>
    public TimeSpan? IdleSweepInterval { get; init; }

    internal TimeSpan ResolveSweepInterval()
    {
        if (IdleSweepInterval is { } explicitInterval)
        {
            return explicitInterval;
        }
        TimeSpan third = IdleEviction / 3;
        if (third < TimeSpan.FromSeconds(1))
        {
            return TimeSpan.FromSeconds(1);
        }
        return third > TimeSpan.FromSeconds(60) ? TimeSpan.FromSeconds(60) : third;
    }
}
