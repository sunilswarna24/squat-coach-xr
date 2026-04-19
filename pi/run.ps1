# Launcher for the Pi edge server on Windows (development).
$ErrorActionPreference = "Stop"

$Here = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $Here

if (-not (Test-Path ".venv")) {
    Write-Host "[run.ps1] Creating virtual env in .venv ..."
    python -m venv .venv
}

. .\.venv\Scripts\Activate.ps1

python -m pip install --quiet --upgrade pip
python -m pip install --quiet -r requirements.txt

python -m src.main @args
