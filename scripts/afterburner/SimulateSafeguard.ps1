[CmdletBinding(DefaultParameterSetName = 'Run')]
param(
    [Parameter(Mandatory, ParameterSetName = 'Run')]
    [Parameter(Mandatory, ParameterSetName = 'Simulate')]
    [guid] $BindingId,
    [Parameter(Mandatory, ParameterSetName = 'Probe')]
    [switch] $Probe,
    [Parameter(Mandatory, ParameterSetName = 'Simulate')]
    [switch] $Simulate,
    [Parameter(Mandatory, ParameterSetName = 'Simulate')]
    [string] $TestRoot
)

$ErrorActionPreference = 'Stop'
$workflowPath = Join-Path $PSScriptRoot 'SafeguardWorkflow.ps1'
. $workflowPath

if (!$Simulate) {
    Test-SafeguardContext
}

if ($Probe) {
    exit 0
}

$root = if ($Simulate) { [IO.Path]::GetFullPath($TestRoot) } else {
    Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::CommonApplicationData)) 'Rtx5090Monitoring.Safeguard'
}
$bindingPath = Join-Path $root ('bindings\' + $BindingId.ToString('N') + '.json')
$history = Join-Path $root 'history'
$outbox = Join-Path $root 'outbox'

foreach ($path in @($bindingPath, $history, $outbox, $PSCommandPath, $workflowPath)) {
    Assert-SafeguardPath $path
}

$security = $null
if ($Simulate) {
    [IO.Directory]::CreateDirectory($history) | Out-Null
    [IO.Directory]::CreateDirectory($outbox) | Out-Null
}
else {
    foreach ($path in @($root, (Join-Path $root 'bindings'), $bindingPath, $history, $outbox)) {
        Assert-SafeguardDataAcl $path
    }
    $security = New-SafeguardFileSecurity
}

if ((Get-Item -LiteralPath $bindingPath).Length -gt 8192) {
    throw 'Invalid Safeguard+ binding size.'
}

$binding = Get-Content -Raw -Encoding UTF8 -LiteralPath $bindingPath | ConvertFrom-Json
if ($BindingId -eq [guid]::Empty -or [guid]$binding.id -ne $BindingId -or $binding.scriptId -cne 'simulation' -or
    $binding.version -ne 1 -or $binding.connector -notin 1, 2 -or $binding.delayMs -isnot [int] -or
    $binding.delayMs -lt 0 -or $binding.delayMs -gt 3600000 -or !$binding.mailId -or [guid]$binding.mailId -eq [guid]::Empty) {
    throw 'Invalid Safeguard+ simulation binding.'
}

if ((Get-FileHash -LiteralPath $PSCommandPath).Hash -ne $binding.scriptHash -or
    (Get-FileHash -LiteralPath $workflowPath).Hash -ne $binding.workflowHash) {
    throw 'Safeguard+ script files have changed. Reconfigure the action.'
}

$lockPath = Join-Path $root ('connector-' + $binding.connector + '.lock')
Assert-SafeguardPath $lockPath
try {
    $guard = if ($security) {
        [IO.FileStream]::new($lockPath, 'OpenOrCreate', [Security.AccessControl.FileSystemRights]::FullControl,
            [IO.FileShare]::None, 4096, [IO.FileOptions]::None, $security)
    }
    else {
        [IO.FileStream]::new($lockPath, 'OpenOrCreate', 'ReadWrite', 'None')
    }
}
catch {
    if (($_.Exception.GetBaseException().HResult -band 0xffff) -in 32, 33) {
        return
    }
    throw
}

try {
    $id = [guid]::NewGuid()
    $name = $id.ToString('N') + '.json'
    $recordPath = Join-Path $history $name
    $run = @{
        id = $id; bindingId = $BindingId; scriptId = 'simulation'; version = 1; connector = $binding.connector
        delayMs = $binding.delayMs; startedAt = [DateTimeOffset]::UtcNow.ToString('O'); finishedAt = $null
        status = 'Simulated'; steps = @()
    }

    Add-SafeguardStep $run 'Afterburner low-power command' 'Simulated. GPU settings were not changed.'
    Add-SafeguardStep $run 'Windows shutdown' 'Simulated. No shutdown was requested.'
    Add-SafeguardStep $run 'Queue email' 'Queued for Windows service'
    $run.finishedAt = [DateTimeOffset]::UtcNow.ToString('O')
    Publish-SafeguardRecord $run $recordPath $security

    try {
        Write-SafeguardJson (Join-Path $outbox $name) @{
            executionId = $id; bindingId = $BindingId; startedAt = $run.startedAt
        } $security
    }
    catch {
        $run.status = 'Simulation email queue failed'
        $run.steps[-1].result = 'Could not write email request'
        Publish-SafeguardRecord $run $recordPath $security
        throw
    }
}
finally {
    $guard.Dispose()
}
