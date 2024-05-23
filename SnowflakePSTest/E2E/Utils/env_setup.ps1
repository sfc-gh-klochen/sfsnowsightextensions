echo "-- Starting Environment Setup --"
$extension_path = "/Users/lcuelloayala/Documents/Repositories/SNOWSIGHT_EXTENSION/OfficialSnowsightExtension/sfsnowsightextensions/SnowflakePS/bin/Debug/net6.0"
$account_1 = "IJB24494"
$account_2 = "IJB24494"
$user_name_1 = "QA_USER_1"
$user_name_2 = "QA_USER_2"
$password_1 = "Password2024"
$password_2 = "Password2024"

cd $extension_path
import-Module ./SnowflakePS.psd1

echo "-- Environment Setup Finished -- "

function sfAppConnectionAccount1 (){
    Connect-SFApp -Account $account_1 -UserName $user_name_1 -Password (ConvertTo-SecureString -String "$password_1" -AsPlainText)
}

function sfAppConnectionAccount2 (){
    Connect-SFApp -Account $account_2 -UserName $user_name_2 -Password (ConvertTo-SecureString -String "$password_2" -AsPlainText)
}

function cleanupWorksheetsFromAccount2 () {
    echo "-- Starting worksheet cleanup process for account 2 --"
    $worksheets2 = Get-SFWorksheets -AuthContext $account2Context

    $worksheets2 | ForEach-Object {Remove-SFWorksheet -AuthContext $account2Context -Worksheet $_ }
    echo "-- Cleanup process for account 2 worksheets finished --"
}

function migrateWorksheetsFromAccount1ToAccount2Overwrite (){
    echo "-- Migrating worksheets from account 1 to account 2 (Overwrite) --"
    $account1Context = sfAppConnectionAccount1
    $account2Context = sfAppConnectionAccount2

    $worksheets = Get-SFWorksheets -AuthContext $account1Context
    $worksheets | Foreach-Object {New-SFWorksheet -AuthContext $account2Context -Worksheet $_ -ActionIfExists CreateNew}
    $worksheets | Foreach-Object {New-SFWorksheet -AuthContext $account2Context -Worksheet $_ -ActionIfExists Overwrite}
    echo "-- Worksheets migration finished --"
    
    cleanupWorksheetsFromAccount2
}

function migrateWorksheetsFromAccount1ToAccount2CreateNew () {
    echo "-- Migrating worksheets from account 1 to account 2 (CreateNew) --"
    $account1Context = sfAppConnectionAccount1
    $account2Context = sfAppConnectionAccount2

    $worksheets = Get-SFWorksheets -AuthContext $account1Context
    $worksheets | Foreach-Object {New-SFWorksheet -AuthContext $account2Context -Worksheet $_ -ActionIfExists CreateNew}
    echo "-- Worksheets migration finished --"

    cleanupWorksheetsFromAccount2
}

function migrateWorksheetsFromAccount1ToAccount2CreateNewWithNewName () {
    echo "-- Migrating worksheets from account 1 to account 2 (CreateNewWithNewName) --"
    $account1Context = sfAppConnectionAccount1
    $account2Context = sfAppConnectionAccount2

    $worksheets = Get-SFWorksheets -AuthContext $account1Context
    $worksheets | Foreach-Object {New-SFWorksheet -AuthContext $account2Context -Worksheet $_ -ActionIfExists CreateNew}
    $worksheets | Foreach-Object {New-SFWorksheet -AuthContext $account2Context -Worksheet $_ -ActionIfExists CreateNew}
    echo "-- Worksheets migration finished --"

    cleanupWorksheetsFromAccount2
}

function migrateWorksheetsFromAccount1ToAccount2Skip () {
    echo "-- Migrating worksheets from account 1 to account 2 (Skip) --"
    $account1Context = sfAppConnectionAccount1
    $account2Context = sfAppConnectionAccount2

    $worksheets = Get-SFWorksheets -AuthContext $account1Context
    $worksheets | Foreach-Object {New-SFWorksheet -AuthContext $account2Context -Worksheet $_ -ActionIfExists CreateNew}
    $worksheets | Foreach-Object {New-SFWorksheet -AuthContext $account2Context -Worksheet $_ -ActionIfExists Skip}
    echo "-- Worksheets migration finished --"

    cleanupWorksheetsFromAccount2
}