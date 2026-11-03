namespace Evatr.Net;

/// <summary>
/// Daily time window in which a service is called. Neither BZSt nor VIES documents service hours
/// (eVatR gateway errors were observed around 02:00 CET), so this is configuration, not a known
/// fact. A window whose <see cref="End"/> is before its <see cref="Start"/> spans midnight.
/// </summary>
public sealed record ServiceWindow
{
    public required TimeOnly Start { get; init; }

    /// <summary>Exclusive end of the window.</summary>
    public required TimeOnly End { get; init; }

    /// <summary>Time zone the window is defined in. Defaults to Europe/Berlin.</summary>
    public TimeZoneInfo TimeZone { get; init; } = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    public bool IsOpen(DateTimeOffset now)
    {
        if (Start == End) return true;
        var local = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZone).DateTime);
        return Start < End ? local >= Start && local < End : local >= Start || local < End;
    }

    /// <summary>The next moment the window opens (or <paramref name="now"/> if it is open).</summary>
    public DateTimeOffset NextOpening(DateTimeOffset now)
    {
        if (IsOpen(now)) return now;
        var local = TimeZoneInfo.ConvertTime(now, TimeZone);
        var date = local.Date;
        if (TimeOnly.FromDateTime(local.DateTime) >= Start) date = date.AddDays(1);
        var opening = date.Add(Start.ToTimeSpan());
        return new DateTimeOffset(opening, TimeZone.GetUtcOffset(opening));
    }
}
