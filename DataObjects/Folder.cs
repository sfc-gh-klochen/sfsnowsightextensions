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

using System;

namespace Snowflake.Powershell
{
    public class Folder : EntityBase
    {
        // Folder metadata
        public string FolderID { get; set; }
        public string FolderName { get; set; }
        public string OwnerUserID { get; set; }
        public string OwnerUserName { get; set; }
        public int Version { get; set; }
        public string URL { get; set; }

        // Folder Dates
        public DateTime RefreshedUtc { get; set; }
        public DateTime ModifiedUtc { get; set; }

        // File name
        public override string FileSystemSafeName
        {
            get 
            {
                return String.Format("Folder.{0}.{1}.{2}.json", FileIOHelper.GetFileSystemSafeString(this.AccountName), FileIOHelper.GetShortenedEntityNameForFileSystem(this.FolderName, 30), FileIOHelper.GetFileSystemSafeString(this.FolderID));
            }
            set
            {
                this._FileSystemSafeName = value;
            }
        }

        public override String ToString()
        {
            return String.Format(
                "Folder: {0} ({1}) in {2} owned by {3}({4})",
                this.FolderName,
                this.FolderID,
                this.AccountFullName,
                this.OwnerUserName,
                this.OwnerUserID);
        }        
    }
}