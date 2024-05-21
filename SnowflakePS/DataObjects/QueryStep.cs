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
    public class QueryStep : EntityBase
    {
        public Query Query { get; set; }

        // Query basics
        public string QueryID { get; set; }

        public int GSRunNumber { get; set; }
        public int StepNumber { get; set; }
        public string Description { get; set; }
        public string State { get; set; }

        public int TimeExecution { get; set; }
        public TimeSpan DurationExecution { get; set; }

        // File name
        public override string FileSystemSafeName
        {
            get 
            {
                return String.Format("QueryStep.{0}.{1}.{2}.{3}.json", FileIOHelper.GetFileSystemSafeString(this.AccountName), this.QueryID, this.GSRunNumber, this.StepNumber);
            }
            set
            {
                this._FileSystemSafeName = value;
            }
        }

        public override String ToString()
        {
            return String.Format(
                "QueryStep: {0} in {1} GS run {2} step {3} took {4} ending in state {5}",
                this.QueryID,
                this.AccountFullName,
                this.GSRunNumber,
                this.StepNumber,
                this.TimeExecution,
                this.State);
        }

        public QueryStep()
        {}

        public QueryStep(JObject entityObject, AppUserContext authContext, Query query)
        {
            this.AccountName = authContext.AccountName;
            this.AccountFullName = authContext.AccountFullName;
            this.AccountUrl = authContext.AccountUrl;
            this.OrganizationID = authContext.OrganizationID;
            this.Region = authContext.Region;

            this.QueryID = query.QueryID;
            this.Query = query;

            this.GSRunNumber = 1;
            this.StepNumber = JSONHelper.getIntValueFromJToken(entityObject, "step");

            this.Description = JSONHelper.getStringValueFromJToken(entityObject, "description");
            this.State = JSONHelper.getStringValueFromJToken(entityObject, "state");
            this.TimeExecution = JSONHelper.getIntValueFromJToken(entityObject, "timeInMs");
            this.DurationExecution = new TimeSpan(0, 0, 0, 0, this.TimeExecution);
        }            
    }
}