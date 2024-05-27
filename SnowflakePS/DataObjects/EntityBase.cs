// Copyright (c) 2021-2022 Snowflake Inc. All rights reserved.

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
using System;
using System.IO;
using System.Reflection;

namespace Snowflake.Powershell
{
    public class EntityBase
    {
        // Version metadata
        public string _CreatedWith { get; set; } = "Snowflake Snowsight Extensions";
        public string _CreatedVersion { get; set; } = Assembly.GetExecutingAssembly().GetName().Version.ToString();

        private static Logger loggerConsole = LogManager.GetLogger("Snowflake.Powershell.Console");

        // Account level metadata
        public string AccountName { get; set; }
        public string AccountFullName { get; set; }
        public string AccountUrl { get; set; }
        public string OrganizationID { get; set; }
        public string Region { get; set; }

        internal string _FileSystemSafeName;
        public virtual string FileSystemSafeName { get; set;}

        public void SaveToFile(string fileName)
        {
            string fullPath = Path.GetFullPath(fileName);
            FileIOHelper.WriteObjectToFile(this, fullPath);
            loggerConsole.Info("Saved {0}", fullPath);
        }

        public void SaveToFolder(string folderName)
        {
            this.SaveToFile(Path.Combine(folderName, this.FileSystemSafeName));
        }
   }
}