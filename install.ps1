#Requires -Version 5.1
[CmdletBinding(SupportsShouldProcess)]
param([string] $InstallerPath)

$ErrorActionPreference = 'Stop'

if (-not $InstallerPath)
{
    $release = Get-Content -LiteralPath (Join-Path $PSScriptRoot '.dist\latest-release.json') -Raw | ConvertFrom-Json
    $InstallerPath = $release.installer
}

$InstallerPath = [IO.Path]::GetFullPath($InstallerPath)

if (-not (Test-Path -LiteralPath $InstallerPath -PathType Leaf))
{
    throw "Installer not found: $InstallerPath"
}

if ($PSCmdlet.ShouldProcess($InstallerPath, 'Open RTX Monitor setup (service required, desktop optional)'))
{
    $process = Start-Process -FilePath $InstallerPath -PassThru -Wait

    if ($process.ExitCode -notin @(0, 3010))
    {
        throw "Setup returned exit code $($process.ExitCode)."
    }
}
