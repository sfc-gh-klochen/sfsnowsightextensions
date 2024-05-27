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

using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace Snowflake.Powershell
{
    public class Filter : EntityBase
    {
        // Filter metadata
        public string Description { get; set; }
        public string Keyword { get; set; }
        public string DisplayName { get; set; }
        public string Scope { get; set; }
        public string Type { get; set; }
        public int Version { get; set; }

        // Query Information
        public string WorksheetID { get; set; }

        public Worksheet Worksheet { get; set; }

        // Query Context
        public string Role { get; set; }
        public string Warehouse { get; set; }
        public string Database { get; set; }
        public string Schema { get; set; }

        // Configuration
        public JObject Configuration { get; set; }

        // File name
        public override string FileSystemSafeName 
        { 
            get
            {
                return String.Format("Filter.{0}.{1}.{2}.json", FileIOHelper.GetFileSystemSafeString(this.AccountName), FileIOHelper.GetFileSystemSafeString(this.Type), FileIOHelper.GetShortenedEntityNameForFileSystem(this.Keyword, 30));
            }
            set
            {
                this._FileSystemSafeName = value;
            }
        }

        public Filter()
        {}

        public Filter(JObject entityObject, AppUserContext authContext)
        {
            this.AccountName = authContext.AccountName;
            this.AccountFullName = authContext.AccountFullName;
            this.AccountUrl = authContext.AccountUrl;
            this.OrganizationID = authContext.OrganizationID;
            this.Region = authContext.Region;

            this.DisplayName = JSONHelper.getStringValueFromJToken(entityObject, "label");
            this.Description = JSONHelper.getStringValueFromJToken(entityObject, "desc");
            this.Keyword = JSONHelper.getStringValueFromJToken(entityObject, "keyword");
            this.Scope = JSONHelper.getStringValueFromJToken(entityObject, "scope");
            this.Type = JSONHelper.getStringValueFromJToken(entityObject, "type");
            this.Version = JSONHelper.getIntValueFromJToken(entityObject, "version");

            JObject contextObject = (JObject)JSONHelper.getJTokenValueFromJToken(entityObject, "context");
            if (contextObject != null)
            {
                this.Role = JSONHelper.getStringValueFromJToken(contextObject, "role");
                this.Warehouse = JSONHelper.getStringValueFromJToken(contextObject, "warehouse");
                this.Database = JSONHelper.getStringValueFromJToken(contextObject, "database");
                this.Schema = JSONHelper.getStringValueFromJToken(contextObject, "schema");
            }

            JObject settingsObject = (JObject)JSONHelper.getJTokenValueFromJToken(entityObject, "settings");
            if (settingsObject != null)
            {
                JObject sourceObject = (JObject)JSONHelper.getJTokenValueFromJToken(settingsObject, "source");
                if (sourceObject != null)
                {
                    this.Type = JSONHelper.getStringValueFromJToken(sourceObject, "type");
                    this.WorksheetID = JSONHelper.getStringValueFromJToken(sourceObject, "pid");
                }
            }

            this.Configuration = entityObject;
        }

        public override String ToString()
        {
            return String.Format(
                "Filter: {0} ({1}) of {2} scope is used as {3} in account {4} is ran by {5} role in {6} warehouse",
                this.DisplayName,
                this.Description,
                this.Scope,
                this.Keyword,
                this.AccountFullName,
                this.Role,
                this.Warehouse);
        }        
    }
}