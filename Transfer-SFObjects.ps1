function Transfer-SFObjects ()
{
    [CmdletBinding()]
    param (
        [Parameter(Mandatory)] [String]$SFObjectTypes,
        [Parameter(Mandatory)] [String]$SourceAccountLocatorOrFilepath,
        [Parameter()] [String]$TargetAccounts,
        [Parameter()] [String]$SFRole='ACCOUNTADMIN',
        [Parameter()] [String]$SFWarehouse='COMPUTE_WH',
        [Parameter()] [String]$SFDatabase,
        [Parameter()] [String]$SFSchema,
        [Parameter()] [String]$OutputDirectory,
        [Parameter()] [ValidateSet("CreateNew", "Skip")] [String]$ActionIfExistsDashboard='Skip',
        [Parameter()] [ValidateSet("Skip", "Overwrite")] [String]$ActionIfExistsFilter='Skip',
        [Parameter()] [ValidateSet("CreateNew", "Skip", "Overwrite")] [String]$ActionIfExistsWorksheet='Skip',
        [Parameter()] [bool]$SSO=$false,
        [Parameter()] [bool]$CleanSourceFiles=$false,
        [Parameter()] [bool]$InteractiveMode=$false
    )

    # Build a hashtable of values for iterating through in target account uploads
    $SourceReplacementValues = @{
        Role = $SFRole
        Warehouse = $SFWarehouse
        Database = $SFDatabase
        Schema = $SFSchema
    }

    # TODO: DELETE
    # RenameTargetObjectsPrompt($SourceReplacementValues, "test_acct_name")

    # Retrieve Source Account Objects
    if (-Not (Test-Path $SourceAccountLocatorOrFilepath)) {        
        # If InteractiveMode, prompt for SSO Input per every source and target account
        if ($InteractiveMode){
            if (SSO-Prompt($SourceAccountLocatorOrFilepath)){ 
            $SourceContext =  Connect-SFApp -Account $SourceAccountLocatorOrFilepath -SSO
            }
            else {
            $SourceContext =  Connect-SFApp -Account $SourceAccountLocatorOrFilepath
            }
        }
        else {
            if ($SSO){ 
                $SourceContext =  Connect-SFApp -Account $SourceAccountLocatorOrFilepath -SSO
                }
            else {
                $SourceContext =  Connect-SFApp -Account $SourceAccountLocatorOrFilepath
                }
        }

        # OutputDirectory used to create an account specific OutPath aka MyOutputDirectory/MyAcctLocator123/[filters,dashboards,worksheets]
        if ($OutputDirectory) {
            $OutPath = "$OutputDirectory/$SourceAccountLocatorOrFilepath"
        }

        # Import Each Object Type
        $SFObjectTypes -Split ',' | ForEach-Object {
            $obj = $_.Trim().ToLower()
            Write-Host "`r`nRetreiving $obj from source account.`r`n" -ForegroundColor Cyan
            
            # Pull down objects + update object wh, role, db, schema
            if ($obj -eq "all") {
                $SourceFilters = Get-SFFilters -AuthContext $SourceContext 
                $SourceFilters = $SourceFilters | Where-Object { $_.Scope  -ne 'global'} # Ensure the global snowflake filters are not retrieved at all
                $SourceDashboards = Get-SFDashboards -AuthContext $SourceContext
                $SourceWorksheets = Get-SFWorksheets -AuthContext $SourceContext

                # If an OutputDirectory is provided, write to it
                if ($OutPath){
                    # Save the source objects as files
                    $SourceFilters | foreach {$_.SaveToFolder("$OutPath/filters")}
                    $SourceDashboards | foreach {$_.SaveToFolder("$OutPath/dashboards")}
                    $SourceWorksheets | foreach {$_.SaveToFolder("$OutPath/worksheets")}

                    # if CleanSourceFiles, rename and clean the source account file.
                    if ($CleanSourceFiles){
                        Invoke-Command -ScriptBlock { Update-SFDocuments -SFObjectTypes $SFObjectTypes -SFRole $SFRole  -SFWarehouse $SFWarehouse -WorksheetsPath "$OutPath/worksheets" -DashboardsPath "$OutPath/dashboards" -FiltersPath "$OutPath/filters" -SFDatabase $SFDatabase -SFSchema $SFSchema 
                        }
                    }
                }
            }
            # Individual object handling
            elseif($obj -eq "filters") {
                $SourceFilters = Get-SFFilters -AuthContext $SourceContext
                $SourceFilters = $SourceFilters | Where-Object { $_.Scope -ne 'global'}
                if ($OutPath){
                    $SourceFilters | foreach {$_.SaveToFolder("$OutPath/filters")}
                    
                    if ($CleanSourceFiles) {
                        Invoke-Command -ScriptBlock { Update-SFDocuments -SFObjectTypes $SFObjectTypes -SFRole $SFRole -SFWarehouse $SFWarehouse -DashboardsPath "$OutPath/dashboards" -FiltersPath "$OutPath/filters" -SFDatabase $SFDatabase -SFSchema $SFSchema 
                        }
                    }
                }
            }
            elseif ($obj -eq "dashboards") {
                $SourceDashboards = Get-SFDashboards -AuthContext $SourceContext
                if ($OutPath){
                    $SourceDashboards | foreach {$_.SaveToFolder("$OutPath/dashboards")}

                    if ($CleanSourceFiles) {
                        Invoke-Command -ScriptBlock { Update-SFDocuments -SFObjectTypes $SFObjectTypes -SFRole $SFRole  -SFWarehouse $SFWarehouse -DashboardsPath "$OutPath/dashboards" -SFDatabase $SFDatabase -SFSchema $SFSchema 
                        }
                    }
                }
            }
            elseif ($obj -eq "worksheets") {
                $SourceWorksheets = Get-SFWorksheets -AuthContext $SourceContext
                if ($OutPath){
                    $SourceWorksheets | foreach {$_.SaveToFolder("$OutPath/worksheets")}

                    if ($CleanSourceFiles) {
                        Invoke-Command -ScriptBlock { Update-SFDocuments -SFObjectTypes $SFObjectTypes -SFRole $SFRole  -SFWarehouse $SFWarehouse -WorksheetsPath "$OutPath/worksheets" -SFDatabase $SFDatabase -SFSchema $SFSchema 
                        }
                    }
                }
            }
            else {
                Write-Host "$obj not a valid object. Use 'Filters, Dashboards, Worksheets, a combination of the three in a comma seperated list, or All.'`r`n"
            }
        }
        if ($TargetAccounts) {
            # Begin the upload to target account process
            $TargetAccounts -Split ',' | ForEach-Object {
                $TargetAccountLocator = $_
                $TargetPath = "$OutPath/$TargetAccountLocator"
                if ($InteractiveMode) {
                    # Prompt for replacement values and SSO if InteractiveMode  
                    $TargetReplacementValues = RenameTargetObjectsPrompt($SourceReplacementValues, $TargetAccountLocator)
                    
                    if (SSO-Prompt($TargetAccountLocator)){
                        $TargetContext =  Connect-SFApp -Account $TargetAccountLocator -SSO
                        }
                        else {
                        $TargetContext =  Connect-SFApp -Account $TargetAccountLocator
                        }
                    }
                else {
                    $TargetReplacementValues = $SourceReplacementValues

                    if ($SSO){ 
                        $TargetContext =  Connect-SFApp -Account $TargetAccountLocator -SSO
                        }
                    else {TargetContext
                        $SourceContext =  Connect-SFApp -Account $TargetAccountLocator
                        }
                    }
               
                # Parse through SFObjects for upload
                $SFObjectTypes -Split ',' | ForEach-Object {
                    $obj = $_.Trim().ToLower()
                    if ($obj -eq "all") {
                        foreach ($f in $SourceFilters){
                            Update-Filter-Object($f, $TargetReplacementValues)
                            New-SFFilter -AuthContext $TargetContext -Filter $f -ActionIfExists $ActionIfExistsFilter
                            
                        }
        
                        foreach ($f in $SourceDashboards){
                            Update-Dashboard-Object($f, $TargetReplacementValues)
                            New-SFDashboard -AuthContext $TargetContext -Dashboard $f -ActionIfExists $ActionIfExistsDashboard
                        }
                        foreach ($f in $SourceWorksheets){
                            Update-Worksheet-Object($f, $TargetReplacementValues)
                            New-SFWorksheet -AuthContext $TargetContext -Worksheet $f -ActionIfExists $ActionIfExistsWorksheet
                        }
                    }
                    elseif ($obj -eq "filters") {
                        foreach ($f in $SourceFilters){
                            Update-Filter-Object($f, $TargetReplacementValues)
                            New-SFFilter -AuthContext $TargetContext -Filter $f -ActionIfExists $ActionIfExistsFilter

                        }
                    }
                    elseif ($obj -eq "dashboards") {
                        foreach ($f in $SourceDashboards){
                            Update-Dashboard-Object($f, $TargetReplacementValues)
                            New-SFDashboard -AuthContext $TargetContext -Dashboard $f -ActionIfExists $ActionIfExistsDashboard
                        }
                    }
                    elseif ($obj -eq "worksheets") {
                        foreach ($f in $SourceWorksheets){
                            Update-Dashboard-Object($f, $TargetReplacementValues)
                            New-SFWorksheet -AuthContext $TargetContext -Worksheet $f -ActionIfExists $ActionIfExistsWorksheet
                        }
                    }
                    else {
                        Write-Host "$obj not a valid object. Use 'Filters, Dashboards, Worksheets, a combination of the three in a comma seperated list, or All.'`r`n"
                    } 
                }
            }
        }
        # No target account provided
        else { Write-Host "`r`nNo target accounts supplied, ending program run successfully." -ForegroundColor Cyan }
    }
    # Begin Filepath Logic
    else {
        if ($OutputDirectory) {
            $OutPath = $OutputDirectory
        }
        else {
            $OutPath = $SourceAccountLocatorOrFilepath
        }
        if ($TargetAccounts){
            $TargetAccounts -Split ',' | ForEach-Object {
                $TargetAccountLocator = $_
                $TargetPath = "$OutPath/$TargetAccountLocator"
                if ($InteractiveMode) {
                    # Prompt for replacement values and SSO if InteractiveMode  
                    $TargetReplacementValues = RenameTargetObjectsPrompt($SourceReplacementValues, $TargetAccountLocator)
                    
                    if (SSO-Prompt($TargetAccountLocator)){
                        $TargetContext =  Connect-SFApp -Account $TargetAccountLocator -SSO
                        }
                        else {
                        $TargetContext =  Connect-SFApp -Account $TargetAccountLocator
                        }
                    }
                else {
                    $TargetReplacementValues = $SourceReplacementValues

                    if ($SSO){ 
                        $TargetContext =  Connect-SFApp -Account $TargetAccountLocator -SSO
                        }
                    else {TargetContext
                        $SourceContext =  Connect-SFApp -Account $TargetAccountLocator
                        }
                    }

                $SFObjectTypes -Split ',' | ForEach-Object {
                    $obj = $_.Trim().ToLower() 
                            
                    if ($obj -eq "all") {
                        $TargetFilters = Get-ChildItem "$TargetPath/filters" -recurse -exclude '*.daterange.*','*.datebucket.*','*.timezone.*'
                        $TargetDashboards = Get-ChildItem "$TargetPath/dashboards"
                        $TargetWorksheets = Get-ChildItem "$TargetPath/worksheets"
                            
                        foreach ($f in $TargetFilters){
                            New-SFFilter -AuthContext $TargetContext -FilterFile $f.FullName -ActionIfExists $ActionIfExistsFilter
                        }

                        foreach ($f in $TargetDashboards){
                            New-SFDashboard -AuthContext $TargetContext -DashboardFile $f.FullName -ActionIfExists $ActionIfExistsDashboard
                        }

                        foreach ($f in $TargetWorksheets){
                            New-SFWorksheet -AuthContext $TargetContext -WorksheetFile $f.FullName -ActionIfExists $ActionIfExistsWorksheet
                        }
                    }
                    elseif ($obj -eq "filters") {
                        Invoke-Command -ScriptBlock { Update-SFDocuments -SFObjectTypes $SFObjectTypes -SFRole $SFRole  -SFWarehouse $SFWarehouse -FiltersPath "$SourceAccountLocatorOrFilepath/filters" -SFDatabase $SFDatabase -SFSchema $SFSchema -OutputDirectory $TargetPath
                        }
                        $TargetFilters = Get-ChildItem "$TargetPath/filters" -recurse -exclude '*.daterange.*','*.datebucket.*','*.timezone.*'

                        foreach ($f in $TargetFilters){
                            New-SFFilter -AuthContext $TargetContext -FilterFile $f.FullName -ActionIfExists $ActionIfExistsFilter
                        }
                    }
                    elseif ($obj -eq "dashboards") {
                        Invoke-Command -ScriptBlock { Update-SFDocuments -SFObjectTypes $SFObjectTypes -SFRole $SFRole  -SFWarehouse $SFWarehouse -DashboardsPath "$SourceAccountLocatorOrFilepath/dashboards" -SFDatabase $SFDatabase -SFSchema $SFSchema -OutputDirectory $TargetPath
                        }
                        $TargetDashboards = Get-ChildItem "$TargetPath/dashboards"

                        foreach ($f in $TargetDashboards){
                            New-SFDashboard -AuthContext $TargetContext -DashboardFile $f.FullName -ActionIfExists $ActionIfExistsDashboard
                        }
                    }
                    elseif ($obj -eq "worksheets") {
                        Invoke-Command -ScriptBlock {Update-SFDocuments -SFObjectTypes $SFObjectTypes -SFRole $SFRole -SFWarehouse $SFWarehouse -WorksheetsPath $SourceAccountLocatorOrFilepath/worksheets -SFDatabase $SFDatabase -SFSchema $SFSchema -OutputDirectory $TargetPath
                        }
                        $TargetWorksheets = Get-ChildItem "$TargetPath/worksheets"

                        foreach ($f in $TargetWorksheets){
                            New-SFWorksheet -AuthContext $TargetContext -WorksheetFile $f.FullName -ActionIfExists $ActionIfExistsWorksheet
                        }
                    }
                    else {
                        Write-Host "$obj not a valid object. Use 'Filters, Dashboards, Worksheets, a combination of the three in a comma seperated list, or All.'`r`n"
                    }
                # If no output directory was provided, delete the target account dir
                if (-Not($OutputDirectory)) {
                    Remove-Item $TargetPath -Recurse
                    }
                }
            }
        # No TargetAccounts Supplied
        }
        else { Write-Host "`r`nNo target accounts supplied, ending program run successfully." -ForegroundColor Cyan }
}


<#
.SYNOPSIS
Transfer-SFObjects is a wrapper module which invokes several other modules in sequence including:
Import-Dashboards
Update-SFDocuments.ps1
taks a single account locator or filepath to existing files pulled from an account and uploads the specified objects to one or many accounts. 
You can also run it without any target accounts to simply pull down files from the source account.

.DESCRIPTION
Use this function for taking snowsight objects between accounts in a one line command. 

.PARAMETER SFObjectTypes
Required [string]: SFObjectTypes specifies the object type being updated. Filters, Dashboards, Worksheets, or or a combination of the three (do not use all with the others or duplicates will be created) can be entered.
Choose All to update Filters, Dashboards, and Worksheets at the same time. Casing, ordering, and spaces do not matter for this argument do not matter. 'filter,dashboard,worksheet' = 'Worksheet, Dashboard, Filter'."

.Parameter SourceAccountLocatorOrFilepath
Required [string]: Contains account locator or file path of the source objects you want to transfer to target accounts. If supplied with a value that is not a file path, this will evaluate as an account locator and attempt to connect. See the -Connect-SFApp method for more details on how connections will be made.
When supplied with a value that is a valid path on your filesystem, this paremeter will evaluate this path and use it to read in files from the following subdirectories:
/filters, /dashboards, /worksheets.

.Parameter TargetAccounts
Optional [string]: A comma separated string with the account locators you wish to upload snowflake objects from SourceAccountLocatorOrFilepath to.
Ex: 'xy12345.us-east-1,MyGCPAcct.us-central1,xy12345.west-us-2.azure,MyPrivateLinkAcct.privatelink.snowflakecomputing.com'

.PARAMETER SFRole
Optional [string]: The name of the Snowflake Role required to run the specific object in target accounts or cleaned source files. Default is ACCOUNTADMIN. This value can be changed within the program with InteractiveMode set to true.

.PARAMETER SFWarehouse
Optional [string]: The name of the Snowflake virtual warehouse required to run the specific object in target accounts or cleaned source files for context. Default is COMPUTE_WH. This value can be changed within the program with InteractiveMode set to true.

.PARAMETER SFDatabase
Optional [string]: The name of the database to use in target accounts or cleaned source files for context. This value can be changed within the program with InteractiveMode set to true.

.PARAMETER SFSchema
Optional [string]: The name of the schema to use in target accounts or cleaned source files for for context. This value can be changed within the program with InteractiveMode set to true.

.PARAMETER OutputDirectory
Optional [string]: A parent level dir determining where source and target accounts should create sub dirs. Subdirs will be whichever objects are provided in ObjectTypes (worksheets/,dashboards/,filters/)

.PARAMETER ActionIfExistsDashboard
Optional [bool] {CreateNew, Skip}: The desired behavior when a Dashboard with the same name is found. Default is Skip. 

.PARAMETER ActionIfExistsFilter
Optional [bool] {Overwrite, Skip}: The desired behavior when a Filter with the same name is found. Default is Skip.

.PARAMETER ActionIfExistsWorksheet
Optional [bool] {Overwrite, CreateNew, Skip}: The desired behavior when a Worksheet with the same name is found. Default is Skip.

.PARAMETER CleanSourceFiles
Optional [bool]: Determines whether files pulled down from a source account are 'cleaned' of identifiable information including account locator, urls, role, warehouse, database, and schema. Default is false.

.PARAMETER SSO
Optional [bool]: Required when not in InteractiveMode, else the user will be prompted on an account by account basis for target accounts. default is False.

.PARAMETER InteractiveMode
Optional [bool]: InteractiveMode specifies that the user can provide inputs during runtime for values such as the context values and SSOs.


.INPUTS
The program will take input. You cannot pipe objects to Transfer-Objects.

.OUTPUTS
Directories with files for the relevant SFObjectTypes in the OutputDirectory or PWD

.EXAMPLE
---------------- All (AWS Account Locator) ----------------
Transfer-Objects -SFObjectTypes 'all' -SourceAccountLocatorOrFilepath 'MyAWSSourceAcct123.us-east-1'  -TargetAccounts 'xy12345.us-east-1,MyGCPAcct.us-central1,xy12345.west-us-2.azure,MyAWSPrivateLinkAcct.us-east-1.privatelink.snowflakecomputing.com' -SFRole TEST_ROLE -SFWarehouse TEST_WH -SFDatabase TEST_DB -SFSchema TEST_SCHEMA -OutputDirectory 'path/to/my/output/dir'

.EXAMPLE
---------------- Filters (Filepath) ----------------
Transfer-Objects -SFObjectTypes 'filters' -TargetAccounts 'xy12345.us-east-2.aws,MyGCPAcct.us-central1,xy12345.west-us-2.azure,MyAWSPrivateLinkAcct.us-east-1.privatelink.snowflakecomputing.com' -SFRole TEST_ROLE -SFWarehouse TEST_WH -SFDatabase TEST_DB -SFSchema TEST_SCHEMA -OutputDirectory 'path/to/my/output/dir'

.EXAMPLE
---------------- Dashboard (Azure & Privatelink AccountLocator) ----------------
Transfer-Objects -SFObjectTypes 'dashboards' -SourceAccountLocatorOrFilepath 'MyAzureSourceAcct123.MyAWSPrivateLinkAcct.west-us-2.azure.privatelink.snowflakecomputing.com'  -TargetAccounts 'xy12345.us-east-1,MyGCPAcct.us-central1.gcp,xy12345.west-us-2.azure,MyAWSPrivateLinkAcct.us-east-1.privatelink.snowflakecomputing.com' -SFRole TEST_ROLE -SFWarehouse TEST_WH -SFDatabase TEST_DB -SFSchema TEST_SCHEMA -OutputDirectory 'path/to/my/output/dir'

.EXAMPLE
---------------- Worksheet (Filepath) ----------------
Transfer-Objects -SFObjectTypes 'worksheets' -SourceAccountLocatorOrFilepath 'path/to/my/acct/object/dir' -TargetAccounts 'xy12345.us-east-1,MyGCPAcct.us-central1.gcp,xy12345.west-us-2.azure,MyAWSPrivateLinkAcct.us-east-1.privatelink.snowflakecomputing.com' -SFRole TEST_ROLE -SFRole TEST_ROLE -SFWarehouse TEST_WH -SFDatabase TEST_DB -SFSchema TEST_SCHEMA -OutputDirectory 'path/to/my/output/dir'

.EXAMPLE
---------------- Only Pull Down and Update Files, No TargetAccount Argument ----------------
Transfer-Objects -SFObjectTypes 'filters, dashboards, worksheets' -SourceAccountLocatorOrFilepath 'path/to/my/acct/object/dir' -SFRole TEST_ROLE  -SFWarehouse TEST_WH -SFDatabase TEST_DB -SFSchema TEST_SCHEMA

.EXAMPLE
---------------- Mix of assets, 'all', Filepath ----------------
Transfer-Objects -SFObjectTypes 'filters, dashboards, worksheets' -SourceAccountLocatorOrFilepath 'path/to/my/acct/object/dir' -TargetAccounts 'xy12345.us-east-1.gcp,MyGCPAcct.us-central1,xy12345.west-us-2.azure,MyAWSPrivateLinkAcct.us-east-1.privatelink.snowflakecomputing.com' -SFRole TEST_ROLE  -SFWarehouse TEST_WH -SFDatabase TEST_DB -SFSchema TEST_SCHEMA

#>
}

