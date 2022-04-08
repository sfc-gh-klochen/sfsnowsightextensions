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
using System.Collections;
using System.Collections.Generic;

namespace Snowflake.Powershell
{
    public class Query : EntityBase
    {
        // Query basics
        public string QueryID { get; set; }
        public string SQL { get; set; }
        public string SQLStart { get; set; }
        public long SQLTextHash { get; set; }
        public string Tag { get; set; }
        public string URL { get; set; }

        // Query Context
        public string Role { get; set; }
        public string Database { get; set; }
        public string Schema { get; set; }
        
        // Warehouse 
        public string Warehouse { get; set; }
        public string WarehouseSize { get; set; }
        public int WarehouseServersTotal { get; set; }
        public int WarehouseServersAvailable { get; set; }

        // Session 
        public long SessionID { get; set; }
        public string ClientAddress { get; set; }
        public string ClientApplication { get; set; }
        public string ClientEnvironment { get; set; }
        public string ClientSupportInfo { get; set; }
        public string User { get; set; }

        public string Status { get; set; }
        public string State { get; set; }
        public string ErrorCode { get; set; }
        public string ErrorMessage { get; set; }

        // Timestamps
        public DateTime ClientSendTimeUtc { get; set; }
        public DateTime StartedTimeUtc { get; set; }
        public DateTime EndedTimeUtc { get; set; }

        // Stats and Durations
        public int TimeTotal { get; set; }
        public int TimeCompilation { get; set; }
        public int TimeSchedule { get; set; }
        public int TimeGSExecution { get; set; }
        public int TimeXPExecution { get; set; }
        public int TimeQuerySubmit { get; set; }

        public int TimeXPExecutionSuccess { get; set; }
        public int TimeXPExecutionFailure { get; set; }

        public TimeSpan DurationTotal { get; set; }
        public TimeSpan DurationCompilation { get; set; }
        public TimeSpan DurationSchedule { get; set; }
        public TimeSpan DurationGSExecution { get; set; }
        public TimeSpan DurationXPExecution { get; set; }
        public TimeSpan DurationQuerySubmit { get; set; }

        public TimeSpan DurationXPExecutionSuccess { get; set; }
        public TimeSpan DurationXPExecutionFailure { get; set; }

        // Bytes and Files
        public long FilesExternal { get; set; }
        public long FilesAssigned { get; set; }
        public long Files { get; set; }
        public long FilesOriginal { get; set; }

        public long BytesScan { get; set; }
        public long BytesScanAssigned { get; set; }
        public long BytesFDNLocalRead { get; set; }
        public long BytesFDNLocalWrite { get; set; }
        public long BytesFDNRemoteRead { get; set; }
        public long BytesFDNRemoteWrite { get; set; }

        public int NumQuerySteps 
        {
            get
            {
                return this.QuerySteps.Count;
            }
        }
        public int NumRunsGS { get; set; }
        public int NumRunsXP { get; set; }

        public List<QueryStep> QuerySteps { get; set; } = new List<QueryStep>(10);

        // File name
        public override string FileSystemSafeName
        {
            get 
            {
                return String.Format("Query.{0}.{1}.json", FileIOHelper.GetFileSystemSafeString(this.AccountName), this.QueryID);
            }
            set
            {
                this._FileSystemSafeName = value;
            }
        }

        public override String ToString()
        {
            return String.Format(
                "Query: {0} in {1} ran by {2} on {3} ({4}) is a {5}",
                this.QueryID,
                this.AccountFullName,
                this.User,
                this.Warehouse,
                this.WarehouseSize,
                this.Status);
        }

        public Query()
        {}

