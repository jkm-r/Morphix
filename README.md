# Morphix

Morphix ist eine Windows-Desktopanwendung für Administratoren und Support-Mitarbeitende von
Zeiterfassungs- und Zutrittskontrollsystemen. Sie sammelt Logdateien, Windows-Ereignisse,
Dienststatus und Ergebnisse der Aufgabenplanung an einem Ort, gruppiert Fehler und erzeugt einen
standardisierten Support-Bericht (HTML, als ZIP mit den Logs exportierbar).

Studienportfolio (IU, „Projekt: Software Engineering“). Die verbindlichen Vorgaben stehen in `docs/`.

## Voraussetzungen

- Windows 10/11
- .NET 10 SDK (zum Bauen) bzw. .NET 10 Desktop Runtime (zum Ausführen)

## Bauen, testen, starten

```
dotnet build
dotnet test
dotnet run --project src/Morphix.App
```

## Aufbau

| Projekt | Inhalt |
|---|---|
| `src/Morphix.Core` | Domäne, Schnittstellen und Anwendungsservices (ohne Windows-Bezug) |
| `src/Morphix.Infrastructure` | Windows-Adapter und Demo-Adapter |
| `src/Morphix.App` | WPF-Oberfläche (Views, ViewModels, DI-Start) |
| `tests/Morphix.Tests` | Unit- und Integrationstests |
| `demo/` | Demo-Profil und Demo-Logdateien |
| `docs/` | Projektdokumente und Umsetzungsplan |

Stand: Meilenstein M1 (Grundgerüst). Eine Benutzeranleitung folgt in M8.
