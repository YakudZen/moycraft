param(
    [string]$UnityData = 'C:/Program Files/Unity/Hub/Editor/6000.5.0f1/Editor/Data',
    [string]$Dotnet = 'dotnet'
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$output = Join-Path $projectRoot 'Builds/SourceChecks'
New-Item -ItemType Directory -Force $output | Out-Null
$env:DOTNET_CLI_HOME = Join-Path $output 'dotnet-home'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$sdkLines = @(& $Dotnet --list-sdks)
if ($LASTEXITCODE -ne 0 -or $sdkLines.Count -eq 0) { throw 'A .NET SDK is required for source-only checks.' }
$match = [regex]::Match($sdkLines[-1], '^(\S+)\s+\[(.+)\]')
$compiler = Join-Path $match.Groups[2].Value ($match.Groups[1].Value + '/Roslyn/bincore/csc.dll')
$runtimeMajor = $match.Groups[1].Value.Split('.')[0]
$references = @(Get-ChildItem "$UnityData/NetStandard/ref/2.1.0" -Filter '*.dll')
$references += Get-ChildItem "$UnityData/Managed/UnityEngine" -Filter '*.dll'
$sources = @(Get-ChildItem "$projectRoot/Assets" -Recurse -Filter '*.cs')
$sources += Get-ChildItem "$projectRoot/Tests" -Filter '*.cs'
$assembly = Join-Path $output 'ManagedChecks.dll'
$options = @('-nologo','-target:exe','-nostdlib+','-langversion:9','-define:UNITY_EDITOR,UNITY_STANDALONE_WIN',('-out:"' + $assembly + '"'))
foreach ($file in $references) { $options += '-r:"' + $file.FullName + '"' }
foreach ($file in $sources) { $options += '"' + $file.FullName + '"' }
$response = Join-Path $output 'compile.rsp'
[IO.File]::WriteAllLines($response,$options)
& $Dotnet $compiler "@$response"
if ($LASTEXITCODE -ne 0) { throw 'Source compilation failed.' }
$config = @{runtimeOptions=@{tfm="net$runtimeMajor.0";framework=@{name='Microsoft.NETCore.App';version="$runtimeMajor.0.0"}}} | ConvertTo-Json -Depth 5
[IO.File]::WriteAllText((Join-Path $output 'ManagedChecks.runtimeconfig.json'),$config)
& $Dotnet $assembly "$UnityData/Managed/UnityEngine"
if ($LASTEXITCODE -ne 0) { throw 'Managed checks failed.' }
Write-Output 'SOURCE_AND_MANAGED_CHECKS_OK'
