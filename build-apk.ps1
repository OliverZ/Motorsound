param(
    [string]$Runtime = 'android-arm64',
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [string]$KeyStore,
    [string]$KeyAlias = 'androiddebugkey'
)
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
$signingProperties = @()
if ($KeyStore) {
    if (-not (Test-Path -LiteralPath $KeyStore)) { throw 'Keystore wurde nicht gefunden.' }
    if (-not $env:ANDROID_SIGNING_STORE_PASSWORD -or -not $env:ANDROID_SIGNING_KEY_PASSWORD) {
        throw 'ANDROID_SIGNING_STORE_PASSWORD und ANDROID_SIGNING_KEY_PASSWORD müssen gesetzt sein.'
    }
    $signingProperties = @(
        '-p:AndroidKeyStore=true',
        "-p:AndroidSigningKeyStore=$((Resolve-Path -LiteralPath $KeyStore).Path)",
        "-p:AndroidSigningKeyAlias=$KeyAlias",
        '-p:AndroidSigningStorePass=env:ANDROID_SIGNING_STORE_PASSWORD',
        '-p:AndroidSigningKeyPass=env:ANDROID_SIGNING_KEY_PASSWORD'
    )
}
# Android's incremental signing inputs do not include the keystore. Remove only
# generated signed APKs for this build so a changed key is always applied.
$signedOutputDirectory = Join-Path $projectRoot "ScooterSound\bin\$Configuration\net10.0-android\$Runtime"
if (Test-Path -LiteralPath $signedOutputDirectory) {
    Get-ChildItem -LiteralPath $signedOutputDirectory -File -Filter '*-Signed.apk' |
        ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force }
}
& $dotnetCommand build (Join-Path $projectRoot 'ScooterSound\ScooterSound.csproj') `
    -t:SignAndroidPackage -c $Configuration -f net10.0-android `
    "-p:RuntimeIdentifier=$Runtime" -p:AndroidPackageFormats=apk `
    -p:EmbedAssembliesIntoApk=true -p:RunAOTCompilation=false `
    -p:DebugSymbols=false -p:DebugType=None @sdkProperties @signingProperties
if ($LASTEXITCODE -ne 0) { throw 'APK-Build fehlgeschlagen.' }
$apk = Get-ChildItem (Join-Path $projectRoot "ScooterSound\bin\$Configuration\net10.0-android\$Runtime") -Recurse -Filter '*-Signed.apk' |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $apk) { throw 'Signierte APK wurde nicht gefunden.' }
$artifactDirectory = Join-Path $projectRoot 'artifacts'
New-Item -ItemType Directory -Force -Path $artifactDirectory | Out-Null
$destination = Join-Path $artifactDirectory "ScooterSound-3.2-$Runtime.apk"
Copy-Item -LiteralPath $apk.FullName -Destination $destination -Force
Write-Output "APK: $destination"
