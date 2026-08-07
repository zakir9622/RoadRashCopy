<#
.SYNOPSIS
  Headless Unity driver for Highway Renegade: compile, test, and build.

.DESCRIPTION
  Wraps Unity batch mode and parses the editor log, because Unity exit codes alone
  are not trustworthy - a run can exit 0 with compile errors. Every mode verifies a
  real artifact (log diagnostics, a results XML, an APK on disk).

  NOTE: This file is intentionally pure ASCII. Windows PowerShell 5.1 reads .ps1 as
  ANSI unless a BOM is present, so non-ASCII characters corrupt and break parsing.

.EXAMPLE
  ./Tools/unity.ps1 -Mode compile
  ./Tools/unity.ps1 -Mode test
  ./Tools/unity.ps1 -Mode build -Apk
#>
[CmdletBinding()]
param(
    [ValidateSet('compile', 'test', 'build', 'activate-request', 'activate-apply')]
    [string]$Mode = 'compile',

    [string]$ProjectPath = 'D:\RoadRashCopy',
    [string]$EditorRoot  = 'D:\Unity\Editors',
    [string]$Version     = '6000.0.81f1',

    [switch]$Apk,
    [string]$LicenseFile,
    [int]$TimeoutMinutes = 60
)

$ErrorActionPreference = 'Stop'

function Get-UnityExe {
    $exe = Join-Path $EditorRoot ($Version + '\Editor\Unity.exe')
    if (Test-Path $exe) { return $exe }

    $found = Get-ChildItem $EditorRoot -Directory -ErrorAction SilentlyContinue |
             ForEach-Object { Join-Path $_.FullName 'Editor\Unity.exe' } |
             Where-Object { Test-Path $_ } | Select-Object -First 1
    if ($found) {
        Write-Warning ("Unity " + $Version + " not found; using " + $found)
        return $found
    }
    throw ("No Unity editor found under " + $EditorRoot)
}

function Invoke-Unity {
    param([string[]]$UnityArgs, [string]$LogFile)

    if (Test-Path $LogFile) { Remove-Item $LogFile -Force }
    $exe = Get-UnityExe

    Write-Host ("> " + $exe)
    Write-Host ("  " + ($UnityArgs -join ' '))
    $proc = Start-Process -FilePath $exe -ArgumentList $UnityArgs -NoNewWindow -PassThru

    if (-not $proc.WaitForExit($TimeoutMinutes * 60 * 1000)) {
        $proc.Kill()
        throw ("Unity timed out after " + $TimeoutMinutes + " minutes.")
    }
    return $proc.ExitCode
}

# Unity writes every compiler diagnostic to the editor log. Scraping it is the only
# reliable way to surface errors from a headless run.
function Show-LogDiagnostics {
    param([string]$LogFile, [int]$MaxLines = 80)

    if (-not (Test-Path $LogFile)) {
        Write-Warning ("No log at " + $LogFile)
        return @()
    }

    $lines = Get-Content $LogFile -ErrorAction SilentlyContinue

    $errs = $lines | Where-Object {
        $_ -match 'error CS\d+' -or
        $_ -match 'Compilation failed' -or
        $_ -match 'Error building Player' -or
        $_ -match 'Failed to resolve packages' -or
        $_ -match 'Unable to resolve' -or
        $_ -match 'error:.*asmdef'
    } | Select-Object -Unique

    $warns = $lines | Where-Object { $_ -match 'warning CS\d+' } | Select-Object -Unique

    if ($errs.Count -gt 0) {
        Write-Host ""
        Write-Host ("=== ERRORS (" + $errs.Count + ") ===") -ForegroundColor Red
        $errs | Select-Object -First $MaxLines | ForEach-Object { Write-Host ("  " + $_) -ForegroundColor Red }
    } else {
        Write-Host ""
        Write-Host "No compile errors found in log." -ForegroundColor Green
    }

    if ($warns.Count -gt 0) {
        Write-Host ""
        Write-Host ("=== WARNINGS (" + $warns.Count + ", first 15) ===") -ForegroundColor Yellow
        $warns | Select-Object -First 15 | ForEach-Object { Write-Host ("  " + $_) -ForegroundColor Yellow }
    }

    return $errs
}

$logDir = Join-Path $ProjectPath 'Logs'
New-Item -ItemType Directory -Path $logDir -Force | Out-Null

