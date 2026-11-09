function Test-SafeguardContext([switch] $CheckShutdownPrivilege) {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    try {
        $principal = [Security.Principal.WindowsPrincipal]::new($identity)
        if (!$principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
            throw 'Start MSI Afterburner as administrator before using Safeguard+ scripts.'
        }
    }
    finally {
        $identity.Dispose()
    }

    if ($CheckShutdownPrivilege) {
        $whoami = Join-Path ([Environment]::SystemDirectory) 'whoami.exe'
        $privileges = & $whoami /priv /fo csv /nh
        if ($LASTEXITCODE -ne 0 -or !($privileges -match 'SeShutdownPrivilege')) {
            throw 'The Windows token does not have permission to request local shutdown.'
        }
    }
}

function Assert-SafeguardPath([string] $Path) {
    for ($current = [IO.Path]::GetFullPath($Path); $current; $current = [IO.Path]::GetDirectoryName($current)) {
        if ([IO.File]::Exists($current) -or [IO.Directory]::Exists($current)) {
            if ([IO.File]::GetAttributes($current) -band [IO.FileAttributes]::ReparsePoint) {
                throw 'Safeguard+ cannot use linked files or directories.'
            }
        }
    }
}

function Assert-SafeguardDataAcl([string] $Path) {
    $acl = Get-Acl -LiteralPath $Path
    $trusted = 'S-1-5-18', 'S-1-5-32-544'
    $sidType = [Security.Principal.SecurityIdentifier]
    if ($acl.GetOwner($sidType).Value -notin $trusted) {
        throw 'Safeguard+ data has an unexpected owner.'
    }

    foreach ($rule in $acl.GetAccessRules($true, $true, $sidType)) {
        if ($rule.AccessControlType -eq 'Allow' -and $rule.IdentityReference.Value -notin $trusted) {
            throw 'Safeguard+ data has unexpected access permissions.'
        }
    }
}

function New-SafeguardFileSecurity {
    $security = [Security.AccessControl.FileSecurity]::new()
    $admins = [Security.Principal.SecurityIdentifier]::new('S-1-5-32-544')
    $security.SetOwner($admins)
    $security.SetAccessRuleProtection($true, $false)
    foreach ($sid in 'S-1-5-18', 'S-1-5-32-544') {
        $identity = [Security.Principal.SecurityIdentifier]::new($sid)
        $rule = [Security.AccessControl.FileSystemAccessRule]::new($identity, 'FullControl', 'Allow')
        $security.AddAccessRule($rule)
    }

    return $security
}