function Update-Filter-Object ($fparam, $values)
{
    $fparam.update | % { #Manual Filter Update
        if($fparam.Type -eq 'manual'){
            $fparam.Role = $values.Role
            $fparam.Warehouse = $values.Warehouse
            $fparam.Configuration.context.role = $values.Role
            $fparam.Configuration.context.warehouse = $values.Warehouse
            $fparam.Database = $values.Database
            $fparam.Schema = $values.Schema
            $fparam.Configuration.context.database = $values.Database
            $fparam.Configuration.context.schema = $values.Schema
            $fparam.FileSystemSafeName = ""
            $fparam.AccountName = ""
            $fparam.AccountFullName = ""
            $fparam.AccountUrl = ""
            $fparam.OrganizationID = ""
            $fparam.Region = ""
        }
        #Query Filter Update
        elseif($fparam.Type -eq 'query') {
            $fparam.Worksheet.OwnerUserID = ""
            $fparam.Worksheet.OwnerUserName = ""
            $fparam.Worksheet.Role = $values.Role
            $fparam.Worksheet.Warehouse = $values.Warehouse
            # $fparam.Worksheet.FileSystemSafeName = ""
            $fparam.Worksheet.AccountName = ""
            $fparam.Worksheet.AccountFullName = ""
            $fparam.Worksheet.AccountUrl = ""
            $fparam.Worksheet.OrganizationID = ""
            $fparam.Worksheet.Region = ""
            $fparam.Worksheet.Database = $values.Database
            $fparam.Worksheet.Schema = $values.Schema
            $fparam.Role = $values.Role
            $fparam.Warehouse = $values.Warehouse
            $fparam.Configuration.context.role = $values.Role
            $fparam.Configuration.context.warehouse = $values.Warehouse
            $fparam.Database = $values.Database
            $fparam.Schema = $values.Schema
            $fparam.Configuration.context.database = $values.Database
            $fparam.Configuration.context.schema = $values.Schema
            $fparam.FileSystemSafeName = ""
            $fparam.AccountName = ""
            $fparam.AccountFullName = ""
            $fparam.AccountUrl = ""
            $fparam.OrganizationID = ""
            $fparam.Region = ""
            }
        }
}

