function Update-Documents ()
{
    
    [CmdletBinding()]
    param (
        [Parameter(Mandatory)] [ValidateSet("All","Filter","Dashboard","Worksheet")] [String]$SFFileType,
        [Parameter()] [String]$WorksheetsPath,
        [Parameter()] [String]$DashboardsPath,
        [Parameter()] [String]$FiltersPath,
        [Parameter()] [String]$SFRole,
        [Parameter()] [String]$SFWarehouse
    )
    
    $tmp_filters = Get-ChildItem $FiltersPath
    $tmp_dashboards = Get-ChildItem $DashboardsPath
    $tmp_worksheets = Get-ChildItem $WorksheetsPath

    if($SFFileType -eq "Filter") {
        Invoke-Command -ScriptBlock { Update-Filters -FiltersPath $FiltersPath -SFRole $SFRole  -SFWarehouse $SFWarehouse }
    }
    elseif ($SFFileType -eq "Dashboard") {
        Invoke-Command -ScriptBlock { Update-Dashboards -DashboardsPath $DashboardsPath -SFRole $SFRole  -SFWarehouse $SFWarehouse }
    }
    elseif ($SFFileType -eq "Worksheet") {
        Invoke-Command -ScriptBlock { Update-Worksheets -WorksheetsPath $WorksheetsPath -SFRole $SFRole  -SFWarehouse $SFWarehouse }
    }
    else {
        Invoke-Command -ScriptBlock { 
            Update-Filters -FiltersPath $FiltersPath -SFRole $SFRole  -SFWarehouse $SFWarehouse 
            Update-Dashboards -DashboardsPath $DashboardsPath -SFRole $SFRole  -SFWarehouse $SFWarehouse
            Update-Worksheets -WorksheetsPath $WorksheetsPath -SFRole $SFRole  -SFWarehouse $SFWarehouse
        }
    }

<#
.SYNOPSIS
 Update Snowflake Role and Warehouse in Filters, Dashboards, or Worksheets files together or separately. 
.DESCRIPTION
 This function allows the user to update the Snowflake Role and Warehouse in the exported Filter, Dashboard, or Worksheet files together or separately. Please refer to the examples for syntax. If a Role is not provided, ACCOUNTADMIN will be the designated Role.

.PARAMETER SFFileType
Specifies the File Type being updated. Only All, Filter, Dashboard, or Worksheet can be entered.
Choose All to update Filters, Dashboards, and Worksheets at the same time.

.PARAMETER FiltersPath
Specifies the path to the directory where the filters are located.

.PARAMETER DashboardsPath
Specifies the path to the directory where the dashboards are located.

.PARAMETER WorksheetsPath
Specifies the path to the directory where the worksheets are located.

.PARAMETER SFRole
Specifies the Snowflake Role required to run the specific object. Default is ACCOUNTADMIN.

.PARAMETER SFWarehouse
Specifies the Snowflake Warehouse required to run the specific object. An error will display if no warehouse is designated.

.INPUTS
None. You cannot pipe objects to Update-Documents.

.OUTPUTS
set-content. Update-Documents updates the JSON value for the Snowflake Role and Warehouse in order to ensure the respective object works appropriately.
 
.EXAMPLE
---------------- All ----------------
Update-Documents -SFFileType ALL -FiltersPath ../Filters -DashboardsPath ../Dashboards -WorksheetsPath ../Worksheets -SFRole THISMEANSEVERYTHINGWORKS  -SFWarehouse SUPERNEWWAREHOUSE

.EXAMPLE
---------------- Filters ----------------
Update-Documents -SFFileType Filter -FiltersPath ../Filters -SFRole THISMEANSEVERYTHINGWORKS  -SFWarehouse SUPERNEWWAREHOUSE

.EXAMPLE
---------------- Dashboard ----------------
Update-Documents -SFFileType Dashboard -DashboardsPath ../Dashboards -SFRole THISMEANSEVERYTHINGWORKS  -SFWarehouse SUPERNEWWAREHOUSE

.EXAMPLE
---------------- Worksheet ----------------
Update-Documents -SFFileType Worksheet -WorksheetsPath ../Worksheets -SFRole THISMEANSEVERYTHINGWORKS  -SFWarehouse SUPERNEWWAREHOUSE

.EXAMPLE
---------------- All without a Role provided ----------------
Update-Documents -SFFileType ALL -FiltersPath ../Filters -DashboardsPath ../Dashboards -WorksheetsPath ../Worksheets -SFWarehouse SUPERNEWWAREHOUSE
 
#>

}

function Update-Filters ()
{
    if($FiltersPath) {
        if($SFRole -eq '') {

            $SFRole = "ACCOUNTADMIN"
        }
        else {
            if(($SFWarehouse)) {
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
                            $fparam.Role = $SFRole
                            $fparam.Warehouse = $SFWarehouse
                            $fparam.Configuration.context.role = $SFRole
                            $fparam.Configuration.context.warehouse = $SFWarehouse
                            $fparam.FileSystemSafeName = ""
                            $fparam.AccountName = ""
                            $fparam.AccountFullName = ""
                            $fparam.AccountUrl = ""
                            $fparam.OrganizationID = ""
                            $fparam.Region = ""
                            }
                    }
                    $fparam | ConvertTo-JSON -depth 10| set-content $f.FullName
                }
            }
            else {Write-Host "You forgot to enter a virtual warehouse"}
        }
        #else {Write-Host "You entered $($SFRole) as role."}
    }
    else { Write-Host "A file Path was not entered."}    
}


function Update-Dashboards () 
{
    if($DashboardsPath) {
        if($SFRole -eq '') {
            $SFRole = "ACCOUNTADMIN"
        }
        else {
            if(($SFWarehouse)) {
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
                        foreach ($worksheet in $fparam.Worksheets) {
                            $worksheet.OwnerUserID = ""
                            $worksheet.OwnerUserName = ""
                            $worksheet.Role = $SFRole
                            $worksheet.Warehouse = $SFWarehouse
                            $worksheet.FileSystemSafeName = ""
                            $worksheet.AccountName = ""
                            $worksheet.AccountFullName = ""
                            $worksheet.AccountUrl = ""
                            $worksheet.OrganizationID = ""
                            $worksheet.Region = ""
                        }
                        $fparam.FileSystemSafeName = ""
                        $fparam.AccountName = ""
                        $fparam.AccountFullName = ""
                        $fparam.AccountUrl = ""
                        $fparam.OrganizationID = ""
                        $fparam.Region = ""
                    }
                $fparam | ConvertTo-JSON -depth 10| set-content $f.FullName
                }
            }
            else {Write-Host "You forgot to enter a virtual warehouse"}
        }
        #else {Write-Host "You entered $($SFRole) as role."} 
    }
    else { Write-Host "A file Path was not entered."}  
}

function Update-Worksheets () 
{
    if($WorksheetsPath) {
        if($SFRole -eq '') {

            $SFRole = "ACCOUNTADMIN"
        }
        else {
            if(($SFWarehouse)) {

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
                        $fparam.FileSystemSafeName = ""
                        $fparam.AccountName = ""
                        $fparam.AccountFullName = ""
                        $fparam.AccountUrl = ""
                        $fparam.OrganizationID = ""
                        $fparam.Region = ""
                    }
                    $fparam | ConvertTo-JSON -depth 10| set-content $f.FullName
                }
            }
            else {Write-Host "You forgot to enter a virtual warehouse"}
        }
        #else {Write-Host "You entered $($SFRole) as role."}
    }
    else { Write-Host "A file Path was not entered."}    
}

