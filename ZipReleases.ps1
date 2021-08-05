$zip = "C:\Program Files\7-Zip\7z.exe"

# Get version from the project file
$projXML = [xml](Get-Content -Path .\SnowflakePS.csproj)
$version = $projXML.SelectNodes("Project/PropertyGroup/Version")."#text"
$version

cd "bin\Publish\win"
& $zip a "..\..\..\..\Releases\$version\SnowflakePS.win.$version.zip" '@..\..\..\ReleaseIncludes\listfile.win.txt'

cd "..\osx"
& $zip a "..\..\..\..\Releases\$version\SnowflakePS.osx.$version.zip" '@..\..\..\ReleaseIncludes\listfile.osx.txt'

cd "..\linux"
& $zip a "..\..\..\..\Releases\$version\SnowflakePS.linux.$version.zip" '@..\..\..\ReleaseIncludes\listfile.linux.txt'

cd "..\..\.."
