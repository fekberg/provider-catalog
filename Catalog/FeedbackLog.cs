using System.Collections.Concurrent;

namespace ProviderCatalog.Catalog;

/// <summary>
/// Feedback teams send about a spec. Kept in memory, so it's cleared when the catalog restarts.
/// </summary>
public sealed class FeedbackLog(TimeProvider time)
{
    private readonly ConcurrentQueue<Feedback> _entries = new();

    public Feedback Add(string providerId, int version, string from, string message)
    {
        var feedback = new Feedback(providerId, version, from, message, time.GetUtcNow());
        _entries.Enqueue(feedback);
        return feedback;
    }

    public IReadOnlyList<Feedback> For(string providerId, int version) =>
        _entries
            .Where(entry => string.Equals(entry.ProviderId, providerId, StringComparison.OrdinalIgnoreCase)
                && entry.Version == version)
            .ToList();
}

public sealed record Feedback(string ProviderId, int Version, string From, string Message, DateTimeOffset ReceivedAt);
