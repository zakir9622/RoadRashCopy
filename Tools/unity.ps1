<#
.SYNOPSIS
  Headless Unity driver for Highway Renegade: compile, test, and build.

.DESCRIPTION
  Wraps Unity's batch mode and — critically — parses the log, because Unity's
  exit codes alone are not trustworthy. A build with compile errors can still
  exit 0 in some configurations, so every mode here verifies its actual artifact
  (compiled assemblies, a results XML, an APK on disk) rather than believing $LASTEXITCODE.

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
    $exe = Join-Path $EditorRoot "$Version\Editor\Unity.exe"
    if (-not (Test-Path $exe)) {
        # Fall back to any installed version rather than failing on an exact match.
        $found = Get-ChildItem $EditorRoot -Directory -ErrorAction SilentlyContinue |
                 ForEach-Object { Join-Path $_.FullName 'Editor\Unity.exe' } |
                 Where-Object { Test-Path $_ } | Select-Object -First 1
        if ($found) {
            Write-Warning "Unity $Version not found; using $found"
            return $found
        }
        throw "No Unity editor found under $EditorRoot"
    }
    return $exe
}

function Invoke-Unity {
    param([string[]]$UnityArgs, [string]$LogFile)

    if (Test-Path $LogFile) { Remove-Item $LogFile -Force }
    $exe = Get-UnityExe

    Write-Host "> Unity $($UnityArgs -join ' ')" -ForegroundColor Cyan
    $p = Start-Process -FilePath $exe -ArgumentList $UnityArgs -NoNewWindow -PassThru

    if (-not $p.WaitForExit($TimeoutMinutes * 60 * 1000)) {
        $p.Kill()
        throw "Unity timed out after $TimeoutMinutes minutes."
    }
    return $p.ExitCode
}

<#
  Unity writes every compiler diagnostic to the editor log. Scraping it is the only
  reliable way to surface errors from a headless run — the console is not available
  and the exit code does not distinguish "compiled with errors" from other failures.
#>
function Show-LogDiagnostics {
    param([string]$LogFile, [int]$MaxLines = 60)

    if (-not (Test-Path $LogFile)) { Write-Warning "No log at $LogFile"; return @() }

    $lines = Get-Content $LogFile -ErrorAction SilentlyContinue

    $errors = $lines | Where-Object {
        $_ -match 'error CS\d+' -or
        $_ -match '^\s*Compilation failed' -or
        $_ -match 'Error building Player' -or
        $_ -match 'Failed to resolve packages' -or
        $_ -match 'error:.*\.asmdef'
    } | Select-Object -Unique

    $warnings = $lines | Where-Object { $_ -match 'warning CS\d+' } | Select-Object -Unique

    if ($errors.Count -gt 0) {
        Write-Host "`n=== ERRORS ($($errors.Count)) ===" -ForegroundColor Red
        $errors | Select-Object -First $MaxLines | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    } else {
        Write-Host "`nNo compile errors found in log." -ForegroundColor Green
    }

    if ($warnings.Count -gt 0) {
        Write-Host "`n=== WARNINGS ($($warnings.Count), first 15) ===" -ForegroundColor Yellow
        $warnings | Select-Object -First 15 | ForEach-Object { Write-Host "  $_" -ForegroundColor Yellow }
    }

    return $errors
}

$logDir = Join-Path $ProjectPath 'Logs'
New-Item -ItemType Directory -Path $logDir -Force | Out-Null

switch ($Mode) {

    'compile' {
        $log = Join-Path $logDir 'compile.log'
        # Importing assets forces a full script compile. -quit is safe here because
        # there is no test runner waiting to write results.
        $code = Invoke-Unity @(
            '-quit', '-batchmode', '-nographics',
            '-projectPath', $ProjectPath,
            '-logFile', $log
        ) $log

        $errs = Show-LogDiagnostics $log
        Write-Host "`nUnity exit code: $code"
        if ($errs.Count -gt 0) { exit 1 }
        Write-Host "COMPILE OK" -ForegroundColor Green
    }

    'test' {
        $log = Join-Path $logDir 'test.log'
        $results = Join-Path $logDir 'editmode-results.xml'
        if (Test-Path $results) { Remove-Item $results -Force }

        # NOTE: no -quit. It would terminate the editor before the test runner
        # finishes writing results, producing a silent false pass.
        $code = Invoke-Unity @(
            '-batchmode', '-nographics',
            '-projectPath', $ProjectPath,
            '-runTests', '-testPlatform', 'EditMode',
            '-testResults', $results,
            '-logFile', $log
        ) $log

        Show-LogDiagnostics $log | Out-Null

        if (-not (Test-Path $results)) {
            Write-Host "NO TEST RESULTS PRODUCED — treating as failure." -ForegroundColor Red
            exit 1
        }

        [xml]$xml = Get-Content $results
        $r = $xml.'test-run'
        Write-Host "`nTests: total=$($r.total) passed=$($r.passed) failed=$($r.failed) skipped=$($r.skipped)" `
                   -ForegroundColor $(if ([int]$r.failed -gt 0) { 'Red' } else { 'Green' })

        if ([int]$r.failed -gt 0) {
            $xml.SelectNodes('//test-case[@result="Failed"]') | ForEach-Object {
                Write-Host "  FAILED: $($_.fullname)" -ForegroundColor Red
                if ($_.failure.message.'#cdata-section') {
                    Write-Host "     $($_.failure.message.'#cdata-section'.Trim())" -ForegroundColor DarkRed
                }
            }
            exit 1
        }
        Write-Host "TESTS OK  (unity exit $code)" -ForegroundColor Green
    }

    'build' {
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
            Write-Host "BUILD PRODUCED NO ARTIFACT (unity exit $code)" -ForegroundColor Red
            exit 1
        }
        Write-Host "`nBUILD OK -> $($artifact.FullName)  ($([math]::Round($artifact.Length/1MB,1)) MB)" -ForegroundColor Green
    }

    'activate-request' {
        # Produces a .alf the user uploads to license.unity3d.com/manual.
        # This path never requires anyone to hand over a password.
        $log = Join-Path $logDir 'activation.log'
        Invoke-Unity @('-quit', '-batchmode', '-nographics',
                       '-createManualActivationFile', '-logFile', $log) $log | Out-Null

        $alf = Get-ChildItem (Get-Location) -Filter '*.alf' -ErrorAction SilentlyContinue |
               Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if ($alf) { Write-Host "ALF created: $($alf.FullName)" -ForegroundColor Green }
        else { Write-Host "No .alf produced — check $log" -ForegroundColor Red; exit 1 }
    }

    'activate-apply' {
        if (-not $LicenseFile -or -not (Test-Path $LicenseFile)) {
            throw "Pass -LicenseFile <path to .ulf>"
        }
        $log = Join-Path $logDir 'activation-apply.log'
        $code = Invoke-Unity @('-quit', '-batchmode', '-nographics',
                               '-manualLicenseFile', $LicenseFile, '-logFile', $log) $log
        Write-Host "Activation exit code: $code"
    }
}
