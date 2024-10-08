# Get version from the project file
$projXML = [xml](Get-Content -Path ./SnowflakePS.csproj)
$version = $projXML.SelectNodes("Project/PropertyGroup/Version")."#text"
$version

cd "bin/Publish/win"
& 7z a "../../../Releases/SnowflakePS.win.$version.zip" '@../../../ReleaseIncludes/listfile.win.txt'

cd "../osx"
& 7z a "../../../Releases/SnowflakePS.osx.$version.zip" '@../../../ReleaseIncludes/listfile.osx.txt'

cd "../osx-arm"
& 7z a "../../../Releases/SnowflakePS.osx-arm.$version.zip" '@../../../ReleaseIncludes/listfile.osx.txt'

cd "../linux"
& 7z a "../../../Releases/SnowflakePS.linux.$version.zip" '@../../../ReleaseIncludes/listfile.linux.txt'

cd "../linux-arm"
& 7z a "../../../Releases/SnowflakePS.linux-arm.$version.zip" '@../../../ReleaseIncludes/listfile.linux.txt'


cd "../../.."

get-childitem -Path "./Releases" -Recurse