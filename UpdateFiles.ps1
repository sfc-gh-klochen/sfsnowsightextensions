function Update-Documents ()
{
    [CmdletBinding()]
    param (
        [Parameter(Mandatory)] [String]$SFObjectTypes,
        [Parameter(Mandatory)] [String]$SFRole,
        [Parameter(Mandatory)] [String]$SFWarehouse,
        [Parameter()] [String]$WorksheetsPath,
        [Parameter()] [String]$DashboardsPath,
        [Parameter()] [String]$FiltersPath,
        [Parameter()] [String]$SFDatabase,
        [Parameter()] [String]$SFSchema,
        [Parameter()] [String]$OutputDirectory
    )

    if ($SFObjectTypes.Trim().ToLower() -eq "all") {
        if (-Not ($WorksheetsPath, $DashboardsPath, $FiltersPath)) {
            echo "Please supply a path for each object type (-WorksheetsPath, -DashboardsPath, -FiltersPath)"
            break
        }

        $tmp_filters = Get-ChildItem $FiltersPath
        if ($tmp_filters) {
            echo "`r`nFound files at $FiltersPath"
            $tmp_filters
            echo "`r`n"
        }
        else {
            echo "`r`nNo files found at $FiltersPath"
        }
        
        $tmp_dashboards = Get-ChildItem $DashboardsPath
        if ($tmp_dashboards) {
            echo "`r`nFound files at $DashboardsPath"
            $tmp_dashboards
            echo "`r`n"
        }
        else {
            echo "`r`nNo files found at $DashboardsPath"
        }
    
        $tmp_worksheets = Get-ChildItem $WorksheetsPath
        if ($tmp_worksheets) {
            echo "Found files at $WorksheetsPath"
            $tmp_worksheets
            echo "`r`n"
        }
        else {
            echo "`r`nNo files found at $WorksheetsPath"
        }

        Invoke-Command -ScriptBlock {
            Update-Filters -FiltersPath $FiltersPath -SFRole $SFRole  -SFWarehouse $SFWarehouse 
            echo "`r`n"
            Update-Dashboards -DashboardsPath $DashboardsPath -SFRole $SFRole  -SFWarehouse $SFWarehouse
            echo "`r`n"
            Update-Worksheets -WorksheetsPath $WorksheetsPath -SFRole $SFRole  -SFWarehouse $SFWarehouse
            echo "`r`n"
        }
    } else {

    $objs = $SFObjectTypes -Split ',' 

    # Begin for loop through array
    foreach ($_ in $objs) {
    $obj = $_.Trim().ToLower()

    echo "`r`nSearching for $obj files.`r`n"

    if($obj -eq "filter") {
        if (-Not ($FiltersPath)) {
            echo "Please supply a path for -FiltersPath argument. Skipping."
            continue
        }
        $tmp_filters = Get-ChildItem $FiltersPath
        if ($tmp_filters) {
            echo "`r`nFound files at $FiltersPath"
            $tmp_filters
            echo "`r`n"
        }
        else {
            echo "`r`nNo files found at $FiltersPath. Skipping."
            continue
        }
        Invoke-Command -ScriptBlock { Update-Filters -FiltersPath $FiltersPath -SFRole $SFRole  -SFWarehouse $SFWarehouse }
        echo "`r`n"
    }

    elseif ($obj -eq "dashboard") {
        if (-Not ($FiltersPath)) {
            echo "Please supply a path for -DashboardsPath argument. Skipping."
            continue
        }
        $tmp_dashboards = Get-ChildItem $DashboardsPath
        if ($tmp_dashboards) {
            echo "`r`nFound files at $DashboardsPath"
            $tmp_dashboards
            echo "`r`n"
        }
        else {
            echo "`r`nNo files Found at $DashboardsPath. Skipping."
            continue
        }

        Invoke-Command -ScriptBlock { Update-Dashboards -DashboardsPath $DashboardsPath -SFRole $SFRole  -SFWarehouse $SFWarehouse }
    }

    elseif ($obj -eq "worksheet") {
        if (-Not ($FiltersPath)) {
            echo "Please supply a path for -WorksheetsPath argument. Skipping."
            continue
        }
        $tmp_worksheets = Get-ChildItem $WorksheetsPath
        if ($tmp_worksheets) {
            echo "`r`nFound files at $WorksheetsPath"
            $tmp_worksheets
            echo "`r`n"
        }
        else {
            echo "`r`nNo files found at $WorksheetsPath"
            continue
        }

        Invoke-Command -ScriptBlock { Update-Worksheets -WorksheetsPath $WorksheetsPath -SFRole $SFRole  -SFWarehouse $SFWarehouse }
    } 
    else {
        echo "$obj not a valid object. Use 'Filter, Dashboard, Worksheet, a combination of the three in a comma seperated list, or All.'`r`n"
        continue
    }
}
}

<#
.SYNOPSIS
 Update Snowflake Role and Warehouse in Filters, Dashboards, or Worksheets files together or separately. 
.DESCRIPTION
 This function allows the user to update the Snowflake Role and Warehouse in the exported Filter, Dashboard, or Worksheet files together or separately. Please refer to the examples for syntax. If a Role is not provided, ACCOUNTADMIN will be the designated Role.

.PARAMETER SFObjectTypes
Specifies the object type being updated. Use Filter, Dashboard, Worksheet, or or a combination of the three (do not use all with the others or duplicates will be created) can be entered.
Choose All to update Filters, Dashboards, and Worksheets at the same time. Casing, ordering, and spaces do not matter for this argument do not matter. 'filter,dashboard,worksheet' = 'Worksheet, Dashboard, Filter'."

.PARAMETER FiltersPath
Specifies the path to the directory where the filters are located. Case sensitive.

.PARAMETER DashboardsPath
Specifies the path to the directory where the dashboards are located. Case sensitive.

.PARAMETER WorksheetsPath
Specifies the path to the directory where the worksheets are located. Case sensitive.

.PARAMETER SFRole
Specifies the Snowflake Role required to run the specific object. Default is ACCOUNTADMIN.

.PARAMETER SFWarehouse
Specifies the Snowflake Warehouse required to run the specific object. An error will display if no warehouse is designated.

.PARAMETER OutputDirectory
A relative or fully qualified path where files will be placed. This directory needs to exist already

.INPUTS
None. You cannot pipe objects to Update-Documents.

.OUTPUTS
set-content. Update-Documents updates the JSON value for the Snowflake Role and Warehouse in order to ensure the respective object works appropriately.
 
.EXAMPLE
---------------- All ----------------
Update-Documents -SFObjectTypes 'All' -FiltersPath $work_dir/filters  -DashboardsPath $work_dir/dashboards -SFRole THISMEANSEVERYTHINGWORKS  -SFWarehouse SUPERNEWWAREHOUSE
.EXAMPLE
---------------- Filters ----------------
Update-Documents -SFObjectTypes 'Filter' -FiltersPath $work_dir/filters -SFRole THISMEANSEVERYTHINGWORKS  -SFWarehouse SUPERNEWWAREHOUSE

.EXAMPLE
---------------- Dashboard ----------------
Update-Documents -SFObjectTypes 'Dashboard' -DashboardsPath $work_dir/dashboards -SFRole THISMEANSEVERYTHINGWORKS  -SFWarehouse SUPERNEWWAREHOUSE

.EXAMPLE
---------------- Worksheet ----------------
Update-Documents -SFObjectTypes 'Worksheet' -WorksheetsPath $work_dir/worksheets -SFRole THISMEANSEVERYTHINGWORKS  -SFWarehouse SUPERNEWWAREHOUSE

.EXAMPLE
---------------- Mix of assets ----------------
Update-Documents -SFObjectTypes 'dashboard, filter, worksheet' -FiltersPath $work_dir/filters -DashboardsPath $work_dir/Dashboards -WorksheetsPath $work_dir/worksheets -SFWarehouse SUPERNEWWAREHOUSE

#>
}


