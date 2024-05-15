Write-Host "Versioning started"
"Build number " + $Env:gitVersion_assemblyVersion
$modulefile = ".\SnowflakePS.psd1"
"Module file to update " + $modulefile
$moduleContents = Get-Content -Path $modulefile;
$moduleContents = $moduleContents -replace "ModuleVersion = '.*'", ("ModuleVersion = `"{0}`"" -f $Env:gitVersion_assemblyVersion)
$moduleContents | Set-Content -Path $modulefile
Write-Host "Updated file:"
Write-Host $moduleContents
Write-Host "Finished"
Get-ChildItem