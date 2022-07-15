# Get 7Zip path
if ($PSVersionTable.Platform -eq "Win32NT")
{
    # Windows
    $zip = "C:\Program Files\7-Zip\7z.exe"
}
elseif (($PSVersionTable.Platform -eq "Unix") -and ($PSVersionTable.OS.Contains("Darwin") -eq $true))
{
    # Mac
    $zip = "~/Downloads/7z2107-mac/7zz"
}

# Get version from the project file
$projXML = [xml](Get-Content -Path .\SnowflakePS.csproj)
$version = $projXML.SelectNodes("Project/PropertyGroup/Version")."#text"
$version

cd "bin/Publish/win"
& $zip a "../../../../Releases/$version/SnowflakePS.win.$version.zip" '@../../../ReleaseIncludes/listfile.win.txt'

cd "../osx"
& $zip a "../../../../Releases/$version/SnowflakePS.osx.$version.zip" '@../../../ReleaseIncludes/listfile.osx.txt'

cd "../osx-arm"
& $zip a "../../../../Releases/$version/SnowflakePS.osx-arm.$version.zip" '@../../../ReleaseIncludes/listfile.osx.txt'

cd "../linux"
& $zip a "../../../../Releases/$version/SnowflakePS.linux.$version.zip" '@../../../ReleaseIncludes/listfile.linux.txt'

cd "../../.."
