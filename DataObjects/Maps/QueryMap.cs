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

using CsvHelper.Configuration;

namespace Snowflake.Powershell
{
    public class QueryMap : ClassMap<Query>
    {
        public QueryMap()
        {
            int i = 0;

            Map(m => m.QueryID).Index(i); i++;
            Map(m => m.URL).Index(i); i++;
            Map(m => m.SQLStart).Index(i); i++;
            Map(m => m.SQLTextHash).Index(i); i++;
            Map(m => m.Tag).Index(i); i++;
            
            Map(m => m.NumQuerySteps).Index(i); i++;
            Map(m => m.NumRunsGS).Index(i); i++;
            Map(m => m.NumRunsXP).Index(i); i++;

            Map(m => m.Status).Index(i); i++;
            Map(m => m.State).Index(i); i++;
            Map(m => m.ErrorCode).Index(i); i++;
            Map(m => m.ErrorMessage).Index(i); i++;

            Map(m => m.Role).Index(i); i++;
            Map(m => m.Database).Index(i); i++;
            Map(m => m.Schema).Index(i); i++;
                        
            Map(m => m.Warehouse).Index(i); i++;
            Map(m => m.WarehouseSize).Index(i); i++;
            Map(m => m.WarehouseServersTotal).Index(i); i++;
            Map(m => m.WarehouseServersAvailable).Index(i); i++;
            
            Map(m => m.User).Index(i); i++;
            Map(m => m.SessionID).Index(i); i++;
            Map(m => m.ClientApplication).Index(i); i++;
            Map(m => m.ClientEnvironment).Index(i); i++;
            Map(m => m.ClientAddress).Index(i); i++;
            Map(m => m.ClientSupportInfo).Index(i); i++;
            
            CSVMapHelper.SetISO8601DateFormat(Map(m => m.ClientSendTimeUtc), i); i++;
            CSVMapHelper.SetISO8601DateFormat(Map(m => m.StartedTimeUtc), i); i++;
            CSVMapHelper.SetISO8601DateFormat(Map(m => m.EndedTimeUtc), i); i++;
             
            Map(m => m.TimeTotal).Index(i); i++;
            Map(m => m.TimeCompilation).Index(i); i++;
            Map(m => m.TimeSchedule).Index(i); i++;
            Map(m => m.TimeGSExecution).Index(i); i++;
            Map(m => m.TimeXPExecution).Index(i); i++;
            Map(m => m.TimeQuerySubmit).Index(i); i++;
            Map(m => m.TimeXPExecutionSuccess).Index(i); i++;
            Map(m => m.TimeXPExecutionFailure).Index(i); i++;

            Map(m => m.DurationTotal).Index(i); i++;
            Map(m => m.DurationCompilation).Index(i); i++;
            Map(m => m.DurationSchedule).Index(i); i++;
            Map(m => m.DurationGSExecution).Index(i); i++;
            Map(m => m.DurationXPExecution).Index(i); i++;
            Map(m => m.DurationQuerySubmit).Index(i); i++;
            Map(m => m.DurationXPExecutionSuccess).Index(i); i++;
            Map(m => m.DurationXPExecutionFailure).Index(i); i++;

            Map(m => m.Files).Index(i); i++;
            Map(m => m.FilesAssigned).Index(i); i++;
            Map(m => m.FilesOriginal).Index(i); i++;
            Map(m => m.FilesExternal).Index(i); i++;
            
            Map(m => m.BytesScan).Index(i); i++;
            Map(m => m.BytesScanAssigned).Index(i); i++;
            Map(m => m.BytesFDNLocalRead).Index(i); i++;
            Map(m => m.BytesFDNLocalWrite).Index(i); i++;
            Map(m => m.BytesFDNRemoteRead).Index(i); i++;
            Map(m => m.BytesFDNRemoteWrite).Index(i); i++;
        }
    }
}