function Update-Dashboard-Object ($fparam, $values)
{
    $fparam.update | % {
        $fparam.OwnerUserID = ""
        $fparam.OwnerUserName = ""
        $fparam.Role = $values.Role
        $fparam.Warehouse = $values.Warehouse
        $fparam.Database = $values.Database
        $fparam.Schema = $values.Schema
        foreach ($worksheet in $fparam.Worksheets) {
            $worksheet.OwnerUserID = ""
            $worksheet.OwnerUserName = ""
            $worksheet.Role = $values.Role
            $worksheet.Warehouse = $values.Warehouse
            $worksheet.Database = $values.Database
            $worksheet.Schema = $values.Schema
            $worksheet.FileSystemSafeName = ""
            $worksheet.AccountName = ""
            $worksheet.AccountFullName = ""
            $worksheet.AccountUrl = ""
            $worksheet.OrganizationID = ""
            $worksheet.Region = ""
        }
        $fparam.Database = $values.Database
        $fparam.Schema = $values.Schema
        $fparam.FileSystemSafeName = ""
        $fparam.AccountName = ""
        $fparam.AccountFullName = ""
        $fparam.AccountUrl = ""
        $fparam.OrganizationID = ""
        $fparam.Region = ""
        $fparam.Contents.context.role = $values.Role
        $fparam.Contents.context.warehouse = $values.Warehouse
        $fparam.Contents.context.database = $values.Database
        $fparam.Contents.context.schema = $values.Schema
    }
}

