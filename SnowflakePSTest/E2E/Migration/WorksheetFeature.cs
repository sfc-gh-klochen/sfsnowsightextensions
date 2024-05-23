namespace SnowflakePSTest.E2E.Migration
{
    using System.Diagnostics;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using SnowflakePSTest.E2E.Utils;
    
    [TestClass]
    public class WorksheetFeature
    {
        protected string TestBasePath { get; set; } = "/Users/lcuelloayala/Documents/Repositories/SNOWSIGHT_EXTENSION/OfficialSnowsightExtension/sfsnowsightextensions/SnowflakePSTest";
        protected const int Timeout = 180000;
        
        [TestMethod]
        [DataRow(CommandCreator.ActionIfExists.CreateNew, "Creating new Worksheet")]
        [DataRow(CommandCreator.ActionIfExists.CreateNewWithNewName, "will be ignored and new Worksheet will be created because ActionIfExists is CreateNew")]
        [DataRow(CommandCreator.ActionIfExists.Overwrite, "will be overwritten because ActionIfExists is Overwrite")]
        [DataRow(CommandCreator.ActionIfExists.Skip, "will be ignored and nothing will be done because ActionIfExists is Skip")]
        public void Migrate_Worksheet_Succeed(CommandCreator.ActionIfExists actionIfExists, string expectedResult)
        {
            var environmentSetupScriptFullPath = string.Join(Path.DirectorySeparatorChar,
                new string[] { 
                    TestBasePath, 
                    CommandCreator.GetTestUtilsFolderPath(), 
                    CommandCreator.SetParameterScriptFileName 
                });
            
            var args = $"{CommandCreator.GetCommandToExecuteScriptFunction(environmentSetupScriptFullPath, CommandCreator.GetMigrationCommand(actionIfExists))}";
            
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
                Assert.IsTrue(standardOutput.Contains(expectedResult));
            }
        }
    }    
}

