using NLog;
using NLog.Targets;
using System;
using System.IO;
using System.Reflection;

namespace Snowflake.Powershell
{
    public class NLogHelper
    {
        public static void ConfigureNLog()
        {
            string assemblyFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string nlogConfigFilePath = Path.Combine(assemblyFolder, "NLog.config");
            var config = new NLog.Config.XmlLoggingConfiguration(nlogConfigFilePath);

            // Remap the files to the relative path to the main DLL
            foreach (Target target in config.ConfiguredNamedTargets)
            {
                if (target is FileTarget)
                {
                    FileTarget fileTarget = (FileTarget)target;
                    fileTarget.FileName = Path.Combine(assemblyFolder, fileTarget.FileName.Render(new LogEventInfo()));
                }
            }

            LogManager.Configuration = config;
        }
    }
}