if ($Mode -eq 'compile') {
    $log = Join-Path $logDir 'compile.log'
    $code = Invoke-Unity @(
        '-quit', '-batchmode', '-nographics',
        '-projectPath', $ProjectPath,
        '-logFile', $log
    ) $log

    $errs = Show-LogDiagnostics $log
    Write-Host ""
    Write-Host ("Unity exit code: " + $code)
    if ($errs.Count -gt 0) { exit 1 }
    Write-Host "COMPILE OK" -ForegroundColor Green
    exit 0
}

if ($Mode -eq 'test') {
    $log = Join-Path $logDir 'test.log'
    $results = Join-Path $logDir 'editmode-results.xml'
    if (Test-Path $results) { Remove-Item $results -Force }

    # No -quit here. It would terminate the editor before the test runner writes
    # results, producing an empty file and a silent false pass.
    $code = Invoke-Unity @(
        '-batchmode', '-nographics',
        '-projectPath', $ProjectPath,
        '-runTests', '-testPlatform', 'EditMode',
        '-testResults', $results,
        '-logFile', $log
    ) $log

    Show-LogDiagnostics $log | Out-Null

    if (-not (Test-Path $results)) {
        Write-Host "NO TEST RESULTS PRODUCED - treating as failure." -ForegroundColor Red
        exit 1
    }

    [xml]$xml = Get-Content $results
    $run = $xml.'test-run'
    $failed = [int]$run.failed
    $summary = "Tests: total=" + $run.total + " passed=" + $run.passed + " failed=" + $run.failed + " skipped=" + $run.skipped

    if ($failed -gt 0) {
        Write-Host $summary -ForegroundColor Red
        $xml.SelectNodes('//test-case[@result="Failed"]') | ForEach-Object {
            Write-Host ("  FAILED: " + $_.fullname) -ForegroundColor Red
            $msg = $_.failure.message
            if ($msg) { Write-Host ("     " + ($msg | Out-String).Trim()) -ForegroundColor DarkRed }
        }
        exit 1
    }

    Write-Host $summary -ForegroundColor Green
    Write-Host ("TESTS OK (unity exit " + $code + ")") -ForegroundColor Green
    exit 0
}

if ($Mode -eq 'build') {
    $log = Join-Path $logDir 'build.log'
    $outDir = Join-Path $ProjectPath 'build\Android'

    $unityArgs = @(
        '-quit', '-batchmode', '-nographics',
        '-projectPath', $ProjectPath,
        '-executeMethod', 'HighwayRenegade.Editor.BuildScript.BuildAndroid',
        '-customBuildPath', $outDir,
        '-logFile', $log
    )
    if ($Apk) { $unityArgs += '-buildApk' }

    $code = Invoke-Unity $unityArgs $log
    Show-LogDiagnostics $log | Out-Null

    $artifact = Get-ChildItem $outDir -Include *.apk, *.aab -Recurse -ErrorAction SilentlyContinue |
                Sort-Object LastWriteTime -Descending | Select-Object -First 1

    if (-not $artifact) {
        Write-Host ("BUILD PRODUCED NO ARTIFACT (unity exit " + $code + ")") -ForegroundColor Red
        exit 1
    }

    $sizeMb = [math]::Round($artifact.Length / 1MB, 1)
    Write-Host ""
    Write-Host ("BUILD OK -> " + $artifact.FullName + " (" + $sizeMb + " MB)") -ForegroundColor Green
    exit 0
}

if ($Mode -eq 'activate-request') {
    # Produces a .alf the user uploads to license.unity3d.com/manual.
    # This path never requires anyone to hand over a password.
    $log = Join-Path $logDir 'activation.log'
    Invoke-Unity @('-quit', '-batchmode', '-nographics',
                   '-createManualActivationFile', '-logFile', $log) $log | Out-Null

    $alf = Get-ChildItem (Get-Location) -Filter '*.alf' -ErrorAction SilentlyContinue |
           Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($alf) {
        Write-Host ("ALF created: " + $alf.FullName) -ForegroundColor Green
        exit 0
    }
    Write-Host ("No .alf produced - check " + $log) -ForegroundColor Red
    exit 1
}

if ($Mode -eq 'activate-apply') {
    if (-not $LicenseFile -or -not (Test-Path $LicenseFile)) {
        throw "Pass -LicenseFile <path to .ulf>"
    }
    $log = Join-Path $logDir 'activation-apply.log'
    $code = Invoke-Unity @('-quit', '-batchmode', '-nographics',
                           '-manualLicenseFile', $LicenseFile, '-logFile', $log) $log
    Write-Host ("Activation exit code: " + $code)
    exit $code
}
