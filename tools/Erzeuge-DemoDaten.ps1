<#
.SYNOPSIS
    Erzeugt die Demo-Logdateien unter demo/logs reproduzierbar (fester Seed).

.DESCRIPTION
    Alle Daten sind frei erfunden (Musterwerk, fiktive Benutzer und private IP-Adressen) und beschreiben
    ausschließlich den technischen Betrieb eines Systems: Anmeldungen, Verbindungen, Datenbank,
    Ressourcen, Zertifikate, Lizenz und Datensicherung. Fachliche Vorgänge kommen nicht vor.
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

function DbVerlorenStack {
    @(Stacktrace @(
        'Muster.Server.Datenzugriff.Verbinden() in C:\Muster\Server\Datenzugriff.cs:Zeile 142',
        'Muster.Server.Anfragebearbeitung.Verarbeite() in C:\Muster\Server\Anfragebearbeitung.cs:Zeile 88'))
}

New-Item -ItemType Directory -Force $Ausgabe | Out-Null
$ci = [cultureinfo]::InvariantCulture

# ---------------------------------------------------------------- Anwendungsserver
$e = [System.Collections.Generic.List[object]]::new()
foreach ($z in Zeitpunkte 880) {
    $w = $rnd.Next(100)
    if ($w -lt 38) {
        $e.Add((Eintrag $z 'INFO' ("Anmeldung erfolgreich: {0} von {1}" -f (Wahl $benutzer), (Wahl $ips))))
    } elseif ($w -lt 62) {
        $e.Add((Eintrag $z 'INFO' ("Anfrage {0} verarbeitet in {1} ms" -f (NeueGuid), $rnd.Next(5, 400))))
    } elseif ($w -lt 78) {
        $e.Add((Eintrag $z 'DEBUG' ("Sitzung gestartet user={0} Sitzungs-ID {1}" -f (Wahl $kurzname), (NeueGuid))))
    } elseif ($w -lt 90) {
        $e.Add((Eintrag $z 'INFO' ("Konfiguration neu geladen, Benutzer: {0}" -f (Wahl $kurzname))))
    } elseif ($w -lt 95) {
        $e.Add((Eintrag $z 'WARN' ("Lizenzwarnung: Die Lizenz läuft in {0} Tagen ab" -f $rnd.Next(10, 15))))
    } elseif ($w -lt 99) {
        $e.Add((Eintrag $z 'WARN' ("Speicherauslastung hoch: {0} %" -f $rnd.Next(85, 98))))
    } else {
        $e.Add((Eintrag $z 'ERROR' 'Datenbankverbindung verloren' (DbVerlorenStack)))
    }
}
# Zusätzliche Verbindungsabbrüche, damit die Fehlergruppe sichtbar groß ist
foreach ($z in Zeitpunkte 6) {
    $e.Add((Eintrag $z 'ERROR' 'Datenbankverbindung verloren' (DbVerlorenStack)))
}
# Nächtliche Datensicherung um 02:00, jede Nacht fehlgeschlagen
foreach ($tag in 0..($tage - 1)) {
    $z = $start.AddDays($tag).AddHours(2).AddSeconds($rnd.Next(0, 40))
    $e.Add((Eintrag $z 'ERROR' 'Datensicherung beendet mit Code 0x1' (@(Stacktrace @(
        'Muster.Server.Sicherung.Ausfuehren() in C:\Muster\Server\Sicherung\Sicherung.cs:Zeile 57',
        'Muster.Server.Planung.Aufgabe.Starte() in C:\Muster\Server\Planung\Aufgabe.cs:Zeile 31')) + '   Ursache: Zielverzeichnis D:\Sicherung ist nicht beschreibbar')))
}
Schreibe 'muster-anwendungsserver.log' $e { param($x) '{0} [{1}] {2}' -f $x.Zeit.ToString('yyyy-MM-dd HH:mm:ss.fff', $ci), $x.Stufe, $x.Text }

# ---------------------------------------------------------------- Kommunikationsdienst
$e = [System.Collections.Generic.List[object]]::new()
foreach ($z in Zeitpunkte 900) {
    $g = $rnd.Next(1, 25)
    $w = $rnd.Next(100)
    if ($w -lt 55) {
        $e.Add((Eintrag $z 'INFO' ("Verbindung zu Gegenstelle {0} hergestellt (Protokollversion 4.{1}.{2})" -f $g, $rnd.Next(0, 4), $rnd.Next(0, 9))))
    } elseif ($w -lt 78) {
        $e.Add((Eintrag $z 'INFO' ("Statusmeldung von Gegenstelle {0} empfangen, Latenz {1} ms" -f $g, $rnd.Next(5, 120))))
    } elseif ($w -lt 84) {
        $e.Add((Eintrag $z 'WARN' ("Gegenstelle {0} antwortet langsam: {1} ms" -f $g, $rnd.Next(900, 4000))))
    } elseif ($w -lt 99) {
        $e.Add((Eintrag $z 'ERROR' ("Timeout nach {0} s bei Gegenstelle {1}" -f (Wahl @(30, 30, 30, 45, 60)), $g)))
    } else {
        $e.Add((Eintrag $z 'FATAL' ("Verbindung zu Gegenstelle {0} ({1}) dauerhaft unterbrochen" -f $g, (Wahl $ips))))
    }
}
Schreibe 'muster-kommunikation.log' $e { param($x) '{0} {1,-5} {2}' -f $x.Zeit.ToString('yyyy-MM-dd HH:mm:ss.fff', $ci), $x.Stufe, $x.Text }

# ---------------------------------------------------------------- Datenbankzugriff
$e = [System.Collections.Generic.List[object]]::new()
foreach ($z in Zeitpunkte 930) {
    $w = $rnd.Next(100)
    if ($w -lt 60) {
        $e.Add((Eintrag $z 'INFO' ("Abfrage ausgeführt in {0} ms" -f $rnd.Next(2, 300))))
    } elseif ($w -lt 85) {
        $e.Add((Eintrag $z 'INFO' ("Verbindungspool: {0} von 50 Verbindungen belegt" -f $rnd.Next(3, 45))))
    } elseif ($w -lt 93) {
        $e.Add((Eintrag $z 'WARN' ("Langsame Abfrage: {0} ms" -f $rnd.Next(2000, 9000))))
    } elseif ($w -lt 96) {
        $e.Add((Eintrag $z 'WARN' ("Zertifikat läuft in {0} Tagen ab" -f $rnd.Next(5, 30))))
    } elseif ($w -lt 98) {
        $e.Add((Eintrag $z 'ERROR' ("Speicherplatz auf Laufwerk D: kritisch niedrig ({0} % frei)" -f $rnd.Next(1, 5))))
    } else {
        $e.Add((Eintrag $z 'ERROR' 'Datenbankverbindung verloren' (@(Stacktrace @(
            'Muster.Daten.Abfrage.Ausfuehren() in C:\Muster\Daten\Abfrage.cs:Zeile 64')))))
    }
}
Schreibe 'muster-datenbank.log' $e { param($x) '{0} [{1}] {2}' -f $x.Zeit.ToString('yyyy-MM-dd HH:mm:ss,fff', $ci), $x.Stufe, $x.Text }
