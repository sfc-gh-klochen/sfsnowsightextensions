$version_number = Read-Host -Prompt "`r`nWhat version of the binaries do you want to install? (Hit enter to get the latest binaries)"

if ($version_number -eq ''){
    Write-Host "`r`nSearching https://api.github.com/repos/Snowflake-Labs/sfsnowsightextensions/releases/latest for latest binaries...`r`n" -ForegroundColor Cyan
    $requestData = Invoke-WebRequest -Uri  "https://api.github.com/repos/Snowflake-Labs/sfsnowsightextensions/releases/latest"
    $releases = ConvertFrom-Json $requestData.content
    $version_number = $releases.tag_name
    Write-Host "Found latest release: $version_number`r`n" -ForegroundColor Cyan
}

Do { $os = Read-Host -Prompt "Which os are you downloading the binaries for? Use osx, win, or linux" }
    while ('osx', 'win', 'linux' -notcontains $os )

if ($os -eq 'win'){
    $download_path = "$home/Downloads"
}
else {
    $download_path = '~/Downloads'
}

Write-Host "`r`nAttempting to download file if it exists at https://github.com/Snowflake-Labs/sfsnowsightextensions/releases/download/$version_number/SnowflakePS.$os.$version_number.zip`r`n" -ForegroundColor Cyan

curl "https://github.com/Snowflake-Labs/sfsnowsightextensions/releases/download/$version_number/SnowflakePS.$os.$version_number.zip" -O --output-dir ~/Downloads -o -J -L

Expand-Archive "$download_path/SnowflakePS.$os.$version_number.zip" -DestinationPath "~/Downloads/SnowflakePS.$os.$version_number" -Force

Import-Module "$download_path/SnowflakePS.$os.$version_number/$os/SnowflakePS.psd1" -Force -Verbose

Get-Command -Module SnowflakePS