namespace Morphix.Core.Schnittstellen;

/// <summary>
/// Wird von einem <see cref="IDienstGateway"/> geworfen, wenn eine Dienstaktion nicht ausgeführt werden konnte
/// (Dienst nicht vorhanden, Zugriff verweigert, Zeitüberschreitung). Die Meldung ist für den Anwender bestimmt.
/// </summary>
public sealed class DienstAktionFehlgeschlagenException : Exception
{
    /// <summary>Erzeugt die Ausnahme mit einer deutschen Meldung.</summary>
    public DienstAktionFehlgeschlagenException(string meldung, Exception? ursache = null)
        : base(meldung, ursache)
    {
    }
}