function Update-Worksheet-Object ($fparam, $values)
{
    $fparam.update | % {
        $fparam.OwnerUserID = ""
        $fparam.OwnerUserName = ""
        $fparam.Role = $values.Role
        $fparam.Warehouse = $values.Warehouse
        $fparam.Database = $values.Database
        $fparam.Schema = $values.Schema
        $fparam.FileSystemSafeName = ""
        $fparam.AccountName = ""
        $fparam.AccountFullName = ""
        $fparam.AccountUrl = ""
        $fparam.OrganizationID = ""
        $fparam.Region = ""
    }
}

function SSO-Prompt([string]$inputString){
    return yes-no "Does the account at $inputString use SSO?"
}

function RenameTargetObjectsPrompt($values, $account){
    Write-Host -ForegroundColor Red "Account Values: $($account)"
    Write-Output ($values | Out-String)
    $yn_str = "Would you like to change any of the context values (Role, Warehouse, Database, or Schema) for the account at $($account)?"
    if (yes-no $yn_str) {
        $TargetReplacementValues = ChangeValues($values)
        return $TargetReplacementValues
    }
    else {return $SourceReplacementValues}
}

function take_input() {
    param
    (
        [Parameter(Position = 0, ValueFromPipeline = $true)]
        [string]$msg,
        [string]$ForegroundColor = "Cyan"
    )

    Write-Host -ForegroundColor $ForegroundColor -NoNewline $msg;
    return Read-Host
}

function yes-no([string]$inputString){
    do {
        $UserInput = take_input "`r`n$($inputString). Use y/n: "
    } while (
       'y','yes','n','no' -notcontains $UserInput
    )
    if ('y','yes' -contains $UserInput){
        return $true
    }
    else{
        return $false
    }
}

function ChangeValues($values){
    $OutHash = @{}
    foreach ($h in $values.GetEnumerator()) {
        $k = $h.Name
        $v = $h.Value
        $UserInput = take_input "Please provide a value for $($k). Current value is $($v), hit enter to use this value: "
        if ($UserInput) {
            $OutHash.$k = $UserInput
        }
        else {$OutHash.$k = $v 
        }
    }
    return $OutHash
}
