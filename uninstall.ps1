#Requires -Version 5.1
<# Opens the registered RTX Monitor uninstaller. Keeps backend configuration and desktop preferences. #>
[CmdletBinding(SupportsShouldProcess)]
param([switch] $Quiet)

$ErrorActionPreference = 'Stop'

$upgradeCode = '{63498E86-5C62-431A-B173-6DE1B4A9E617}'
$uninstallKey = 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall'
$registration = $null

foreach ($view in @([Microsoft.Win32.RegistryView]::Registry64, [Microsoft.Win32.RegistryView]::Registry32))
{
    $machine = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::LocalMachine, $view)

    try
    {
        $key = $machine.OpenSubKey($uninstallKey)

        if ($key)
        {
            try
            {
                foreach ($name in $key.GetSubKeyNames())
                {
                    $entry = $key.OpenSubKey($name)

                    try
                    {
                        if (@($entry.GetValue('BundleUpgradeCode')) -contains $upgradeCode)
                        {
                            $registration = $entry.GetValue('BundleCachePath')
                            break
                        }
                    }
                    finally
                    {
                        $entry.Dispose()
                    }
                }
            }
            finally
            {
                $key.Dispose()
            }
        }
    }
    finally
    {
        $machine.Dispose()
    }

    if ($registration)
    {
        break
    }
}

if (-not $registration)
{
    Write-Host 'RTX Monitor is not installed by this installer.'
    return
}

if (-not (Test-Path -LiteralPath $registration -PathType Leaf))
{
    throw 'The cached uninstaller is missing. Run the matching setup EXE to repair its cache, then uninstall.'
}

if ($PSCmdlet.ShouldProcess('RTX Monitor service and desktop app', 'Uninstall (keep configuration and preferences)'))
{
    $arguments = @('-uninstall', '-norestart')

    if ($Quiet)
    {
        $arguments += '-quiet'
    }

    $process = Start-Process -FilePath $registration -ArgumentList $arguments -Wait -PassThru

    if ($process.ExitCode -notin @(0, 3010))
    {
        throw "Uninstall returned exit code $($process.ExitCode)."
    }
}
