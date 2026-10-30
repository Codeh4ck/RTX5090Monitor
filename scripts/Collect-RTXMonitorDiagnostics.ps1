#Requires -Version 5.1
<#
.SYNOPSIS
Collects a read-only RTX Monitor service diagnostic report.
.DESCRIPTION
Works with Windows PowerShell 5.1 and PowerShell 7. No administrator rights,
desktop app or additional modules are required. Calls only GET endpoints and
receives WebSocket messages; it never updates configuration or controls hardware.
.EXAMPLE
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Collect-RTXMonitorDiagnostics.ps1
#>
[CmdletBinding()]
param(
    [Uri] $ServiceAddress = 'http://localhost:59273/',
    [ValidateRange(1, 100)][int] $Samples = 5,
    [ValidateRange(1, 120)][int] $TimeoutSeconds = 20,
    [string] $OutputDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0
Add-Type -AssemblyName System.Net.Http

if ($ServiceAddress.Scheme -notin @('http', 'https') -or -not $ServiceAddress.IsLoopback -or
    $ServiceAddress.AbsolutePath -ne '/' -or $ServiceAddress.UserInfo -or
    $ServiceAddress.Query -or $ServiceAddress.Fragment)
{
    throw 'ServiceAddress must be a local service root, for example http://localhost:59273/.'
}

if (-not $OutputDirectory)
{
    $OutputDirectory = [Environment]::GetFolderPath('Desktop')

    if (-not $OutputDirectory)
    {
        $OutputDirectory = (Get-Location).Path
    }
}

$reportName = 'RTXMonitor-Diagnostics-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 6)
$reportDirectory = Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) $reportName
$null = [IO.Directory]::CreateDirectory($reportDirectory)
$errors = [Collections.Generic.List[string]]::new()
$endpoints = [Collections.Generic.List[object]]::new()
$frames = [Collections.Generic.List[object]]::new()
$utf8 = [Text.UTF8Encoding]::new($false)

function Write-ReportText
{
    param([string] $Name, [string] $Text)

    [IO.File]::WriteAllText((Join-Path $reportDirectory $Name), $Text, $utf8)
}

function Get-Field
{
    param($Object, [string] $Name)

    if ($null -ne $Object)
    {
        $property = $Object.PSObject.Properties[$Name]

        if ($null -ne $property)
        {
            return $property.Value
        }
    }

    return $null
}

$handler = [Net.Http.HttpClientHandler]::new()
$handler.UseProxy = $false
$http = [Net.Http.HttpClient]::new($handler)
$http.Timeout = [TimeSpan]::FromSeconds(5)

function Read-ServiceJson
{
    param([string] $Path, [string] $Filename)

    Write-Host ('Reading ' + $Path)
    $response = $null

    try
    {
        $uri = [Uri]::new($ServiceAddress, $Path.TrimStart('/'))
        $response = $http.GetAsync($uri).GetAwaiter().GetResult()
        $text = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        Write-ReportText $Filename $text
        $endpoints.Add([pscustomobject]@{ Path = $Path; Status = [int]$response.StatusCode })

        if (-not $response.IsSuccessStatusCode)
        {
            $errors.Add(('GET {0}: HTTP {1} {2}' -f $Path, [int]$response.StatusCode, $response.ReasonPhrase))
            return $null
        }

        return ($text | ConvertFrom-Json)
    }
    catch
    {
        $errors.Add(('GET {0}: {1}' -f $Path, $_.Exception.Message))
        return $null
    }
    finally
    {
        if ($null -ne $response)
        {
            $response.Dispose()
        }
    }
}

Write-Host 'RTX Monitor diagnostics - read only'
Write-Host ('Service: ' + $ServiceAddress.AbsoluteUri)

try
{
    $null = Read-ServiceJson '/version' 'version.json'
    $null = Read-ServiceJson '/health' 'health.json'
    $null = Read-ServiceJson '/plugins' 'plugins.json'
    $nvidiaConfig = Read-ServiceJson '/plugins/generic-nvidia/config' 'nvidia-config.json'
    $null = Read-ServiceJson '/plugins/wireview-pro2/config' 'wireview-config.json'
    $null = Read-ServiceJson '/plugins/wireview-pro2/sensors' 'wireview-sensors.json'
}
finally
{
    $http.Dispose()
}

$socket = [Net.WebSockets.ClientWebSocket]::new()
$socket.Options.Proxy = $null
$deadline = [Threading.CancellationTokenSource]::new()
$deadline.CancelAfter([TimeSpan]::FromSeconds($TimeoutSeconds))
$webSocketUri = [UriBuilder]::new([Uri]::new($ServiceAddress, 'sensors'))
$webSocketUri.Scheme = if ($ServiceAddress.Scheme -eq 'https') { 'wss' } else { 'ws' }

Write-Host ('Receiving up to {0} broadcast frames (maximum {1} seconds)...' -f $Samples, $TimeoutSeconds)

