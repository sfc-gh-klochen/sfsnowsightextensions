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

using System;

namespace Snowflake.Powershell
{
    public class AppUserContext : EntityBase
    {
        public string MainAppUrl { get; set; }
        public string AppServerUrl { get; set; }
        public string AuthTokenSnowsight { get; set; }
        public string AuthTokenMaster { get; set; }
        public string AuthTokenSession { get; set; }
        public string ClientID { get; set; }
        public string CSRFToken { get; set; }
        public string DefaultRole { get; set; }
        public string DefaultWarehouse { get; set; }
        public string UserID { get; set; }
        public string UserName { get; set; }
        public string ServerVersion { get; set; }
        public string ContextUrl { get; set; }
        public string RedirectURI { get; set; }
        public string ContextUserNameUrl 
        { 
            get 
            {
                // if (this.AccountUrl.Contains("privatelink", StringComparison.InvariantCultureIgnoreCase) == true)
                // {
                //     // For privatelink, it is username::accounturl without the privatelink
                //     // X-Snowflake-Context: MAJ93::https://amerdev01.us-east-1.snowflakecomputing.com
                //     // X-Snowflake-Privatelink-Host: https://app.us-east-1.privatelink.snowflakecomputing.com
                //     // Remove privatelink. from the string
                //     return String.Format("{0}::{1}", this.UserName, this.AccountUrl.Replace("privatelink.", "", true, System.Globalization.CultureInfo.InvariantCulture));
                // }
                // else
                // {
                    return String.Format("{0}::{1}", this.UserName, this.ContextUrl);
                //}
            }
            set 
            {}
        }

        public override string FileSystemSafeName
        {
            get 
            {
                return String.Format("AppUserContext.{0}.{1}.{2}.json", FileIOHelper.GetFileSystemSafeString(this.AccountName), FileIOHelper.GetFileSystemSafeString(this.Region), FileIOHelper.GetFileSystemSafeString(this.UserName));
            }
            set
            {
                this._FileSystemSafeName = value;
            }
        }

        public override String ToString()
        {
            return String.Format(
                "AppUserContext: User {0}({1})@{2}({3}), Snowsight at {4} served by {5}",
                this.UserName,
                this.UserID,
                this.AccountFullName,
                this.OrganizationID,
                this.AccountUrl, 
                this.AppServerUrl);
        }        
    }
}