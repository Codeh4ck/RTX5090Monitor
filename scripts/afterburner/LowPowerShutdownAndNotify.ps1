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
. (Join-Path $PSScriptRoot 'SafeguardWorkflow.ps1')
if ($Probe) {
    Test-SafeguardContext -CheckShutdownPrivilege
    exit 0
}

Invoke-SafeguardWorkflow -ScriptId 'low-power-shutdown-notify' -BindingId $BindingId -EntryScript $PSCommandPath -Simulate:$Simulate -TestRoot $TestRoot