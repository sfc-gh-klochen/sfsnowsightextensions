$version_number = Read-Host -Prompt "What version of the binaries do you want to install?"

Do { $os = Read-Host -Prompt "Which os are you downloading the binaries for? Use osx, win, or linux" }
    while ('osx', 'win', 'linux' -notcontains $os )

Write-Host "`r`nAttempting to download file if it exists at https://github.com/Snowflake-Labs/sfsnowsightextensions/releases/download/$version_number/SnowflakePS.$os.$version_number.zip`r`n" -ForegroundColor Cyan

curl "https://github.com/Snowflake-Labs/sfsnowsightextensions/releases/download/$version_number/SnowflakePS.$os.$version_number.zip" -O --output-dir ~/Downloads -o -J -L

Expand-Archive "~/Downloads/SnowflakePS.$os.$version_number.zip" -DestinationPath "~/Downloads/SnowflakePS.$os.$version_number" -Force

Import-Module "~/Downloads/SnowflakePS.$os.$version_number/SnowflakePS.psd1 -Force"
