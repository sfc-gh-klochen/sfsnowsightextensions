namespace SnowflakePSTest.E2E.Utils;

public static class CommandCreator
{
    public const string SetParameterScriptFileName = "env_setup.ps1";
    public enum Account
    {
        Account1 = 1,
        Account2 = 2
    }

    public enum ActionIfExists
    {
        CreateNew,
        CreateNewWithNewName,
        Overwrite,
        Skip
    }

    public static string GetMigrationCommand(ActionIfExists actionIfExists = ActionIfExists.CreateNew)
    {
        switch (actionIfExists)
        {
            case ActionIfExists.Overwrite:
                return "migrateWorksheetsFromAccount1ToAccount2Overwrite";
            
            case ActionIfExists.Skip:
                return "migrateWorksheetsFromAccount1ToAccount2Skip";
            
            case ActionIfExists.CreateNewWithNewName:
                return "migrateWorksheetsFromAccount1ToAccount2CreateNewWithNewName";
            
            default:
                return "migrateWorksheetsFromAccount1ToAccount2CreateNew";
        }
    }
    
    public static string GetConnectionCommand(Account account = Account.Account1)
    {
        return $"sfAppConnectionAccount{(int)account}";
    }

    public static string GetCommandToExecuteScriptFunction (string powerShellScriptPath, string functionName)
    {
        return $"-Command \"& {{ . {powerShellScriptPath}; {functionName}}}\"";
    }

    public static string GetTestUtilsFolderPath()
    {
        return string.Join(Path.DirectorySeparatorChar, new string[] { "E2E", "Utils" });
    }
}