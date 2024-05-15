Write-Host "Versioning started"
"Build number " + $Env:gitVersion_assemblyVersion
$csprojfilename = "SnowflakePS.csproj"
"Project file to update " + $csprojfilename
[xml]$csprojcontents = Get-Content -Path $csprojfilename;
"Current version number is" + $csprojcontents.Project.PropertyGroup.Version
$oldversionNumber = $csprojcontents.Project.PropertyGroup.Version
$csprojcontents.Project.PropertyGroup.Version = $Env:gitVersion_assemblyVersion
$csprojcontents.Save($csprojfilename)
"Version number has been udated from " + $oldversionNumber + " to " + $Env:gitVersion_assemblyVersion
Write-Host "Finished"
Get-ChildItem