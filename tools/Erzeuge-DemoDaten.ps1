<#
.SYNOPSIS
    Erzeugt die Demo-Logdateien unter demo/logs reproduzierbar (fester Seed).

.DESCRIPTION
    Alle Daten sind frei erfunden (Musterwerk, fiktive Benutzer und private IP-Adressen).
    Die Zeitstempel liegen in einem festen Fenster (26.10.2026 bis 02.11.2026); der DemoLogLeser
    verschiebt sie beim Lesen so, dass der jüngste Eintrag "jetzt" ist.
    Gleicher Seed => byte-identische Dateien. Aufruf: pwsh tools/Erzeuge-DemoDaten.ps1
#>
param(
    [string]$Ausgabe = (Join-Path $PSScriptRoot '..\demo\logs')
)

$ErrorActionPreference = 'Stop'
$rnd = [System.Random]::new(20261026)
$start = [datetime]'2026-10-26T00:00:00'
$tage = 7

function Wahl([object[]]$liste) { $liste[$rnd.Next($liste.Count)] }

function NeueGuid { $b = [byte[]]::new(16); $rnd.NextBytes($b); [guid]::new($b) }

function Zeitpunkte([int]$anzahl) {
    1..$anzahl | ForEach-Object {
        $start.AddSeconds($rnd.NextDouble() * $tage * 86400).AddMilliseconds($rnd.Next(0, 1000))
    }
}

$benutzer = @('MUSTERWERK\anna.beispiel', 'MUSTERWERK\bernd.probe', 'MUSTERWERK\carla.test', 'MUSTERWERK\dieter.demo')
$kurzname = @('abeispiel', 'bprobe', 'ctest', 'ddemo')
$ips = @('192.168.10.23', '192.168.10.41', '192.168.10.57', '10.20.0.8', '10.20.0.15')

function Stacktrace([string[]]$zeilen) { $zeilen | ForEach-Object { "   bei $_" } }

# Jeder Eintrag: Zeit (datetime), Stufe (string), Text (string), optional Zusatz (Fortsetzungszeilen)
function Eintrag($zeit, $stufe, $text, $zusatz = @()) {
    [pscustomobject]@{ Zeit = $zeit; Stufe = $stufe; Text = $text; Zusatz = @($zusatz) }
}

function Schreibe($datei, $eintraege, [scriptblock]$format) {
    $zeilen = [System.Collections.Generic.List[string]]::new()
    foreach ($e in ($eintraege | Sort-Object Zeit)) {
        $zeilen.Add((& $format $e))
        foreach ($z in $e.Zusatz) { $zeilen.Add($z) }
    }
    $pfad = Join-Path $Ausgabe $datei
    [System.IO.File]::WriteAllText($pfad, (($zeilen -join "`n") + "`n"), [System.Text.UTF8Encoding]::new($false))
    "{0}: {1} Zeilen" -f $datei, $zeilen.Count
}

New-Item -ItemType Directory -Force $Ausgabe | Out-Null
$ci = [cultureinfo]::InvariantCulture

# ---------------------------------------------------------------- Applikationsserver
$e = [System.Collections.Generic.List[object]]::new()
foreach ($z in Zeitpunkte 880) {
    $w = $rnd.Next(100)
    if ($w -lt 38) {
        $e.Add((Eintrag $z 'INFO' ("Anmeldung erfolgreich: {0} von {1}" -f (Wahl $benutzer), (Wahl $ips))))
    } elseif ($w -lt 62) {
        $e.Add((Eintrag $z 'INFO' ("Zeitbuchung verarbeitet für Mitarbeiter {0}, Buchung {1}" -f $rnd.Next(1000, 1400), $rnd.Next(100000, 999999))))
    } elseif ($w -lt 78) {
        $e.Add((Eintrag $z 'DEBUG' ("Sitzung gestartet user={0} Sitzungs-ID {1}" -f (Wahl $kurzname), (NeueGuid))))
    } elseif ($w -lt 90) {
        $e.Add((Eintrag $z 'INFO' ("Stammdaten geändert, Benutzer: {0}" -f (Wahl $kurzname))))
    } elseif ($w -lt 95) {
        $e.Add((Eintrag $z 'WARN' ("Lizenzwarnung: Die Lizenz läuft in {0} Tagen ab" -f $rnd.Next(10, 15))))
    } elseif ($w -lt 99) {
        $e.Add((Eintrag $z 'WARN' ("Antwortzeit der Datenbank erhöht: {0} ms" -f $rnd.Next(800, 3000))))
    } else {
        $e.Add((Eintrag $z 'ERROR' 'Datenbankverbindung verloren' (@(Stacktrace @(
            'ZE.Server.Datenzugriff.Verbinden() in C:\ZE\Server\Datenzugriff.cs:Zeile 142',
            'ZE.Server.Buchungsdienst.Verarbeite() in C:\ZE\Server\Buchungsdienst.cs:Zeile 88')))))
    }
}
# Zusätzliche Verbindungsabbrüche, damit die Fehlergruppe sichtbar groß ist
foreach ($z in Zeitpunkte 6) {
    $e.Add((Eintrag $z 'ERROR' 'Datenbankverbindung verloren' (@(Stacktrace @(
        'ZE.Server.Datenzugriff.Verbinden() in C:\ZE\Server\Datenzugriff.cs:Zeile 142',
        'ZE.Server.Buchungsdienst.Verarbeite() in C:\ZE\Server\Buchungsdienst.cs:Zeile 88')))))
}
# Nächtlicher Lohnexport um 02:00, jede Nacht fehlgeschlagen
foreach ($tag in 0..($tage - 1)) {
    $z = $start.AddDays($tag).AddHours(2).AddSeconds($rnd.Next(0, 40))
    $e.Add((Eintrag $z 'ERROR' 'Export-Job beendet mit Code 0x1' (@(Stacktrace @(
        'ZE.Server.Export.LohnExport.Ausfuehren() in C:\ZE\Server\Export\LohnExport.cs:Zeile 57',
        'ZE.Server.Planung.Aufgabe.Starte() in C:\ZE\Server\Planung\Aufgabe.cs:Zeile 31')) + '   Ursache: Zielverzeichnis D:\Export\Lohn ist nicht beschreibbar')))
}
Schreibe 'ze-applikationsserver.log' $e { param($x) '{0} [{1}] {2}' -f $x.Zeit.ToString('yyyy-MM-dd HH:mm:ss.fff', $ci), $x.Stufe, $x.Text }

