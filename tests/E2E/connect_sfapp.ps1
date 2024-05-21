cd "{EXTENSION_URL}"
import-Module ./SnowflakePS.psd1
Connect-SFApp -Account {ACCOUNT} -UserName {USER_NAME} -Password (ConvertTo-SecureString -String "{PASSWORD}" -AsPlainText)