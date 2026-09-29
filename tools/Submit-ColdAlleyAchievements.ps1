param(
    [int[]]$AchievementId = @(1),
    [switch]$All,
    [switch]$Launch,
    [switch]$Foreground,
    [switch]$Detailed,
    [int]$WaitSeconds = 25
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$sessionRoot = Join-Path $repoRoot "analysis\coldalley\submit\$stamp"
$templatePath = Join-Path $scriptRoot "coldalley-frida-submit-achievements.js"
$scriptPath = Join-Path $sessionRoot "coldalley-submit-achievements.js"
$logPath = Join-Path $sessionRoot "submit.log"

New-Item -ItemType Directory -Force -Path $sessionRoot | Out-Null

function Get-ColdAlleyProcess {
    Get-Process |
        Where-Object {
            $_.ProcessName -match "Cold|Alley" -or
            ($_.Path -and $_.Path -like "*ColdAlley*")
        } |
        Select-Object -First 1
}

$frida = Get-Command frida -ErrorAction SilentlyContinue
if ($null -eq $frida) {
    throw "frida was not found on PATH."
}

if ($All) {
    $ids = 1..18
} else {
    $ids = $AchievementId |
    Where-Object { $_ -ge 1 -and $_ -le 18 } |
    Select-Object -Unique
}

if ($ids.Count -eq 0) {
    throw "Provide one or more Cold Alley achievement IDs from 1 to 18."
}

$process = Get-ColdAlleyProcess
if ($Launch) {
    if ($null -ne $process) {
        Write-Host "Cold Alley is already running as PID $($process.Id); reusing it."
    } else {
        Start-Process "shell:AppsFolder\Microsoft.ColdAlley_8wekyb3d8bbwe!App"
        $deadline = (Get-Date).AddSeconds(25)
        do {
            Start-Sleep -Milliseconds 100
            $process = Get-ColdAlleyProcess
        } while ($null -eq $process -and (Get-Date) -lt $deadline)
    }
}

if ($null -eq $process) {
    throw "Cold Alley is not running. Launch it first, or rerun with -Launch."
}

$idsJson = "[" + (($ids | ForEach-Object { $_.ToString() }) -join ",") + "]"
$detailedLiteral = if ($Detailed) { "true" } else { "false" }
(Get-Content -LiteralPath $templatePath -Raw).
    Replace("__ACHIEVEMENT_IDS__", $idsJson).
    Replace("__DETAILED__", $detailedLiteral) |
    Set-Content -LiteralPath $scriptPath -Encoding UTF8

@(
    "Cold Alley in-process achievement submit"
    "Started: $(Get-Date -Format o)"
    "PID: $($process.Id)"
    "IDs: $($ids -join ', ')"
    "Detailed: $Detailed"
    "Script: $scriptPath"
    "Log: $logPath"
) | Set-Content -LiteralPath (Join-Path $sessionRoot "session.txt") -Encoding UTF8

Write-Host "Attaching to Cold Alley PID $($process.Id)"
Write-Host "IDs: $($ids -join ', ')"
Write-Host "Log: $logPath"

if (-not $Foreground) {
    $errorLogPath = Join-Path $sessionRoot "submit-error.log"
    $arguments = "-p $($process.Id) -l `"$scriptPath`""
    $fridaProcess = Start-Process -FilePath $frida.Source -ArgumentList $arguments -NoNewWindow -PassThru -RedirectStandardOutput $logPath -RedirectStandardError $errorLogPath

    try {
        Start-Sleep -Seconds $WaitSeconds
    } finally {
        if ($null -ne $fridaProcess -and -not $fridaProcess.HasExited) {
            Stop-Process -Id $fridaProcess.Id -Force -ErrorAction SilentlyContinue
        }
    }

    Get-Content -LiteralPath $logPath -ErrorAction SilentlyContinue
    if ((Test-Path -LiteralPath $errorLogPath) -and (Get-Item -LiteralPath $errorLogPath).Length -gt 0) {
        Write-Host ""
        Write-Host "Frida stderr:"
        Get-Content -LiteralPath $errorLogPath
    }
} else {
    Write-Host ""
    Write-Host "Frida will stay attached so its output is visible."
    Write-Host "Wait for '[coldalley-submit] done', then press Ctrl+C if the prompt does not return."
    Write-Host ""
    & $frida.Source -p $process.Id -l $scriptPath 2>&1 | Tee-Object -FilePath $logPath
}
