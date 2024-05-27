namespace SnowflakePSTest.E2E.Authentication
{
    using SnowflakePSTest.E2E.Utils;
    using System.Diagnostics;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    
    [TestClass]
    public class LoginFeature : TestBase
    {
        [TestMethod]
        [DataRow(CommandCreator.Account.Account1)]
        [DataRow(CommandCreator.Account.Account2)]
        public void Login_WithUserNameAndPassword_Succeed(CommandCreator.Account account)
        {
            var environmentSetupScriptFullPath = string.Join(Path.DirectorySeparatorChar,
                new string[] { 
                    TestBasePath, 
                    CommandCreator.GetTestUtilsFolderPath(), 
                    CommandCreator.SetParameterScriptFileName 
                });
            
            var args = $"{CommandCreator.GetCommandToExecuteScriptFunction(environmentSetupScriptFullPath, CommandCreator.GetConnectionCommand(account))}";
            
            var standardOutput = string.Empty;
            
            using (var process = new Process())
            {
                process.StartInfo.FileName = "pwsh";
                process.StartInfo.Arguments = args;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;

                standardOutput += $"Command:\n{args}\n\nOutput:\n";
                process.Start();
                standardOutput += process.StandardOutput.ReadToEnd();
                standardOutput += process.StandardError.ReadToEnd();
                
                if (!process.WaitForExit(Timeout))
                {
                    process.Kill();
                    Assert.Fail($"Command {args} timed out");
                }

                Assert.IsNotNull(standardOutput);
                Assert.IsTrue(standardOutput.Contains("Successfully authenticated"));
            }
        }
    }    
}

