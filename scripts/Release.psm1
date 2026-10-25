Set-StrictMode -Version Latest

function Invoke-ReleaseCommand
{
    param(
        [Parameter(Mandatory)][string] $Command,
        [Parameter(Mandatory)][string[]] $Arguments
    )

    & $Command @Arguments

    if ($LASTEXITCODE -ne 0)
    {
        throw "$Command failed with exit code $LASTEXITCODE."
    }
}

function Get-VerifiedVcRedistributable
{
    param([Parameter(Mandatory)][string] $Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf))
    {
        New-Item -ItemType Directory -Path (Split-Path -Parent $Path) -Force | Out-Null
        Invoke-WebRequest -Uri 'https://aka.ms/vs/17/release/vc_redist.x64.exe' -OutFile $Path -UseBasicParsing
    }

    $signature = Get-AuthenticodeSignature -LiteralPath $Path

    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation')
    {
        throw 'The Visual C++ redistributable must have a valid Microsoft Authenticode signature.'
    }

    $version = (Get-Item -LiteralPath $Path).VersionInfo.ProductVersion

    if ($version -notmatch '^\d+\.\d+\.\d+(\.\d+)?$')
    {
        throw "Invalid redistributable version: $version"
    }

    return $version
}

function Get-ReleaseSignTool
{
    $command = Get-Command signtool.exe -ErrorAction SilentlyContinue

    if ($command)
    {
        return $command.Source
    }

    $packages = Join-Path $env:USERPROFILE '.nuget\packages\microsoft.windows.sdk.buildtools'
    $tool = Get-ChildItem -LiteralPath $packages -Recurse -Filter signtool.exe | Where-Object { $_.Directory.Name -eq 'x64' } |
        Sort-Object FullName -Descending | Select-Object -First 1

    if (-not $tool)
    {
        throw 'Install Windows SDK SignTool or add signtool.exe to PATH.'
    }

    return $tool.FullName
}

function Write-ReleaseFileManifest
{
    param([Parameter(Mandatory)][string] $Directory)

    $root = [IO.Path]::GetFullPath($Directory).TrimEnd('\')

    $rows = foreach ($file in Get-ChildItem -LiteralPath $root -File | Where-Object { $_.Name -ne 'SHA256SUMS.txt' -and $_.Extension -ne '.zip' } | Sort-Object Name)
    {
        '{0}  {1}' -f (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $file.Name
    }

    $rows | Set-Content -LiteralPath (Join-Path $root 'SHA256SUMS.txt') -Encoding ASCII
}

Export-ModuleMember -Function Invoke-ReleaseCommand, Get-VerifiedVcRedistributable, Get-ReleaseSignTool, Write-ReleaseFileManifest
