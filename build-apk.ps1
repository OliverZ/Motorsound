param([string]$Runtime = 'android-arm64')
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$localDotnet = Join-Path $projectRoot '.build-tools\dotnet\dotnet.exe'
$dotnetCommand = 'dotnet'
$sdkProperties = @()
if (Test-Path -LiteralPath $localDotnet) {
    $dotnetCommand = $localDotnet
    $env:DOTNET_CLI_HOME = Join-Path $projectRoot '.build-tools\cli'
    $env:NUGET_PACKAGES = Join-Path $projectRoot '.build-tools\nuget'
    $sdkProperties = @(
        "-p:AndroidSdkDirectory=$projectRoot\.build-tools\android-sdk",
        "-p:JavaSdkDirectory=$projectRoot\.build-tools\jdk"
    )
}
& $dotnetCommand build (Join-Path $projectRoot 'ScooterSound\ScooterSound.csproj') `
    -t:SignAndroidPackage -c Debug -f net10.0-android `
    "-p:RuntimeIdentifier=$Runtime" -p:AndroidPackageFormats=apk `
    -p:EmbedAssembliesIntoApk=true @sdkProperties
if ($LASTEXITCODE -ne 0) { throw 'APK-Build fehlgeschlagen.' }
$apk = Get-ChildItem (Join-Path $projectRoot 'ScooterSound\bin\Debug\net10.0-android') -Recurse -Filter '*-Signed.apk' |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $apk) { throw 'Signierte APK wurde nicht gefunden.' }
$artifactDirectory = Join-Path $projectRoot 'artifacts'
New-Item -ItemType Directory -Force -Path $artifactDirectory | Out-Null
$destination = Join-Path $artifactDirectory "ScooterSound-debug-$Runtime.apk"
Copy-Item -LiteralPath $apk.FullName -Destination $destination -Force
Write-Output "APK: $destination"