function Update-Filters ()
{
    if($FiltersPath) {
        # Update Filters files
        foreach ($f in $tmp_filters){
            $fparam = Get-Content $f.FullName | ConvertFrom-JSON
            $fparam.update | % { #Manual Filter Update
                if($fparam.Type -eq 'manual'){
                    Write-Host 'Updating MANUAL FILTER values for' -ForegroundColor Cyan 
                    Write-Host $f.Name -ForegroundColor Green
                    $fparam.Role = $SFRole
                    $fparam.Warehouse = $SFWarehouse
                    $fparam.Configuration.context.role = $SFRole
                    $fparam.Configuration.context.warehouse = $SFWarehouse
                    $fparam.Database = $SFDatabase
                    $fparam.Schema = $SFSchema
                    $fparam.Configuration.context.database = $SFSchema
                    $fparam.Configuration.context.schema = $SFSchema
                    $fparam.FileSystemSafeName = ""
                    $fparam.AccountName = ""
                    $fparam.AccountFullName = ""
                    $fparam.AccountUrl = ""
                    $fparam.OrganizationID = ""
                    $fparam.Region = ""
                    }
                #Query Filter Update
                elseif($fparam.Type -eq 'query') {
                    Write-Host 'Updating QUERY FILTER values for' -ForegroundColor Cyan
                    Write-Host $f.Name -ForegroundColor Yellow
                    $fparam.Worksheet.OwnerUserID = ""
                    $fparam.Worksheet.OwnerUserName = ""
                    $fparam.Worksheet.Role = $SFRole
                    $fparam.Worksheet.Warehouse = $SFWarehouse
                    $fparam.Worksheet.FileSystemSafeName = ""
                    $fparam.Worksheet.AccountName = ""
                    $fparam.Worksheet.AccountFullName = ""
                    $fparam.Worksheet.AccountUrl = ""
                    $fparam.Worksheet.OrganizationID = ""
                    $fparam.Worksheet.Region = ""
                    $fparam.Worksheet.Database = $SFDatabase
                    $fparam.Worksheet.Schema = $SFSchema
                    $fparam.Role = $SFRole
                    $fparam.Warehouse = $SFWarehouse
                    $fparam.Configuration.context.role = $SFRole
                    $fparam.Configuration.context.warehouse = $SFWarehouse
                    $fparam.Database = $SFDatabase
                    $fparam.Schema = $SFSchema
                    $fparam.Configuration.context.database = $SFDatabase
                    $fparam.Configuration.context.schema = $SFSchema
                    $fparam.FileSystemSafeName = ""
                    $fparam.AccountName = ""
                    $fparam.AccountFullName = ""
                    $fparam.AccountUrl = ""
                    $fparam.OrganizationID = ""
                    $fparam.Region = ""
                    }
            }
            if ($OutputDirectory) {
                if (-Not (Test-Path $OutputDirectory/filters)) {
                    # Create the directory if not exists
                    New-Item $OutputDirectory/filters -ItemType Directory
                    Write-Host "Created path $OutputDirectory/filters"
                } else {
                $fparam | ConvertTo-JSON -depth 10| set-content $OutputDirectory/filters/$f.FullName
                }
            }
            else {
                $fparam | ConvertTo-JSON -depth 10| set-content $f.FullName
            }
        }
    }
    else { Write-Host "-FiltersPath was not entered"}    
}


