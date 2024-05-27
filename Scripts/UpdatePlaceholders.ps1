$TestBasePath = $Env:Test_base_path
$TestTimeout = $Env:Test_timeout

$RunSettingsPath = $Env:Runsettings_path
$PowerShellScriptPath =  $Env:PSScript_path


# Setup Secrets and Variables
$extension_path = $Env:EXTENSION_PATH
$account_1 = $Env:ACCOUNT_1
$account_2 = $Env:ACCOUNT_2
$user_name_1 = $Env:USER_1
$user_name_2 = $Env:USER_2
$password_1 = $Env:PASSWORD_1
$password_2 = $Env:PASSWORD_2


# Update Runsettings placeholders
$RunSettingsContent = Get-Content -Path $RunSettingsPath

$RunSettingsContent = $RunSettingsContent -replace 'name="TestBasePath" value="{TEST_BASE_PATH}"', ('name="TestBasePath" value="{0}"' -f  $TestBasePath)
$RunSettingsContent = $RunSettingsContent -replace 'name="Timeout" value="{TEST_TIMEOUT}"', ('name="Timeout" value="{0}"' -f $TestTimeout)
$RunSettingsContent | Set-Content -Path $RunSettingsPath
Get-Content -Path $RunSettingsPath

#Update ps1 placeholders
$PSScriptContent = Get-Content -Path $PowerShellScriptPath

$PSScriptContent = $PSScriptContent -replace '$extension_path = "{EXTENSION_PATH}"', ('$extension_path = "{0}"' -f $extension_path)
$PSScriptContent = $PSScriptContent -replace '$account_1 = "{ACCOUNT_1}"', ('$account_1 = "{0}"' -f  $account_1)
$PSScriptContent = $PSScriptContent -replace '$account_2 = "{ACCOUNT_2}"', ('$account_2 = "{0}"' -f  $account_2)
$PSScriptContent = $PSScriptContent -replace '$user_name_1 = "{USER_1}"', ('$user_name_1 = "{0}"' -f  $user_name_1)
$PSScriptContent = $PSScriptContent -replace '$user_name_2 = "{USER_2}"', ('$user_name_2 = "{0}"' -f  $user_name_2)
$PSScriptContent = $PSScriptContent -replace '$password_1 = "{PASSWORD_1}"', ('$password_1 = "{0}"' -f  $password_1)
$PSScriptContent = $PSScriptContent -replace '$password_2 = "{PASSWORD_2}"', ('$password_2 = "{0}"' -f  $password_2)
$PSScriptContent | Set-Content -Path $PowerShellScriptPath
Get-Content -Path $PowerShellScriptPath

