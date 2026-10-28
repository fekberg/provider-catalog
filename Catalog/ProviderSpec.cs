namespace ProviderCatalog.Catalog;

/// <summary>One version of a delivery provider's integration spec.</summary>
public sealed record ProviderSpec(
    string ProviderId,
    string Provider,
    int Version,
    string Status,
    DateOnly Published,
    IReadOnlyList<ServiceSpec> Services,
    IReadOnlyList<TrackingStatusSpec> TrackingStatuses)
{
    public bool IsRetired => Status == "retired";
}

public sealed record ServiceSpec(string Code, string Name, int TransitDays, string Description);

/// <summary>A tracking code. Final means the parcel won't move any further after this update.</summary>
public sealed record TrackingStatusSpec(string Code, string Description, bool Final);

public sealed record ProviderSummary(string Id, string Name, IReadOnlyList<VersionSummary> Versions);

public sealed record VersionSummary(int Version, string Status, DateOnly Published);
