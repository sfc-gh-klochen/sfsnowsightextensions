using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Snowflake.Powershell.tests.E2E
{
    [TestClass]
    public class LoginFeature
    {
        private string TestBasePath { get; set; }
        private const int Timeout = 180000;
        private const string TestScriptFileName = "connect_sfapp.ps1";

        [TestMethod]
        public void Login_WithUserNameAndPassword_Succeed()
        {
            var args = $"-Command {TestBasePath}{Path.DirectorySeparatorChar}{TestScriptFileName}";
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
                Assert.IsTrue(standardOutput.Contains($"Successfully authenticated"));
            }
        }
    }    
}