function Update-Dashboards () 
{
    if($DashboardsPath) {
        # Update DASHBOARD files
            foreach ($f in $tmp_dashboards){
                $fparam = Get-Content $f.FullName | ConvertFrom-JSON
                $fparam.update | % {
                    Write-Host 'Updating DASHBOARD values for' -ForegroundColor Cyan
                    Write-Host $f.Name -ForegroundColor Yellow
                    $fparam.OwnerUserID = ""
                    $fparam.OwnerUserName = ""
                    $fparam.Role = $SFRole
                    $fparam.Warehouse = $SFWarehouse
                    $fparam.Database = $SFDatabase
                    $fparam.Schema = $SFSchema
                    foreach ($worksheet in $fparam.Worksheets) {
                        $worksheet.OwnerUserID = ""
                        $worksheet.OwnerUserName = ""
                        $worksheet.Role = $SFRole
                        $worksheet.Warehouse = $SFWarehouse
                        $worksheet.Database = $SFDatabase
                        $worksheet.Schema = $SFSchema
                        $worksheet.FileSystemSafeName = ""
                        $worksheet.AccountName = ""
                        $worksheet.AccountFullName = ""
                        $worksheet.AccountUrl = ""
                        $worksheet.OrganizationID = ""
                        $worksheet.Region = ""
                    }
                    $fparam.Database = $SFDatabase
                    $fparam.Schema = $SFSchema
                    $fparam.FileSystemSafeName = ""
                    $fparam.AccountName = ""
                    $fparam.AccountFullName = ""
                    $fparam.AccountUrl = ""
                    $fparam.OrganizationID = ""
                    $fparam.Region = ""
                    $fparam.Contents.context.role = $SFRole
                    $fparam.Contents.context.warehouse = $SFWarehouse
                    $fparam.Contents.context.database = $SFDatabase
                    $fparam.Contents.context.schema = $SFSchema
                }

            if ($OutputDirectory) {
                if (-Not (Test-Path $OutputDirectory/dashboards)) {
                    # Create the directory if not exists
                    New-Item $OutputDirectory/dashboards -ItemType Directory
                    Write-Host "Created path $OutputDirectory/dashboards"
                }
                else {
                $fparam | ConvertTo-JSON -depth 10| set-content $OutputDirectory/dashboards/$f.FullName
                }
            } else {
            $fparam | ConvertTo-JSON -depth 10| set-content $f.FullName
            }
        }
    }
    else { Write-Host "-DashboardsPath was not entered"} 
}


function Update-Worksheets ()
{
    if($WorksheetsPath) {
        # Update WORKSHEET files
        foreach ($f in $tmp_worksheets){
            $fparam = Get-Content $f.FullName | ConvertFrom-JSON
            $fparam.update | % {
                Write-Host 'Updating WORKSHEET values for' -ForegroundColor Cyan
                Write-Host $f.Name -ForegroundColor Yellow
                $fparam.OwnerUserID = ""
                $fparam.OwnerUserName = ""
                $fparam.Role = $SFRole
                $fparam.Warehouse = $SFWarehouse
                $fparam.Database = $SFDatabase
                $fparam.Schema = $SFSchema
                $fparam.FileSystemSafeName = ""
                $fparam.AccountName = ""
                $fparam.AccountFullName = ""
                $fparam.AccountUrl = ""
                $fparam.OrganizationID = ""
                $fparam.Region = ""
            }
            if ($OutputDirectory) {
                if (-Not (Test-Path $OutputDirectory/worksheets)) {
                    # Create the directory if not exists
                    New-Item $OutputDirectory/worksheets -ItemType Directory
                    Write-Host "Created path $OutputDirectory/worksheets"
                } else {
                $fparam | ConvertTo-JSON -depth 10| set-content $OutputDirectory/worksheets/$f.FullName
                }
            }
            else {
            $fparam | ConvertTo-JSON -depth 10| set-content $f.FullName
            }
        }
    }
    else { Write-Host "-WorksheetsPath was not entered"}    
}

