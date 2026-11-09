$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$root = Join-Path ([IO.Path]::GetTempPath()) ('rtx-safeguard-fixture-' + [guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory((Join-Path $root 'bindings')) | Out-Null

$windowsPowerShell = Join-Path ([Environment]::SystemDirectory) 'WindowsPowerShell\v1.0\powershell.exe'
$workflow = Join-Path $repo 'scripts/afterburner/SafeguardWorkflow.ps1'
$cases = @(
    @{ Id = 'low-power-shutdown'; File = 'LowPowerAndShutdown.ps1'; Notify = $false }
    @{ Id = 'low-power-shutdown-notify'; File = 'LowPowerShutdownAndNotify.ps1'; Notify = $true }
)

foreach ($case in $cases) {
    $entry = Join-Path $repo ('scripts/afterburner/' + $case.File)
    $id = [guid]::NewGuid()
    $path = Join-Path $root ('bindings\' + $id.ToString('N') + '.json')
    $config = @{
        id = $id; scriptId = $case.Id; version = 1; connector = 2; delayMs = 5000
        afterburnerPath = Join-Path $root 'MSIAfterburner.exe'
        executableHash = '0' * 64
        scriptHash = (Get-FileHash -LiteralPath $entry).Hash
        workflowHash = (Get-FileHash -LiteralPath $workflow).Hash
        mailId = if ($case.Notify) { [guid]::NewGuid() } else { $null }
    }
    [IO.File]::WriteAllText($path, ($config | ConvertTo-Json -Compress))
    & $windowsPowerShell -NoLogo -NoProfile -NonInteractive -File $entry -BindingId $id -Simulate -TestRoot $root
    if ($LASTEXITCODE -ne 0) {
        throw 'Safeguard+ workflow simulation failed.'
    }

    $journal = Join-Path $root 'history'
    $records = @(Get-ChildItem -LiteralPath $journal -Filter '*.json')
    $record = $records | ForEach-Object { Get-Content -Raw -LiteralPath $_.FullName | ConvertFrom-Json } | Where-Object bindingId -eq $id
    if ($record.status -ne 'Simulated' -or $record.steps.Count -ne (2 + [int]$case.Notify) -or !$record.finishedAt) {
        throw 'The low-power timeout must still produce a completed shutdown attempt in the journal.'
    }

    $outbox = Join-Path $root ('outbox\' + ([guid]$record.id).ToString('N') + '.json')
    if ([IO.File]::Exists($outbox) -ne $case.Notify) {
        throw 'Unexpected persistent email request state.'
    }

    $guard = [IO.FileStream]::new((Join-Path $root 'connector-2.lock'), 'OpenOrCreate', 'ReadWrite', 'None')
    try {
        & $windowsPowerShell -NoLogo -NoProfile -NonInteractive -File $entry -BindingId $id -Simulate -TestRoot $root
        if ($LASTEXITCODE -ne 0 -or @(Get-ChildItem -LiteralPath $journal -Filter '*.json').Count -ne $records.Count) {
            throw 'A second alarm must not run while this connector has an active execution.'
        }
    }
    finally {
        $guard.Dispose()
    }

    $config.delayMs = -1
    [IO.File]::WriteAllText($path, ($config | ConvertTo-Json -Compress))
    $previousErrorPolicy = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        & $windowsPowerShell -NoLogo -NoProfile -NonInteractive -File $entry -BindingId $id -Simulate -TestRoot $root 2>$null
        if ($LASTEXITCODE -eq 0 -or @(Get-ChildItem -LiteralPath $journal -Filter '*.json').Count -ne $records.Count) {
            throw 'An invalid delay reached the execution journal.'
        }
    }
    finally {
        $ErrorActionPreference = $previousErrorPolicy
    }

    Write-Output ('PASS ' + $case.Id + ' timeout, shutdown journal, email queue, repeated alarm and invalid delay.')
}

Write-Output ('Simulation records: ' + $root)