function Write-SafeguardJson([string] $Path, $Value, $Security) {
    $temporary = $Path + '.' + [guid]::NewGuid().ToString('N') + '.tmp'
    try {
        $bytes = [Text.UTF8Encoding]::new($false).GetBytes(($Value | ConvertTo-Json -Depth 8 -Compress))
        $file = if ($Security) {
            [IO.FileStream]::new($temporary, 'CreateNew', [Security.AccessControl.FileSystemRights]::FullControl,
                [IO.FileShare]::None, 4096, [IO.FileOptions]::None, $Security)
        }
        else {
            [IO.FileStream]::new($temporary, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
        }
        try {
            $file.Write($bytes, 0, $bytes.Length)
            $file.Flush($true)
        }
        finally {
            $file.Dispose()
        }

        if ([IO.File]::Exists($Path)) {
            [IO.File]::Replace($temporary, $Path, [NullString]::Value)
        }
        else {
            [IO.File]::Move($temporary, $Path)
        }
    }
    finally {
        [IO.File]::Delete($temporary)
    }
}

function Publish-SafeguardRecord($Run, [string] $Path, $Security) {
    if (!$Path) {
        return
    }

    try {
        Write-SafeguardJson $Path $Run $Security
    }
    catch {
        Write-Warning ('Could not write the Safeguard+ record: ' + $_.Exception.Message)
    }
}

function Add-SafeguardStep($Run, [string] $Name, [string] $Result, $ExitCode = $null) {
    $Run.steps += @{ timestamp = [DateTimeOffset]::UtcNow.ToString('O'); name = $Name; result = $Result; exitCode = $ExitCode }
}

function Start-SafeguardProcess([string] $Path, [string] $Arguments, [int] $TimeoutMs) {
    $start = [Diagnostics.ProcessStartInfo]::new($Path, $Arguments)
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.WorkingDirectory = [IO.Path]::GetDirectoryName($Path)
    $process = [Diagnostics.Process]::Start($start)
    try {
        if (!$process.WaitForExit($TimeoutMs)) {
            return @{ Result = 'Timed out'; ExitCode = $null }
        }

        $result = if ($process.ExitCode -eq 0) { 'Command returned successfully' } else { 'Command failed' }
        return @{ Result = $result; ExitCode = $process.ExitCode }
    }
    finally {
        $process.Dispose()
    }
}

function Invoke-SafeguardWorkflow {
    param([string] $ScriptId, [guid] $BindingId, [string] $EntryScript, [switch] $Simulate, [string] $TestRoot)

    $root = if ($Simulate) { [IO.Path]::GetFullPath($TestRoot) } else {
        Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::CommonApplicationData)) 'Rtx5090Monitoring.Safeguard'
    }

    if (!$Simulate) {
        Test-SafeguardContext
    }

    $bindingPath = Join-Path $root ('bindings\' + $BindingId.ToString('N') + '.json')
    Assert-SafeguardPath $bindingPath
    $security = $null
    if (!$Simulate) {
        Assert-SafeguardDataAcl $root
        Assert-SafeguardDataAcl (Join-Path $root 'bindings')
        Assert-SafeguardDataAcl $bindingPath
        $security = New-SafeguardFileSecurity
    }

    if ((Get-Item -LiteralPath $bindingPath).Length -gt 8192) {
        throw 'Invalid Safeguard+ binding size.'
    }

    $binding = Get-Content -Raw -Encoding UTF8 -LiteralPath $bindingPath | ConvertFrom-Json
    if ([guid]$binding.id -ne $BindingId -or $binding.scriptId -cne $ScriptId -or $binding.version -ne 1 -or
        $binding.connector -notin 1, 2 -or $binding.delayMs -isnot [int] -or $binding.delayMs -lt 0 -or $binding.delayMs -gt 3600000 -or
        ![IO.Path]::IsPathRooted($binding.afterburnerPath) -or [IO.Path]::GetFileName($binding.afterburnerPath) -ine 'MSIAfterburner.exe' -or
        ($ScriptId -eq 'low-power-shutdown-notify' -and (!$binding.mailId -or [guid]$binding.mailId -eq [guid]::Empty))) {
        throw 'Invalid Safeguard+ binding.'
    }

    Assert-SafeguardPath $EntryScript
    $workflowPath = Join-Path $PSScriptRoot 'SafeguardWorkflow.ps1'
    Assert-SafeguardPath $workflowPath
    $history = Join-Path $root 'history'
    $outbox = Join-Path $root 'outbox'
    if (!$Simulate) {
        try {
            Assert-SafeguardPath $history
            Assert-SafeguardDataAcl $history
        }
        catch {
            $history = $null
            Write-Warning 'Safeguard+ history is unavailable. Continuing with protection.'
        }

        try {
            Assert-SafeguardPath $outbox
            Assert-SafeguardDataAcl $outbox
        }
        catch {
            $outbox = $null
            Write-Warning 'Safeguard+ email queue is unavailable. Continuing with protection.'
        }
    }
    else {
        [IO.Directory]::CreateDirectory($history) | Out-Null
        [IO.Directory]::CreateDirectory($outbox) | Out-Null
    }

    # Separate locks let either connector act without resetting the other's countdown.
    $lockPath = Join-Path $root ('connector-' + $binding.connector + '.lock')
    $guard = $null
    try {
        Assert-SafeguardPath $lockPath
        $guard = if ($security) {
            [IO.FileStream]::new($lockPath, 'OpenOrCreate', [Security.AccessControl.FileSystemRights]::FullControl,
                [IO.FileShare]::None, 4096, [IO.FileOptions]::None, $security)
        }
        else {
            [IO.FileStream]::new($lockPath, [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
        }
    }
    catch {
        if (($_.Exception.GetBaseException().HResult -band 0xffff) -in 32, 33) {
            return
        }

        Write-Warning 'Safeguard+ could not lock its journal. Continuing with protection.'
    }

    try {
        $id = [guid]::NewGuid()
        $name = $id.ToString('N') + '.json'
        $recordPath = if ($history) { Join-Path $history $name } else { $null }
        $run = @{
            id = $id; bindingId = $BindingId; scriptId = $ScriptId; version = 1; connector = $binding.connector
            delayMs = $binding.delayMs; startedAt = [DateTimeOffset]::UtcNow.ToString('O'); finishedAt = $null
            status = 'Running'; steps = @()
        }

        Publish-SafeguardRecord $run $recordPath $security
        try {
            if ((Get-FileHash -LiteralPath $EntryScript).Hash -ne $binding.scriptHash -or
                (Get-FileHash -LiteralPath $workflowPath).Hash -ne $binding.workflowHash) {
                throw 'Safeguard+ script files have changed. Reapply the connector configuration.'
            }
        }
        catch {
            $run.status = 'Script verification failed'
            $run.finishedAt = [DateTimeOffset]::UtcNow.ToString('O')
            Add-SafeguardStep $run 'Verify scripts' $_.Exception.Message
            Publish-SafeguardRecord $run $recordPath $security
            return
        }

        if ($ScriptId -eq 'low-power-shutdown-notify') {
            try {
                if (!$outbox) {
                    throw 'Email queue is unavailable.'
                }

                Write-SafeguardJson (Join-Path $outbox $name) @{
                    executionId = $id; bindingId = $BindingId; startedAt = $run.startedAt
                } $security
                Add-SafeguardStep $run 'Queue email' 'Queued for Windows service'
            }
            catch {
                Add-SafeguardStep $run 'Queue email' 'Could not write email request'
            }
        }

        try {
            if ($Simulate) {
                $lowPower = @{ Result = 'Simulated timeout'; ExitCode = $null }
            }
            else {
                Assert-SafeguardPath $binding.afterburnerPath
                if ((Get-FileHash -LiteralPath $binding.afterburnerPath).Hash -ne $binding.executableHash) {
                    throw 'Afterburner executable changed after configuration.'
                }

                $lowPower = Start-SafeguardProcess $binding.afterburnerPath '-lowpowerprofile -message GPUSafeguard' 5000
            }
        }
        catch {
            $lowPower = @{ Result = $_.Exception.Message; ExitCode = $null }
        }

        $delayClock = [Diagnostics.Stopwatch]::StartNew()
        Add-SafeguardStep $run 'Afterburner low-power command' $lowPower.Result $lowPower.ExitCode
        $run.status = 'Shutdown countdown'
        Publish-SafeguardRecord $run $recordPath $security

        try {
            if ($Simulate) {
                $shutdown = @{ Result = 'Simulated forced shutdown'; ExitCode = 0 }
            }
            else {
                $remaining = [Math]::Max(0, $binding.delayMs - $delayClock.ElapsedMilliseconds)
                if ($remaining -gt 0) {
                    Start-Sleep -Milliseconds ([int]$remaining)
                }

                Add-SafeguardStep $run 'Windows shutdown' 'Submitting forced shutdown request'
                $run.status = 'Submitting shutdown'
                Publish-SafeguardRecord $run $recordPath $security
                $shutdownPath = Join-Path ([Environment]::SystemDirectory) 'shutdown.exe'
                $shutdown = Start-SafeguardProcess $shutdownPath ('/s /f /t 0 /d p:0:0 /c "RTX Monitor Safeguard+ ' + $id.ToString('N') + '"') 5000
            }

            $run.status = if ($Simulate) { 'Simulated' } elseif ($shutdown.ExitCode -eq 0) { 'Shutdown requested' } else { 'Shutdown request failed' }
            Add-SafeguardStep $run 'Windows shutdown' $shutdown.Result $shutdown.ExitCode
        }
        catch {
            $run.status = 'Shutdown request failed'
            Add-SafeguardStep $run 'Windows shutdown' $_.Exception.Message
        }
        finally {
            $run.finishedAt = [DateTimeOffset]::UtcNow.ToString('O')
            Publish-SafeguardRecord $run $recordPath $security
        }

        if (!$Simulate -and $run.status -eq 'Shutdown requested') {
            # Hold the lock while Windows begins ending the session.
            Start-Sleep -Seconds 10
        }
    }
    finally {
        if ($guard) {
            $guard.Dispose()
        }
    }
}