#Requires -Version 5.1
#Requires -RunAsAdministrator
<# Run only inside a disposable Windows VM. This script installs/removes the product in that VM. #>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('DISPOSABLE-VM-ONLY')][string] $Confirmation,
    [Parameter(Mandatory)][string] $SetupPath,
    [Parameter(Mandatory)][string] $MsiPath,
    [string] $UpgradeSetupPath,
    [string] $OutputDirectory = (Join-Path $env:TEMP 'RtxMonitorInstallerAcceptance')
)

$ErrorActionPreference = 'Stop'

$model = (Get-CimInstance Win32_ComputerSystem).Model

if ($model -notmatch '(?i)virtual|vmware|kvm|qemu')
{
    throw 'This acceptance script refuses to run on a physical machine.'
}

$serviceName = 'Rtx5090MonitoringService'

if (Get-Service -Name $serviceName -ErrorAction SilentlyContinue)
{
    throw 'Use a clean VM snapshot with no existing RTX Monitor service.'
}

$configurationDirectory = Join-Path $env:ProgramData 'Rtx5090Monitoring'
$configurationPath = Join-Path $configurationDirectory 'appsettings.json'

if (Test-Path -LiteralPath $configurationDirectory)
{
    throw 'Use a clean VM snapshot with no existing RTX Monitor runtime configuration.'
}

$installDirectory = Join-Path $env:ProgramFiles 'Rtx5090Monitoring'

if (Test-Path -LiteralPath $installDirectory)
{
    throw 'Use a clean VM snapshot with no existing RTX Monitor installation folder.'
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $configurationDirectory | Out-Null

$settings = [ordered]@{
    monitoring = @{
        streamIntervalMs = 1000
        selections = @()
    }
    plugins = @{}
}

foreach ($id in @('wireview-pro2', 'asus-astral', 'msi-psu'))
{
    $settings.plugins[$id] = @{
        enabled = $false
        pollIntervalMs = 1000
        staleAfterMs = 5000
    }
}

$settings | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $configurationPath -Encoding UTF8

function Invoke-SetupProcess
{
    param(
        [string] $File,
        [string] $Arguments,
        [int[]] $ExpectedExitCodes = @(0, 3010)
    )

    $process = Start-Process -FilePath $File -ArgumentList $Arguments -PassThru -Wait -WindowStyle Hidden

    if ($process.ExitCode -notin $ExpectedExitCodes)
    {
        throw "$File returned $($process.ExitCode). See acceptance logs."
    }

    return $process.ExitCode
}

function Assert-Installed
{
    param([bool] $Desktop)

    if ((Get-Service $serviceName).Status -ne 'Running')
    {
        throw 'Service is not running.'
    }

    $health = Invoke-RestMethod 'http://localhost:59273/health'

    if ($health.status -ne 'running' -or $health.configurationRevision -lt 1)
    {
        throw 'Health gate failed.'
    }

    $app = Join-Path $installDirectory 'Desktop\Rtx5090Monitoring.App.exe'

    if ((Test-Path -LiteralPath $app) -ne $Desktop)
    {
        throw 'Desktop feature state is incorrect.'
    }

    $configuration = Get-Content -LiteralPath $configurationPath -Raw | ConvertFrom-Json

    if (@($configuration.plugins.PSObject.Properties.Value | Where-Object enabled).Count)
    {
        throw 'Acceptance fixture unexpectedly enabled hardware plugins.'
    }
}

$setup = [IO.Path]::GetFullPath($SetupPath)
$msi = [IO.Path]::GetFullPath($MsiPath)
$msiexec = Join-Path $env:SystemRoot 'System32\msiexec.exe'
$quoteMsi = '"' + $msi + '"'
$logs = [IO.Path]::GetFullPath($OutputDirectory)

[void](Invoke-SetupProcess $setup ('-quiet -norestart -log "' + (Join-Path $logs 'install.log') + '"'))
Assert-Installed $true

[void](Invoke-SetupProcess $msiexec ('/i ' + $quoteMsi + ' /qn /norestart ADDLOCAL=Service REMOVE=Desktop /l*v "' + (Join-Path $logs 'remove-desktop.log') + '"'))
Assert-Installed $false

[void](Invoke-SetupProcess $msiexec ('/i ' + $quoteMsi + ' /qn /norestart ADDLOCAL=Service,Desktop /l*v "' + (Join-Path $logs 'add-desktop.log') + '"'))
Assert-Installed $true

$repairFile = [IO.Path]::GetFullPath((Join-Path $installDirectory 'Desktop\Assets\monitor.ico'))

if (-not $repairFile.StartsWith([IO.Path]::GetFullPath($installDirectory) + '\', [StringComparison]::OrdinalIgnoreCase))
{
    throw 'Invalid repair-test file path.'
}

Remove-Item -LiteralPath $repairFile

[void](Invoke-SetupProcess $msiexec ('/fa ' + $quoteMsi + ' /qn /norestart /l*v "' + (Join-Path $logs 'repair.log') + '"'))

if (-not (Test-Path -LiteralPath $repairFile))
{
    throw 'Repair did not restore the missing app asset.'
}

Assert-Installed $true

if ($UpgradeSetupPath)
{
    [void](Invoke-SetupProcess ([IO.Path]::GetFullPath($UpgradeSetupPath)) ('-quiet -norestart -log "' + (Join-Path $logs 'upgrade.log') + '"'))
    Assert-Installed $true
}

[void](Invoke-SetupProcess $setup ('-uninstall -quiet -norestart -log "' + (Join-Path $logs 'uninstall.log') + '"'))

if (Get-Service -Name $serviceName -ErrorAction SilentlyContinue)
{
    throw 'Uninstall left the Windows service registered.'
}

if (Test-Path -LiteralPath (Join-Path $installDirectory 'Service\Rtx5090Monitoring.Service.exe'))
{
    throw 'Uninstall left service binaries.'
}

if (Test-Path -LiteralPath (Join-Path $installDirectory 'Desktop\Rtx5090Monitoring.App.exe'))
{
    throw 'Uninstall left desktop binaries.'
}

if (-not (Test-Path -LiteralPath $configurationPath))
{
    throw 'Uninstall deleted retained runtime configuration.'
}

Write-Host 'PASS: Install, feature removal/addition, repair, optional upgrade and uninstall completed. Configuration was retained.'
Write-Host 'Still verify the desktop UI, Windows reboot/sign-in startup, minimized launch, failed-upgrade rollback and native iCUE import manually.'