# ---------------------------------------------------------------- Terminalkommunikation
$e = [System.Collections.Generic.List[object]]::new()
foreach ($z in Zeitpunkte 900) {
    $t = $rnd.Next(1, 25)
    $w = $rnd.Next(100)
    if ($w -lt 55) {
        $e.Add((Eintrag $z 'INFO' ("Terminal {0} verbunden (Firmware 4.{1}.{2})" -f $t, $rnd.Next(0, 4), $rnd.Next(0, 9))))
    } elseif ($w -lt 78) {
        $e.Add((Eintrag $z 'INFO' ("Heartbeat von Terminal {0} empfangen, Latenz {1} ms" -f $t, $rnd.Next(5, 120))))
    } elseif ($w -lt 84) {
        $e.Add((Eintrag $z 'WARN' ("Terminal {0} antwortet langsam: {1} ms" -f $t, $rnd.Next(900, 4000))))
    } elseif ($w -lt 99) {
        $e.Add((Eintrag $z 'ERROR' ("Timeout nach {0} s bei Terminal {1}" -f (Wahl @(30, 30, 30, 45, 60)), $t)))
    } else {
        $e.Add((Eintrag $z 'FATAL' ("Verbindung zu Terminal {0} ({1}) dauerhaft unterbrochen" -f $t, (Wahl $ips))))
    }
}
Schreibe 'ze-terminalkommunikation.log' $e { param($x) '{0} {1,-5} {2}' -f $x.Zeit.ToString('yyyy-MM-dd HH:mm:ss.fff', $ci), $x.Stufe, $x.Text }

# ---------------------------------------------------------------- Zutrittsdienst
$e = [System.Collections.Generic.List[object]]::new()
foreach ($z in Zeitpunkte 930) {
    $w = $rnd.Next(100)
    if ($w -lt 70) {
        $e.Add((Eintrag $z 'INFO' ("Zutritt gewährt: Ausweis {0} an Tür {1}" -f $rnd.Next(10000, 10400), $rnd.Next(1, 16))))
    } elseif ($w -lt 85) {
        $e.Add((Eintrag $z 'INFO' ("Türstatus geändert: Tür {0} ist {1}" -f $rnd.Next(1, 16), (Wahl @('geschlossen', 'geöffnet')))))
    } elseif ($w -lt 95) {
        $e.Add((Eintrag $z 'WARN' ("Zutritt verweigert: Ausweis {0} an Tür {1} (keine Berechtigung)" -f $rnd.Next(10000, 10400), $rnd.Next(1, 16))))
    } elseif ($w -lt 98) {
        $e.Add((Eintrag $z 'ERROR' ("Kartenleser an Tür {0} antwortet nicht" -f $rnd.Next(1, 16))))
    } else {
        $e.Add((Eintrag $z 'ERROR' 'Datenbankverbindung verloren' (@(Stacktrace @(
            'ZE.Zutritt.Berechtigungen.Laden() in C:\ZE\Zutritt\Berechtigungen.cs:Zeile 64')))))
    }
}
Schreibe 'ze-zutritt.log' $e { param($x) '{0} [{1}] {2}' -f $x.Zeit.ToString('yyyy-MM-dd HH:mm:ss,fff', $ci), $x.Stufe, $x.Text }
