$TestBasePath = $Env:Test_base_path
$TestTimeout = $Env:Test_timeout


$RunSettingsPath = $Env:Runsettings_path
$PowerShellScriptPath =  $Env:PSScript_path

$RunSettingsContent = Get-Content -Path $RunSettingsPath


$RunSettingsContent = $RunSettingsContent -replace 'name="TestBasePath" value="{TEST_BASE_PATH}"', 'name="TestBasePath" value="$TestBasePath"'
$RunSettingsContent = $RunSettingsContent -replace 'name="Timeout" value="{TEST_TIMEOUT}"', 'name="Timeout" value="$TestTimeout"'
$RunSettingsContent | Set-Content -Path $RunSettingsPath

Get-Content -Path $RunSettingsPath
