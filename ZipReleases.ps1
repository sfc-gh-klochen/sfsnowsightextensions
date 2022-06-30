#Native compression apps may not be executable from a script. 7-Zip works good. Make sure to update the Path accordingly.
$zip = "/Users/mybarra/Downloads/7z2107-mac/7zz"

# Get version from the project file
$projXML = [xml](Get-Content -Path .\SnowflakePS.csproj)
$version = $projXML.SelectNodes("Project/PropertyGroup/Version")."#text"
$version

cd "bin/Publish/win"
& $zip a "../../../../Releases/$version/SnowflakePS.win.$version.zip" '@../../../ReleaseIncludes/listfile.win.txt'

cd "../osx"
& $zip a "../../../../Releases/$version/SnowflakePS.osx.$version.zip" '@../../../ReleaseIncludes/listfile.osx.txt'

cd "../linux"
& $zip a "../../../../Releases/$version/SnowflakePS.linux.$version.zip" '@../../../ReleaseIncludes/listfile.linux.txt'

cd "../../.."
