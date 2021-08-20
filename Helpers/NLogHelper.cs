// Copyright (c) 2021 Snowflake Inc. All rights reserved.

// Licensed under the Apache License, Version 2.0 (the
// "License"); you may not use this file except in compliance
// with the License.  You may obtain a copy of the License at

//   http://www.apache.org/licenses/LICENSE-2.0

// Unless required by applicable law or agreed to in writing,
// software distributed under the License is distributed on an
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
// KIND, either express or implied.  See the License for the
// specific language governing permissions and limitations
// under the License.

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