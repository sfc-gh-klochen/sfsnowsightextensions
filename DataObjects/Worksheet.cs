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
    public class Worksheet : EntityBase
    {
        // Worksheet metadata
        public string FolderID { get; set; }
        public string FolderName { get; set; }
        public string OwnerUserID { get; set; }
        public string OwnerUserName { get; set; }
        public int Version { get; set; }
        public string URL { get; set; }
        public string WorksheetID { get; set; }
        public string WorksheetName { get; set; }
        public string LocalPath {get;set;}

        // Worksheet Dates
        public DateTime StartedUtc { get; set; }
        public DateTime EndedUtc { get; set; }
        public DateTime ModifiedUtc { get; set; }

        // Query Context
        public string Role { get; set; }
        public string Warehouse { get; set; }
        public string Database { get; set; }
        public string Schema { get; set; }

        // Query 
        public string Query { get; set; }
        public JArray Parameters { get; set; } = new JArray();

        public List<Chart> Charts { get; set; } = new List<Chart>();

        // File name
        public override string FileSystemSafeName 
        { 
            get
            {
                return String.Format("Worksheet.{0}.{1}.{2}.json", FileIOHelper.GetFileSystemSafeString(this.AccountName), FileIOHelper.GetShortenedEntityNameForFileSystem(this.WorksheetName, 30), FileIOHelper.GetFileSystemSafeString(this.WorksheetID));
            }
            set
            {
                this._FileSystemSafeName = value;
            }
        }

        public Worksheet()
        {}

        public Worksheet(JObject entityObject, JObject worksheetsPayloadObject, AppUserContext authContext)
        {
            JObject queriesObject = new JObject();
            if (JSONHelper.isTokenPropertyNull(worksheetsPayloadObject["models"], "queries") == false)
            {
                queriesObject = (JObject)worksheetsPayloadObject["models"]["queries"];
            }
            JObject usersObject = new JObject();
            if (JSONHelper.isTokenPropertyNull(worksheetsPayloadObject["models"], "users") == false)
            {
                usersObject = (JObject)worksheetsPayloadObject["models"]["users"];
            }
            JObject foldersObject = new JObject();
            if (JSONHelper.isTokenPropertyNull(worksheetsPayloadObject["models"], "folders") == false)
            {
                foldersObject = (JObject)worksheetsPayloadObject["models"]["folders"];
            }

            this.AccountName = authContext.AccountName;
            this.AccountFullName = authContext.AccountFullName;
            this.AccountUrl = authContext.AccountUrl;
            this.OrganizationID = authContext.OrganizationID;
            this.Region = authContext.Region;

            this.WorksheetID = JSONHelper.getStringValueFromJToken(entityObject, "entityId");
            this.WorksheetName = JSONHelper.getStringValueFromJToken(entityObject["info"], "name");
            
            this.Query = String.Empty;
            this.FolderName = String.Empty;
            
            JObject localPathObject = FileIOHelper.LoadLocalPathSettings();
            string worksheetLocalPath = JSONHelper.getStringValueFromJToken(localPathObject, "Worksheet");
            if(!String.IsNullOrEmpty(worksheetLocalPath)){
                this.LocalPath = String.Concat(worksheetLocalPath, this._FileSystemSafeName);

            }
        

            JObject entityDetailObject = (JObject)JSONHelper.getJTokenValueFromJToken(queriesObject, this.WorksheetID);
            if (entityDetailObject != null)
            {
                this.FolderID = JSONHelper.getStringValueFromJToken(entityDetailObject, "folderId");
                if (this.FolderID.Length > 0)
                {
                    JObject folderDetailObject = (JObject)foldersObject[this.FolderID];
                    if (folderDetailObject != null)
                    {
                        this.FolderName = JSONHelper.getStringValueFromJToken(folderDetailObject, "name");
                    }
                }
                else
                {
                    this.FolderName = String.Empty;
                }

                this.OwnerUserID = JSONHelper.getStringValueFromJToken(entityDetailObject, "ownerId");

                JObject userDetailObject = (JObject)JSONHelper.getJTokenValueFromJToken(usersObject, this.OwnerUserID);
                if (userDetailObject != null)
                {
                    this.OwnerUserName = JSONHelper.getStringValueFromJToken(userDetailObject, "username");
                }

                this.Version = JSONHelper.getIntValueFromJToken(entityDetailObject, "version");
                this.URL = JSONHelper.getStringValueFromJToken(entityDetailObject, "url");

                DateTime dateTimeValue = DateTime.MinValue;
                this.ModifiedUtc = JSONHelper.getDateTimeValueFromJToken(entityDetailObject, "modified");
                if (this.ModifiedUtc == DateTime.MinValue)
                {
                    if (DateTime.TryParse(JSONHelper.getStringValueFromJToken(entityDetailObject, "modified"), out dateTimeValue) == true) this.ModifiedUtc = dateTimeValue.ToUniversalTime();
                }
                else
                {
                    this.ModifiedUtc = this.ModifiedUtc.ToUniversalTime();
                }
                this.StartedUtc = JSONHelper.getDateTimeValueFromJToken(entityDetailObject, "startDate");
                if (this.StartedUtc == DateTime.MinValue)
                {
                    if (DateTime.TryParse(JSONHelper.getStringValueFromJToken(entityDetailObject, "startDate"), out dateTimeValue) == true) this.StartedUtc = dateTimeValue.ToUniversalTime();
                }
                else
                {
                    this.StartedUtc = this.StartedUtc.ToUniversalTime();
                }
                this.EndedUtc = JSONHelper.getDateTimeValueFromJToken(entityDetailObject, "endDate");
                if (this.EndedUtc == DateTime.MinValue)
                {
                    if (DateTime.TryParse(JSONHelper.getStringValueFromJToken(entityDetailObject, "endDate"), out dateTimeValue) == true) this.EndedUtc = dateTimeValue.ToUniversalTime();
                }
                else
                {
                    this.EndedUtc = this.EndedUtc.ToUniversalTime();
                }

                this.Role = JSONHelper.getStringValueFromJToken(entityDetailObject["queryContext"], "role");
                this.Warehouse = JSONHelper.getStringValueFromJToken(entityDetailObject["queryContext"], "warehouse");
                this.Database = JSONHelper.getStringValueFromJToken(entityDetailObject["queryContext"], "database");
                this.Schema = JSONHelper.getStringValueFromJToken(entityDetailObject["queryContext"], "schema");

                this.Query = JSONHelper.getStringValueFromJToken(entityDetailObject, "query");
                if (JSONHelper.isTokenPropertyNull(entityDetailObject, "paramRefs") == false)
                {
                    this.Parameters = (JArray)entityDetailObject["paramRefs"];
                }

                if (JSONHelper.isTokenPropertyNull(entityDetailObject, "drafts") == false)
                {
                    JObject queryDraftObject = (JObject)entityDetailObject["drafts"][this.OwnerUserID];
                    if (queryDraftObject != null)
                    {
                        this.Role = JSONHelper.getStringValueFromJToken(queryDraftObject["executionContext"], "role");
                        this.Warehouse = JSONHelper.getStringValueFromJToken(queryDraftObject["executionContext"], "warehouse");
                        this.Database = JSONHelper.getStringValueFromJToken(queryDraftObject["executionContext"], "database");
                        this.Schema = JSONHelper.getStringValueFromJToken(queryDraftObject["executionContext"], "schema");

                        this.Query = JSONHelper.getStringValueFromJToken(queryDraftObject, "query");
                    }
                }

                if (JSONHelper.isTokenPropertyNull(entityDetailObject, "chartInfos") == false)
                {
                    JArray chartInfoArray = (JArray)entityDetailObject["chartInfos"];
                    foreach (JObject chartInfoObject in chartInfoArray)
                    {
                        Chart chart = new Chart(this, chartInfoObject, authContext);
                        this.Charts.Add(chart);
                    }
                }
            }
        }

        public Worksheet(string worksheetID, JObject worksheetsPayloadObject, AppUserContext authContext)
        {
            JObject queriesObject = new JObject();
            if (JSONHelper.isTokenPropertyNull(worksheetsPayloadObject["models"], "queries") == false)
            {
                queriesObject = (JObject)worksheetsPayloadObject["models"]["queries"];
            }
            JObject usersObject = new JObject();
            if (JSONHelper.isTokenPropertyNull(worksheetsPayloadObject["models"], "users") == false)
            {
                usersObject = (JObject)worksheetsPayloadObject["models"]["users"];
            }
            JObject foldersObject = new JObject();
            if (JSONHelper.isTokenPropertyNull(worksheetsPayloadObject["models"], "folders") == false)
            {
                foldersObject = (JObject)worksheetsPayloadObject["models"]["folders"];
            }

            this.AccountName = authContext.AccountName;
            this.AccountFullName = authContext.AccountFullName;
            this.AccountUrl = authContext.AccountUrl;
            this.OrganizationID = authContext.OrganizationID;
            this.Region = authContext.Region;

            this.WorksheetID = worksheetID;
            this.LocalPath = String.Empty;
            this.Query = String.Empty;
            this.FolderName = String.Empty;

            JObject entityDetailObject = (JObject)JSONHelper.getJTokenValueFromJToken(queriesObject, this.WorksheetID);
            if (entityDetailObject != null)
            {
                this.WorksheetName = JSONHelper.getStringValueFromJToken(entityDetailObject, "name");

                this.FolderID = JSONHelper.getStringValueFromJToken(entityDetailObject, "folderId");
                if (this.FolderID.Length > 0)
                {
                    JObject folderDetailObject = (JObject)foldersObject[this.FolderID];
                    if (folderDetailObject != null)
                    {
                        this.FolderName = JSONHelper.getStringValueFromJToken(folderDetailObject, "name");
                    }
                }
                else
                {
                    this.FolderName = String.Empty;
                }

                this.OwnerUserID = JSONHelper.getStringValueFromJToken(entityDetailObject, "ownerId");

                JObject userDetailObject = (JObject)JSONHelper.getJTokenValueFromJToken(usersObject, this.OwnerUserID);
                if (userDetailObject != null)
                {
                    this.OwnerUserName = JSONHelper.getStringValueFromJToken(userDetailObject, "username");
                }

                this.Version = JSONHelper.getIntValueFromJToken(entityDetailObject, "version");
                this.URL = JSONHelper.getStringValueFromJToken(entityDetailObject, "url");

                DateTime dateTimeValue = DateTime.MinValue;
                this.ModifiedUtc = JSONHelper.getDateTimeValueFromJToken(entityDetailObject, "modified");
                if (this.ModifiedUtc == DateTime.MinValue)
                {
                    if (DateTime.TryParse(JSONHelper.getStringValueFromJToken(entityDetailObject, "modified"), out dateTimeValue) == true) this.ModifiedUtc = dateTimeValue.ToUniversalTime();
                }
                else
                {
                    this.ModifiedUtc = this.ModifiedUtc.ToUniversalTime();
                }
                this.StartedUtc = JSONHelper.getDateTimeValueFromJToken(entityDetailObject, "startDate");
                if (this.StartedUtc == DateTime.MinValue)
                {
                    if (DateTime.TryParse(JSONHelper.getStringValueFromJToken(entityDetailObject, "startDate"), out dateTimeValue) == true) this.StartedUtc = dateTimeValue.ToUniversalTime();
                }
                else
                {
                    this.StartedUtc = this.StartedUtc.ToUniversalTime();
                }
                this.EndedUtc = JSONHelper.getDateTimeValueFromJToken(entityDetailObject, "endDate");
                if (this.EndedUtc == DateTime.MinValue)
                {
                    if (DateTime.TryParse(JSONHelper.getStringValueFromJToken(entityDetailObject, "endDate"), out dateTimeValue) == true) this.EndedUtc = dateTimeValue.ToUniversalTime();
                }
                else
                {
                    this.EndedUtc = this.EndedUtc.ToUniversalTime();
                }

                this.Role = JSONHelper.getStringValueFromJToken(entityDetailObject["queryContext"], "role");
                this.Warehouse = JSONHelper.getStringValueFromJToken(entityDetailObject["queryContext"], "warehouse");
                this.Database = JSONHelper.getStringValueFromJToken(entityDetailObject["queryContext"], "database");
                this.Schema = JSONHelper.getStringValueFromJToken(entityDetailObject["queryContext"], "schema");

                this.Query = JSONHelper.getStringValueFromJToken(entityDetailObject, "query");
                if (JSONHelper.isTokenPropertyNull(entityDetailObject, "paramRefs") == false)
                {
                    this.Parameters = (JArray)entityDetailObject["paramRefs"];
                }

                if (JSONHelper.isTokenPropertyNull(entityDetailObject, "drafts") == false)
                {
                    JObject queryDraftObject = (JObject)entityDetailObject["drafts"][this.OwnerUserID];
                    if (queryDraftObject != null)
                    {
                        this.Role = JSONHelper.getStringValueFromJToken(queryDraftObject["executionContext"], "role");
                        this.Warehouse = JSONHelper.getStringValueFromJToken(queryDraftObject["executionContext"], "warehouse");
                        this.Database = JSONHelper.getStringValueFromJToken(queryDraftObject["executionContext"], "database");
                        this.Schema = JSONHelper.getStringValueFromJToken(queryDraftObject["executionContext"], "schema");

                        this.Query = JSONHelper.getStringValueFromJToken(queryDraftObject, "query");
                    }
                }

                if (JSONHelper.isTokenPropertyNull(entityDetailObject, "chartInfos") == false)
                {
                    JArray chartInfoArray = (JArray)entityDetailObject["chartInfos"];
                    foreach (JObject chartInfoObject in chartInfoArray)
                    {
                        Chart chart = new Chart(this, chartInfoObject, authContext);
                        this.Charts.Add(chart);
                    }
                }
            }
        }

        public override String ToString()
        {
            int queryLength = this.Query.Length;
            if (queryLength > 256) queryLength = 256;

            if (this.FolderName.Length == 0)
            {
                return String.Format(
                    "Worksheet: {0} ({1}) in account {2} is owned by {3} ({4}) with query text:\n{5}",
                    this.WorksheetName,
                    this.WorksheetID,
                    this.AccountFullName,
                    this.OwnerUserName,
                    this.OwnerUserID,
                    this.Query.Substring(0, queryLength));
            }
            else
            {
                return String.Format(
                    "Worksheet: {0} ({1}) in folder {2} ({3}) in account {4} is owned by {5} ({6}) with query text:\n{7}",
                    this.WorksheetName,
                    this.WorksheetID,
                    this.FolderName,
                    this.FolderID,
                    this.AccountFullName,
                    this.OwnerUserName,
                    this.OwnerUserID,
                    this.Query.Substring(0, queryLength));
            }
        }        
    }
}