        public Query(JObject entityObject, AppUserContext authContext)
        {
            this.AccountName = authContext.AccountName;
            this.AccountFullName = authContext.AccountFullName;
            this.AccountUrl = authContext.AccountUrl;
            this.OrganizationID = authContext.OrganizationID;
            this.Region = authContext.Region;

            JObject dataObject = (JObject)JSONHelper.getJTokenValueFromJToken(entityObject, "data");
            if (dataObject != null)
            {
                JArray queriesArray = (JArray)JSONHelper.getJTokenValueFromJToken(dataObject, "queries");
                if (queriesArray != null && queriesArray.Count > 0)
                {
                    JObject queryObject = (JObject)queriesArray[0];

                    this.QueryID = JSONHelper.getStringValueFromJToken(queryObject, "id");
                    this.SQL = JSONHelper.getStringValueFromJToken(queryObject, "sqlText");
                    if (this.SQL.Length >= 50)
                    {
                        this.SQLStart = this.SQL.Substring(0, 50);                    
                    }
                    this.SQLTextHash = JSONHelper.getLongValueFromJToken(queryObject, "sqlTextHash");
                    this.Tag = JSONHelper.getStringValueFromJToken(queryObject, "queryTag");
                    
                    this.URL = String.Format("{0}/{1}/{2}/compute/history/queries/{3}/detail", authContext.MainAppUrl,  authContext.Region, authContext.AccountFullName, this.QueryID);

                    this.Status = JSONHelper.getStringValueFromJToken(queryObject, "status");
                    this.State = JSONHelper.getStringValueFromJToken(queryObject, "state");

                    this.ErrorCode = JSONHelper.getStringValueFromJToken(queryObject, "errorCode");
                    this.ErrorMessage = JSONHelper.getStringValueFromJToken(queryObject, "errorMessage");

                    this.Warehouse = JSONHelper.getStringValueFromJToken(queryObject, "warehouseName");
                    this.WarehouseSize = JSONHelper.getStringValueFromJToken(queryObject, "warehouseExternalSize");
                    this.WarehouseServersTotal = JSONHelper.getIntValueFromJToken(queryObject["stats"], "warehouseSize");
                    this.WarehouseServersAvailable = JSONHelper.getIntValueFromJToken(queryObject["stats"], "warehouseAvailableSize");

                    this.TimeTotal = JSONHelper.getIntValueFromJToken(queryObject, "totalDuration");
                    this.TimeCompilation = JSONHelper.getIntValueFromJToken(queryObject["stats"], "compilationTime");
                    this.TimeSchedule = JSONHelper.getIntValueFromJToken(queryObject["stats"], "scheduleTime");
                    this.TimeGSExecution = JSONHelper.getIntValueFromJToken(queryObject["stats"], "gsExecTime");
                    this.TimeXPExecution = JSONHelper.getIntValueFromJToken(queryObject["stats"], "xpExecTime");
                    this.TimeQuerySubmit = JSONHelper.getIntValueFromJToken(queryObject["stats"], "querySubmissionTime");

                    this.DurationTotal = new TimeSpan(0, 0, 0, 0, this.TimeTotal);
                    this.DurationCompilation = new TimeSpan(0, 0, 0, 0, this.TimeCompilation);
                    this.DurationSchedule = new TimeSpan(0, 0, 0, 0, this.TimeSchedule);
                    this.DurationGSExecution = new TimeSpan(0, 0, 0, 0, this.TimeGSExecution);
                    this.DurationXPExecution = new TimeSpan(0, 0, 0, 0, this.TimeXPExecution);
                    this.DurationQuerySubmit = new TimeSpan(0, 0, 0, 0, this.TimeQuerySubmit);

                    this.ClientSendTimeUtc =  DateTimeOffset.FromUnixTimeMilliseconds(JSONHelper.getLongValueFromJToken(queryObject, "clientSendTime")).UtcDateTime;
                    this.StartedTimeUtc = DateTimeOffset.FromUnixTimeMilliseconds(JSONHelper.getLongValueFromJToken(queryObject, "startTime")).UtcDateTime; 
                    this.EndedTimeUtc = DateTimeOffset.FromUnixTimeMilliseconds(JSONHelper.getLongValueFromJToken(queryObject, "endTime")).UtcDateTime;
                
                    this.FilesExternal = JSONHelper.getLongValueFromJToken(queryObject, "listingExternalFiles");
                    this.FilesAssigned = JSONHelper.getLongValueFromJToken(queryObject["stats"], "scanAssignedFiles");
                    this.Files = JSONHelper.getLongValueFromJToken(queryObject["stats"], "scanFiles");
                    this.FilesOriginal = JSONHelper.getLongValueFromJToken(queryObject["stats"], "scanOriginalFiles");

                    this.BytesScan = JSONHelper.getLongValueFromJToken(queryObject["stats"], "scanBytes");
                    this.BytesScanAssigned = JSONHelper.getLongValueFromJToken(queryObject["stats"], "scanAssignedBytes");
                    this.BytesFDNLocalRead = JSONHelper.getLongValueFromJToken(queryObject["stats"], "ioLocalFdnReadBytes");
                    this.BytesFDNLocalWrite = JSONHelper.getLongValueFromJToken(queryObject["stats"], "ioLocalFdnWriteBytes");
                    this.BytesFDNRemoteRead = JSONHelper.getLongValueFromJToken(queryObject["stats"], "ioRemoteFdnReadBytes");
                    this.BytesFDNRemoteWrite = JSONHelper.getLongValueFromJToken(queryObject["stats"], "ioRemoteFdnWriteBytes");
                }

                JArray sessionsArray = (JArray)JSONHelper.getJTokenValueFromJToken(dataObject, "sessionsShort");
                if (sessionsArray != null && sessionsArray.Count > 0)
                {
                    JObject sessionObject = (JObject)sessionsArray[0];

                    this.User = JSONHelper.getStringValueFromJToken(sessionObject, "userName");

                    this.Role = JSONHelper.getStringValueFromJToken(sessionObject, "currentRole");
                    this.Database = JSONHelper.getStringValueFromJToken(sessionObject, "currentDatabase");
                    this.Schema = JSONHelper.getStringValueFromJToken(sessionObject, "currentSchema");
                    
                    this.SessionID = JSONHelper.getLongValueFromJToken(sessionObject, "id");
                    this.ClientAddress = JSONHelper.getStringValueFromJToken(sessionObject, "clientNetAddress");
                    this.ClientApplication = JSONHelper.getStringValueFromJToken(sessionObject, "clientApplication");
                    this.ClientEnvironment = JSONHelper.getStringValueFromJToken(sessionObject, "clientEnvironment");
                    this.ClientSupportInfo = JSONHelper.getStringValueFromJToken(sessionObject, "clientSupportInfo");
                }
            }
        }            
    }
}