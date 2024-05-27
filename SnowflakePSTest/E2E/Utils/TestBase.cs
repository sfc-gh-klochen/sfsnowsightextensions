namespace SnowflakePSTest.E2E.Utils
{
    using System.Diagnostics;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    public class TestBase
    {
        public string TestBasePath { get; set; } = String.Empty;
        public int Timeout { get; set; } = 0;
        
        public TestContext TestContext { get; set; }

        private Dictionary<string, string?> GetParametersFromRunSettings()
        {
            return this.TestContext.Properties.Cast<KeyValuePair<string, object>>()
                .ToDictionary(parameter => parameter.Key, parameter => parameter.Value.ToString());
        }

        [TestInitialize]
        public void Initialize()
        {
            try
            {
                var runSettings = this.GetParametersFromRunSettings();
                TestBasePath = runSettings["TestBasePath"];
                Timeout = int.Parse(runSettings["Timeout"]); 
            }
            catch
            {
                Debug.WriteLine("Failed to initialize test");
            }
        }
    }
}