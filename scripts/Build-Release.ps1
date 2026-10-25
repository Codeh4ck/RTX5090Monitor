#Requires -Version 7.0
<# Builds local release artifacts; never executes the installer or changes an installed service. #>
[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+$')][string] $Version = '0.4.0',
    [string] $VcRedistPath,

    [ValidatePattern('^[A-Fa-f0-9]{40}$')][string] $SigningCertificateThumbprint,
    [string] $TimestampUrl = 'http://timestamp.digicert.com'
)

$ErrorActionPreference = 'Stop'

Set-StrictMode -Version Latest

Import-Module (Join-Path $PSScriptRoot 'Release.psm1') -Force

$solution = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$work = Join-Path $solution ('.dist\build\' + [Guid]::NewGuid().ToString('N'))
$payload = Join-Path $work 'payload'
$release = Join-Path $solution ".dist\releases\$Version"

New-Item -ItemType Directory -Path $payload -Force | Out-Null

if (-not $VcRedistPath)
{
    $VcRedistPath = Join-Path $solution '.dist\prerequisites\vc_redist.x64.exe'
}

$VcRedistPath = [IO.Path]::GetFullPath($VcRedistPath)
$vcVersion = Get-VerifiedVcRedistributable $VcRedistPath

Push-Location $solution

try
{
    Invoke-ReleaseCommand dotnet @('run', '--project', 'Rtx5090Monitoring.Tests', '-c', 'Release')

    Invoke-ReleaseCommand dotnet @('run', '--project', 'UI/Rtx5090Monitoring.UI.Tests', '-c', 'Release', '-p:EnableTelemetryDebug=false')

    Invoke-ReleaseCommand dotnet @('run', '--project', 'Installer/Rtx5090Monitoring.Installer.Tests', '-c', 'Release')

    Invoke-ReleaseCommand dotnet @('publish', 'Rtx5090Monitoring.Service', '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true',
        '-p:PublishSingleFile=false', '-p:PublishTrimmed=false', "-p:Version=$Version", '-o', (Join-Path $payload 'Service'))

    Invoke-ReleaseCommand dotnet @('publish', 'UI/Rtx5090Monitoring.App', '-c', 'Release', '-p:PublishProfile=Portable',
        '-p:EnableTelemetryDebug=false', "-p:Version=$Version", '-o', (Join-Path $payload 'Desktop'), '-m:1')

    Invoke-ReleaseCommand dotnet @('run', '--project', 'UI/Rtx5090Monitoring.UI.Tests', '-c', 'Release', '-p:EnableTelemetryDebug=false',
        '--', '--verify-release', (Join-Path $payload 'Desktop'))

    Push-Location (Join-Path $solution 'XeneonEdgeWidget')

    try
    {
        if (-not (Test-Path -LiteralPath 'node_modules'))
        {
            Invoke-ReleaseCommand npm.cmd @('ci')
        }

        Invoke-ReleaseCommand npm.cmd @('run', 'build')
        Invoke-ReleaseCommand npm.cmd @('run', 'verify')
    }
    finally
    {
        Pop-Location
    }

    $signTool = ''

    if ($SigningCertificateThumbprint)
    {
        $signTool = Get-ReleaseSignTool

        foreach ($file in Get-ChildItem -LiteralPath $payload -Recurse -File | Where-Object { $_.Name -like 'Rtx5090Monitoring.*' -and $_.Extension -in @('.exe', '.dll') })
        {
            Invoke-ReleaseCommand $signTool @('sign', '/sha1', $SigningCertificateThumbprint, '/fd', 'SHA256', '/tr', $TimestampUrl, '/td', 'SHA256', $file.FullName)
        }
    }

    $buildProperties = @('-c', 'Release', "-p:InstallerVersion=$Version", "-p:PayloadDirectory=$payload")

    Invoke-ReleaseCommand dotnet (@('build', 'Installer/Rtx5090Monitoring.Installer/Rtx5090Monitoring.Installer.wixproj') + $buildProperties)

    $msi = Join-Path $solution "Installer\Rtx5090Monitoring.Installer\bin\x64\Release\RtxMonitor-$Version-win-x64.msi"

    if ($SigningCertificateThumbprint)
    {
        Invoke-ReleaseCommand $signTool @('sign', '/sha1', $SigningCertificateThumbprint, '/fd', 'SHA256', '/tr', $TimestampUrl, '/td', 'SHA256', $msi)
    }

    Invoke-ReleaseCommand dotnet @('run', '--project', 'Installer/Rtx5090Monitoring.Installer.Tests', '-c', 'Release', '--', '--msi', $msi, $payload)

    Invoke-ReleaseCommand dotnet (@('build', 'Installer/Rtx5090Monitoring.Bootstrapper/Rtx5090Monitoring.Bootstrapper.wixproj') + $buildProperties +
        @('-p:BuildProjectReferences=false', "-p:VcRedistPath=$VcRedistPath", "-p:VcRedistVersion=$vcVersion",
            "-p:SigningCertificateThumbprint=$SigningCertificateThumbprint", "-p:TimestampUrl=$TimestampUrl", "-p:SignToolPath=$signTool"))

    $setup = Join-Path $solution "Installer\Rtx5090Monitoring.Bootstrapper\bin\x64\Release\RtxMonitor-Setup-$Version-win-x64.exe"

    & (Join-Path $PSScriptRoot 'Verify-Setup.ps1') -SetupPath $setup -MsiPath $msi -VcRedistPath $VcRedistPath -OutputDirectory (Join-Path $work 'extracted-setup')

    New-Item -ItemType Directory -Path $release -Force | Out-Null

    Copy-Item -LiteralPath $setup -Destination $release -Force
    Copy-Item -LiteralPath (Join-Path $solution '.dist\rtx5090monitor.icuewidget') -Destination $release -Force
    Copy-Item -LiteralPath (Join-Path $solution 'Installer\Resources\ReleaseReadme.txt') -Destination (Join-Path $release 'README.txt') -Force
    Copy-Item -LiteralPath (Join-Path $solution 'Installer\Resources\CHANGELOG.txt') -Destination $release -Force

    & (Join-Path $PSScriptRoot 'Collect-Licenses.ps1') -SolutionDirectory $solution -OutputPath (Join-Path $release 'THIRD-PARTY-NOTICES.txt')

    Copy-Item -LiteralPath (Join-Path $solution 'uninstall.ps1') -Destination (Join-Path $release 'Uninstall-RTXMonitor.ps1') -Force

    $widget = Get-Content -LiteralPath (Join-Path $solution '.dist\latest.json') -Raw | ConvertFrom-Json
    $commit = 'unavailable'
    $sourceHasChanges = $null

    if (Test-Path -LiteralPath (Join-Path $solution '.git'))
    {
        $commit = & git rev-parse HEAD

        if ($LASTEXITCODE -ne 0)
        {
            $commit = 'unavailable'
        }
        else
        {
            $sourceHasChanges = [bool](& git status --porcelain)
        }
    }

    $metadata = [ordered]@{
        version = $Version
        runtime = 'win-x64'
        widgetVersion = $widget.version
        sourceCommit = $commit
        sourceHasChanges = $sourceHasChanges
        signed = [bool]$SigningCertificateThumbprint
        vcRedistVersion = $vcVersion
        vcRedistSha256 = (Get-FileHash -LiteralPath $VcRedistPath -Algorithm SHA256).Hash
        builtAt = [DateTime]::UtcNow.ToString('o')
        cleanMachineAcceptance = 'pending'
        serviceVersion = $Version
        desktopVersion = $Version
    }

    $metadata | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $release 'release.json') -Encoding UTF8

    Write-ReleaseFileManifest $release

    $zip = Join-Path $release "RtxMonitor-$Version-win-x64.zip"
    $entries = Get-ChildItem -LiteralPath $release -File | Where-Object Extension -ne '.zip'

    Compress-Archive -LiteralPath $entries.FullName -DestinationPath $zip -Force

    [ordered]@{
        version = $Version
        installer = (Join-Path $release ([IO.Path]::GetFileName($setup)))
        zip = $zip
        sha256 = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
        signed = [bool]$SigningCertificateThumbprint
    } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $solution '.dist\latest-release.json') -Encoding UTF8

    Write-Host "Release ZIP: $zip" -ForegroundColor Green

    if (-not $SigningCertificateThumbprint)
    {
        Write-Warning 'Unsigned release candidate. Code signing and clean-machine acceptance are required before a public release.'
    }
}
finally
{
    Pop-Location
}
