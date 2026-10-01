[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskSource = Join-Path $taskRoot 'source-art/third-party/polyhaven'
$taskFiles = @(
    @{ Asset='asphalt_04'; Name='asphalt_04_diff_1k.jpg'; Md5='729a3299145b25982a48f2f9e05e7f21'; Url='https://dl.polyhaven.org/file/ph-assets/Textures/jpg/1k/asphalt_04/asphalt_04_diff_1k.jpg' },
    @{ Asset='asphalt_04'; Name='asphalt_04_nor_gl_1k.png'; Md5='dcdf029cda36f9d405ef6669daeed436'; Url='https://dl.polyhaven.org/file/ph-assets/Textures/png/1k/asphalt_04/asphalt_04_nor_gl_1k.png' },
    @{ Asset='asphalt_04'; Name='asphalt_04_rough_1k.jpg'; Md5='a5db0aefcaf7fb3c96b275dba63f0f4e'; Url='https://dl.polyhaven.org/file/ph-assets/Textures/jpg/1k/asphalt_04/asphalt_04_rough_1k.jpg' },
    @{ Asset='industrial_sunset_puresky'; Name='industrial_sunset_puresky_2k.hdr'; Md5='54457ed6fc7e0dcd7fcdcd0603892a7d'; Url='https://dl.polyhaven.org/file/ph-assets/HDRIs/hdr/2k/industrial_sunset_puresky_2k.hdr' }
)

foreach ($taskFile in $taskFiles) {
    $taskDirectory = Join-Path $taskSource $taskFile.Asset
    New-Item -ItemType Directory -Path $taskDirectory -Force | Out-Null
    $taskPath = Join-Path $taskDirectory $taskFile.Name
    if (-not (Test-Path -LiteralPath $taskPath)) {
        $taskPartial = "$taskPath.part"
        Invoke-WebRequest -Uri $taskFile.Url -OutFile $taskPartial -UseBasicParsing -TimeoutSec 90
        $taskHash = (Get-FileHash -LiteralPath $taskPartial -Algorithm MD5).Hash.ToLowerInvariant()
        if ($taskHash -ne $taskFile.Md5) {
            throw "Provider checksum mismatch for $($taskFile.Name); the partial file was retained for inspection."
        }
        Move-Item -LiteralPath $taskPartial -Destination $taskPath
    }
    $taskHash = (Get-FileHash -LiteralPath $taskPath -Algorithm MD5).Hash.ToLowerInvariant()
    if ($taskHash -ne $taskFile.Md5) {
        throw "Existing source checksum mismatch: $($taskFile.Name). It has not been overwritten."
    }
    Write-Output "Verified $($taskFile.Name): $((Get-Item -LiteralPath $taskPath).Length) bytes."
}