try
{
    $null = $socket.ConnectAsync($webSocketUri.Uri, $deadline.Token).GetAwaiter().GetResult()
    $buffer = New-Object byte[] 16384
    $segment = [ArraySegment[byte]]::new($buffer)

    while ($frames.Count -lt $Samples)
    {
        $message = [IO.MemoryStream]::new()

        try
        {
            do
            {
                $result = $socket.ReceiveAsync($segment, $deadline.Token).GetAwaiter().GetResult()

                if ($result.MessageType -eq [Net.WebSockets.WebSocketMessageType]::Close)
                {
                    throw 'The service closed the broadcast stream.'
                }

                if ($result.MessageType -ne [Net.WebSockets.WebSocketMessageType]::Text)
                {
                    throw 'The service sent an unexpected non-text broadcast message.'
                }

                $message.Write($buffer, 0, $result.Count)

                if ($message.Length -gt 2MB)
                {
                    throw 'A broadcast message exceeded the 2 MB diagnostic limit.'
                }
            }
            while (-not $result.EndOfMessage)

            $text = [Text.Encoding]::UTF8.GetString($message.ToArray())
            $name = 'snapshot-{0:D3}.json' -f ($frames.Count + 1)
            Write-ReportText $name $text
            $frame = $text | ConvertFrom-Json
            $frames.Add($frame)
            Write-Host ('  Captured frame {0}/{1}' -f $frames.Count, $Samples)
        }
        finally
        {
            $message.Dispose()
        }
    }
}
catch
{
    if ($deadline.IsCancellationRequested)
    {
        $errors.Add(('Broadcast capture timed out after {0} seconds; collected {1}/{2} frames.' -f $TimeoutSeconds, $frames.Count, $Samples))
    }
    else
    {
        $errors.Add(('Broadcast capture: ' + $_.Exception.Message))
    }
}
finally
{
    if ($socket.State -eq [Net.WebSockets.WebSocketState]::Open -or
        $socket.State -eq [Net.WebSockets.WebSocketState]::CloseReceived)
    {
        $closeDeadline = [Threading.CancellationTokenSource]::new()
        $closeDeadline.CancelAfter([TimeSpan]::FromSeconds(2))

        try
        {
            $null = $socket.CloseAsync([Net.WebSockets.WebSocketCloseStatus]::NormalClosure, 'Diagnostics complete', $closeDeadline.Token).GetAwaiter().GetResult()
        }
        catch
        {
            # Captured data remains useful even if the close handshake times out.
        }
        finally
        {
            $closeDeadline.Dispose()
        }
    }

    $socket.Dispose()
    $deadline.Dispose()
}

$electricalSource = Get-Field (Get-Field $nvidiaConfig 'options') 'electricalSource'

if (-not $electricalSource)
{
    $electricalSource = 'Automatic (default, or configuration unavailable)'
}

$rows = [Collections.Generic.List[object]]::new()

if ($frames.Count -gt 0)
{
    $latest = $frames[$frames.Count - 1]
    $sensors = @(Get-Field $latest 'sensors')

    foreach ($pluginId in @('generic-nvidia', 'wireview-pro2'))
    {
        foreach ($metric in @('Power', 'Current', 'Voltage'))
        {
            $matches = @($sensors | Where-Object {
                $null -ne $_ -and $_.identity.pluginId -eq $pluginId -and
                $_.identity.component -eq 'GpuConnector' -and $_.identity.channel -eq 1 -and
                $_.identity.metric -eq $metric
            })

            if ($matches.Count -eq 0)
            {
                $rows.Add([pscustomobject]@{ Plugin = $pluginId; Metric = $metric; Value = $null; Unit = ''; Quality = 'Not published'; ObservedAt = $null })
            }
            else
            {
                foreach ($reading in $matches)
                {
                    $rows.Add([pscustomobject]@{ Plugin = $pluginId; Metric = $metric; Value = $reading.value; Unit = $reading.unit; Quality = $reading.quality; ObservedAt = $reading.observedAt })
                }
            }
        }
    }
}

$summary = @(
    'RTX Monitor service diagnostics'
    ('Captured at: ' + [DateTimeOffset]::Now.ToString('o'))
    ('PowerShell: ' + $PSVersionTable.PSVersion.ToString())
    ('Service: ' + $ServiceAddress.AbsoluteUri)
    ('Broadcast frames captured: ' + $frames.Count)
    ('GPU status electrical source: ' + $electricalSource)
    ''
    'In v1.0.2, Automatic uses native GPU connector readings; it does not fall back to WireView.'
    'Select WireView in Nvidia configuration if it monitors the selected GPU.'
    'Core voltage is a separate GPU measurement, not the connector input voltage.'
    ''
    'Latest broadcast electrical readings (connector channel 1):'
    ($rows | Format-Table Plugin, Metric, Value, Unit, Quality, ObservedAt -AutoSize | Out-String -Width 180)
    'HTTP results:'
    ($endpoints | Format-Table -AutoSize | Out-String -Width 180)
    'Capture errors (disabled or absent plugins may return an error):'
    ($errors -join [Environment]::NewLine)
    ''
    'No configuration or hardware settings were changed.'
    'Raw snapshots include reported device identifiers, names, sensor values and warnings.'
)

Write-ReportText 'summary.txt' ($summary -join [Environment]::NewLine)
Write-ReportText 'electrical-readings.json' (ConvertTo-Json -InputObject $rows.ToArray() -Depth 10)
Write-ReportText 'capture-errors.txt' ($errors -join [Environment]::NewLine)

$zipPath = $reportDirectory + '.zip'
$files = @(Get-ChildItem -LiteralPath $reportDirectory -File)
Compress-Archive -LiteralPath $files.FullName -DestinationPath $zipPath -CompressionLevel Optimal

Write-Host ''
Write-Host ('GPU status electrical source: ' + $electricalSource)

if ($rows.Count -gt 0)
{
    $rows | Format-Table Plugin, Metric, Value, Unit, Quality -AutoSize | Out-Host
}

if ($errors.Count -gt 0)
{
    Write-Warning 'Some data could not be collected. Details are included in the report.'
}

Write-Host ('Send this ZIP back: ' + $zipPath) -ForegroundColor Green
