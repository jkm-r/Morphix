namespace Morphix.Tests;

/// <summary>Uhr mit fester lokaler Zeit für Tests (ersetzt die Systemuhr).</summary>
public sealed class FesteUhr : TimeProvider
{
    private readonly DateTimeOffset _jetzt;

    /// <summary>Erzeugt die Uhr.</summary>
    /// <param name="jetzt">Die Zeit, die die Uhr dauerhaft anzeigt (als UTC interpretiert, Zeitzone UTC).</param>
    public FesteUhr(DateTime jetzt)
    {
        _jetzt = new DateTimeOffset(DateTime.SpecifyKind(jetzt, DateTimeKind.Utc));
    }

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => _jetzt;

    /// <inheritdoc />
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}
