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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace Snowflake.Powershell
{
    [Cmdlet
        (VerbsCommon.Get,
        "SFQueryProfile",
        SupportsPaging=false,
        SupportsShouldProcess=false)]
    [OutputType(typeof(String))]
    public class GetQueryProfileCommand : PSCmdlet
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private static Logger loggerConsole = LogManager.GetLogger("Snowflake.Powershell.Console");

        Stopwatch stopWatch = new Stopwatch();

        [Parameter(
            Mandatory = true,
            Position = 0,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true,
            HelpMessage = "Application user context from authentication process")]
        public AppUserContext AuthContext { get; set; }

        [Parameter(
            Mandatory = true,
            Position = 1,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true,
            HelpMessage = "ID of the query to retrieve")]
        public string[] QueryIDs { get; set; }

        [Parameter(
            Mandatory = false,
            Position = 2,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true,
            HelpMessage = "Role with which to access")]
        public string Role { get; set; }

        [Parameter(
            Mandatory = false,
            Position = 3,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true,
            HelpMessage = "Path to folder where to save result")]
        public string OutputFolder { get; set; }

        [Parameter(
            Mandatory = false,
            Position = 4,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true,
            HelpMessage = "Whether to include GS Job Retries")]
        public SwitchParameter IncludeJobRetries { get; set; }

        protected override void BeginProcessing()
        {
            stopWatch.Start();

            NLogHelper.ConfigureNLog();

            logger = LogManager.GetCurrentClassLogger();
            loggerConsole = LogManager.GetLogger("Snowflake.Powershell.Console");

            logger.Trace("BEGIN {0}", this.GetType().Name);
            WriteVerbose(String.Format("BEGIN {0}", this.GetType().Name));
        }

        protected override void EndProcessing()
        {
            stopWatch.Stop();

            logger.Trace("END {0} execution took {1:c} ({2} ms)", this.GetType().Name, stopWatch.Elapsed, stopWatch.ElapsedMilliseconds);
            loggerConsole.Trace("Execution took {0:c} ({1} ms)", stopWatch.Elapsed, stopWatch.ElapsedMilliseconds);            
            WriteVerbose(String.Format("END {0}, execution took {1:c} ({2} ms)", this.GetType().Name, stopWatch.Elapsed, stopWatch.ElapsedMilliseconds));
            
            LogManager.Flush();
        }

        protected override void ProcessRecord()
        {
            try
            {
                if (this.Role == null || this.Role.Length == 0)
                {
                    this.Role = this.AuthContext.DefaultRole;
                }

                if (this.OutputFolder == null || this.OutputFolder.Length == 0) this.OutputFolder = ".";
                this.OutputFolder = Path.GetFullPath(this.OutputFolder);
                logger.Info("OutputFolder={0}", this.OutputFolder);

                logger.Info("IncludeJobRetries={0}", this.IncludeJobRetries);

                logger.Info("Number of Queries to Retrieve={0}", this.QueryIDs.Length);
                logger.Info("Role={0}", this.Role);
                loggerConsole.Info("Retrieving {0} Queries using {1} role", this.QueryIDs.Length, this.Role);

                // Get all queries
                Parallel.For(0, this.QueryIDs.Length, (queryIndex, stateQueries) =>
                {
                    string queryID = this.QueryIDs[queryIndex];

                    loggerConsole.Info("Retrieving Query {0}/{1}: {2}", queryIndex + 1, this.QueryIDs.Length, queryID);

                    string queryDetailsFilePath = Path.Combine(this.OutputFolder, String.Format("{0}.details.json", queryID));

                    // Only retrieve this if it hasn't been retrieved yet
                    if (File.Exists(queryDetailsFilePath) == true) return;

                    // First, get details
                    string queryDetailsApiResult = SnowflakeDriver.GetQueryDetails(this.AuthContext.AppServerUrl, this.AuthContext.AccountUrl, this.AuthContext.OrganizationID, this.AuthContext.UserName, this.AuthContext.AuthTokenSnowsight, queryID, this.Role);
                    if (queryDetailsApiResult.Length == 0)
                    {
                        logger.Warn("Getting details for {0} failed", queryID);
                        return;
                    }
                    else
                    {
                        JObject queryDetailsApiObject = JObject.Parse(queryDetailsApiResult);
                        if (queryDetailsApiObject == null)
                        {
                            logger.Warn("Parsing details for {0} failed", queryID);
                            return;
                        }
                        else
                        {
                            if (JSONHelper.getBoolValueFromJToken(queryDetailsApiObject, "success") == false)
                            {
                                logger.Warn("Getting details for {0} returned failure", queryID);
                                return;
                            }
                            else
                            {
                                FileIOHelper.SaveFileToPath(queryDetailsApiResult, queryDetailsFilePath);
                                loggerConsole.Trace("Query {0}/{1}: {2} - details to {3}", queryIndex + 1, this.QueryIDs.Length, queryID, queryDetailsFilePath);
                            }
                        }
                    }

                    // If we got here, we got details successfully
                    if (this.IncludeJobRetries.IsPresent == false)
                    {
                        // Only get latest
                        string queryProfileApiResult = SnowflakeDriver.GetQueryProfile(this.AuthContext.AppServerUrl, this.AuthContext.AccountUrl, this.AuthContext.OrganizationID, this.AuthContext.UserName, this.AuthContext.AuthTokenSnowsight, queryID, this.Role);
                        if (queryProfileApiResult.Length == 0)
                        {
                            logger.Warn("Getting profile for {0} failed", queryID);
                            return;
                        }
                        else
                        {
                            JObject queryProfileApiObject = JObject.Parse(queryProfileApiResult);
                            if (queryProfileApiObject == null)
                            {
                                logger.Warn("Parsing profile for {0} failed", queryID);
                                return;
                            }
                            else
                            {
                                if (JSONHelper.getBoolValueFromJToken(queryProfileApiObject, "success") == false)
                                {
                                    logger.Warn("Getting profile for {0} returned failure", queryID);
                                    return;
                                }
                                else
                                {
                                    string queryProfileFilePath = Path.Combine(this.OutputFolder, String.Format("{0}.profile.0.json", queryID));
                                    FileIOHelper.SaveFileToPath(queryProfileApiResult, queryProfileFilePath);
                                    loggerConsole.Trace("Query {0}/{1}: {2} - profile to {3}", queryIndex + 1, this.QueryIDs.Length, queryID, queryProfileFilePath);
                                }
                            }
                        }
                    }
                    else
                    {
                        // Get up to 3 retries with GS
                        Parallel.For(0, 3, (retryNumber, stateRetries) => 
                        {
                            string queryProfileApiResult = SnowflakeDriver.GetQueryProfile(this.AuthContext.AppServerUrl, this.AuthContext.AccountUrl, this.AuthContext.OrganizationID, this.AuthContext.UserName, this.AuthContext.AuthTokenSnowsight, queryID, this.Role, retryNumber);
                            if (queryProfileApiResult.Length == 0)
                            {
                                logger.Warn("Getting profile for {0} retry number {1} failed", queryID, retryNumber);
                                return;
                            }
                            else
                            {
                                JObject queryProfileApiObject = JObject.Parse(queryProfileApiResult);
                                if (queryProfileApiObject == null)
                                {
                                    logger.Warn("Parsing profile for {0} failed", queryID, retryNumber);
                                    return;
                                }
                                else
                                {
                                    if (JSONHelper.getBoolValueFromJToken(queryProfileApiObject, "success") == false)
                                    {
                                        logger.Warn("Getting profile for {0} returned failure", queryID, retryNumber);
                                        return;
                                    }
                                    else
                                    {
                                        string queryProfileFilePath = Path.Combine(this.OutputFolder, String.Format("{0}.profile.{1}.json", queryID, retryNumber));
                                        FileIOHelper.SaveFileToPath(queryProfileApiResult, queryProfileFilePath);
                                        loggerConsole.Trace("Query {0}/{1}: {2} - job retry {3} profile to {4}", queryIndex + 1, this.QueryIDs.Length, queryID, retryNumber, queryProfileFilePath);
                                    }
                                }
                            }                            
                        });
                    }

                    loggerConsole.Info("Query {0}/{1}: {2} completed", queryIndex + 1, this.QueryIDs.Length, queryID);
                });

                // Parse the queries
                List<Query> queriesList = new List<Query>(this.QueryIDs.Length);
                for (int queryIndex = 0; queryIndex < this.QueryIDs.Length; queryIndex++)
                {
                    string queryID = this.QueryIDs[queryIndex];

                    loggerConsole.Info("Parsing Query {0}/{1}: {2}", queryIndex + 1, this.QueryIDs.Length, queryID);

                    // Check for duplicate
                    if (queriesList.Count(q => q.QueryID == queryID) > 0)
                    {
                        continue;
                    }

                    string queryDetailsFilePath = Path.Combine(this.OutputFolder, String.Format("{0}.details.json", queryID));                    
                    JObject queryDetailsObject = FileIOHelper.LoadJObjectFromFile(queryDetailsFilePath);
                    if (queryDetailsObject == null)
                    {
                        continue;
                    }
                    if (JSONHelper.getBoolValueFromJToken(queryDetailsObject, "success") == false)
                    {
                        continue;
                    }
                    
                    Query query = new Query(queryDetailsObject, this.AuthContext);
                    if (query.QueryID == null || query.QueryID.Length == 0)
                    {
                        query.QueryID = queryID;
                        query.Status = "NO_DATA_RETRIEVED";
                    }

                    logger.Info(query);
                    loggerConsole.Trace("Found Query {0} started on {1} in {2} status", query.QueryID, query.StartedTimeUtc, query.Status);

                    // Now process each of the 1, 2 or 3 steps looking for each step timing
                    for (int retryNumber = 0; retryNumber <=2; retryNumber++)
                    {
                        string queryProfileFilePath = Path.Combine(this.OutputFolder, String.Format("{0}.profile.{1}.json", queryID, retryNumber));
                        JObject queryProfileObject = FileIOHelper.LoadJObjectFromFile(queryProfileFilePath);
                        if (queryProfileObject == null)
                        {
                            continue;
                        }
                        if (JSONHelper.getBoolValueFromJToken(queryProfileObject, "success") == false)
                        {
                            continue;
                        }

                        query.NumRunsXP++;

                        JObject dataObject = (JObject)JSONHelper.getJTokenValueFromJToken(queryProfileObject, "data");
                        if (dataObject != null)
                        {
                            JArray queryStepArray = (JArray)JSONHelper.getJTokenValueFromJToken(dataObject, "steps");
                            if (queryStepArray != null && queryStepArray.Count > 0)
                            {
                                foreach (JObject queryStepObject in queryStepArray)
                                {
                                    QueryStep queryStep = new QueryStep(queryStepObject, this.AuthContext, query);
                                    queryStep.GSRunNumber = retryNumber + 1;

                                    if (queryStep.State == "success")
                                    {
                                        query.TimeXPExecutionSuccess = query.TimeXPExecutionSuccess + queryStep.TimeExecution;
                                    }
                                    else if (queryStep.State == "failure")
                                    {
                                        query.TimeXPExecutionFailure = query.TimeXPExecutionFailure + queryStep.TimeExecution;
                                    }

                                    query.QuerySteps.Add(queryStep);
                                    
                                    if (queryStep.StepNumber > 1000)
                                    {
                                        query.NumRunsXP++;
                                    }
                                }
                            }
                        }

                        query.NumRunsGS = retryNumber + 1;
                    }

                    //query.TimeXPExecutionSuccess = query.QuerySteps.Where(q => q.State == "success").Sum(q => q.TimeExecution);
                    //query.TimeXPExecutionSuccess = query.QuerySteps.Where(q => q.State == "failure").Sum(q => q.TimeExecution);
                    query.DurationXPExecutionSuccess = new TimeSpan(0, 0, 0, 0, query.TimeXPExecutionSuccess);
                    query.DurationXPExecutionFailure = new TimeSpan(0, 0, 0, 0, query.TimeXPExecutionFailure);

                    queriesList.Add(query);
                }

                // Combine all queries steps into one list
                List<QueryStep> queryStepsList = new List<QueryStep>(queriesList.Count * 10);
                foreach (Query query1 in queriesList)
                {
                    queryStepsList.AddRange(query1.QuerySteps);
                }

                // Save results
                loggerConsole.Info("Retrieved {0} Queries", queriesList.Count);
                if (queriesList.Count > 0)
                {
                    DateTime now = DateTime.UtcNow;
                    string queriesCSVFilePath = Path.Combine(this.OutputFolder, String.Format("Queries.{0}.{1:yyyyMMddHHmm}.csv", queriesList.Count, now));
                    FileIOHelper.WriteListToCSVFile<Query>(queriesList, new QueryMap(), queriesCSVFilePath);

                    string queryStepsCSVFilePath = Path.Combine(this.OutputFolder, String.Format("Queries.Steps.{0}.{1:yyyyMMddHHmm}.csv", queriesList.Count, now));
                    FileIOHelper.WriteListToCSVFile<QueryStep>(queryStepsList, new QueryStepMap(), queryStepsCSVFilePath );

                    loggerConsole.Info("Query Info in {0}", queriesCSVFilePath);
                    loggerConsole.Info("Query Steps in {0}", queryStepsCSVFilePath);
                }

                WriteObject(queriesList.ToArray());
            }
            catch (Exception ex)
            {
                logger.Error("{0} threw {1} ({2})", this.GetType().Name, ex.Message, ex.Source);
                logger.Error(ex);

                if (ex is ItemNotFoundException)
                {
                    this.ThrowTerminatingError(new ErrorRecord(ex, "errorid", ErrorCategory.ObjectNotFound, null));
                }
                else if (ex is FileNotFoundException)
                {
                    this.ThrowTerminatingError(new ErrorRecord(ex, "errorid", ErrorCategory.ObjectNotFound, null));
                }
                else if (ex is ArgumentNullException)
                {
                    this.ThrowTerminatingError(new ErrorRecord(ex, "errorid", ErrorCategory.ObjectNotFound, null));
                }
                else
                {
                    this.ThrowTerminatingError(new ErrorRecord(ex, "errorid", ErrorCategory.OperationStopped, null));
                }
            }
            finally
            {
                LogManager.Flush();
            }
        }
    }
}
