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
using System.Diagnostics;
using System.IO;
using System.Management.Automation;

namespace Snowflake.Powershell
{
    [Cmdlet
        (VerbsLifecycle.Invoke,
        "SFWorksheet",
        DefaultParameterSetName="WorksheetName",
        SupportsPaging=false,
        SupportsShouldProcess=false)]
    [OutputType(typeof(String))]
    public class InvokeWorksheetCommand : PSCmdlet
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
            HelpMessage = "Name of Worksheet to execute",
            ParameterSetName = "WorksheetName")]
        public string WorksheetName { get; set; }

        [Parameter(
            Mandatory = true,
            Position = 1,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true,
            HelpMessage = "ID of Worksheet to execute",
            ParameterSetName = "WorksheetID")]
        public string WorksheetID { get; set; }

        [Parameter(
            Mandatory = true,
            Position = 1,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true,
            HelpMessage = "Worksheet object of Worksheet to execute",
            ParameterSetName = "WorksheetObject")]
        public Worksheet Worksheet { get; set; }

        [Parameter(
            Mandatory = true,
            Position = 1,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true,
            HelpMessage = "File representation of Worksheet to execute",
            ParameterSetName = "WorksheetFile")]
        public string WorksheetFile { get; set; }

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
                logger.Info("ParameterSetName={0}", this.ParameterSetName);

                switch (this.ParameterSetName)
                {                 
                    case "WorksheetObject":
                        break;

                    case "WorksheetFile":
                        if (File.Exists(this.WorksheetFile) == false)
                        {
                            throw new FileNotFoundException(String.Format("No Worksheet file found at {0}", this.WorksheetFile));
                        }

                        this.Worksheet = JsonConvert.DeserializeObject<Worksheet>(FileIOHelper.ReadFileFromPath(this.WorksheetFile));
                        if (this.Worksheet == null)
                        {
                            throw new ArgumentNullException(String.Format("Unable to convert file found at {0} to Worksheet", this.WorksheetFile));
                        }

                        break;

                    case "WorksheetName":
                    case "WorksheetID":
                        break;

                    default:
                        throw new ArgumentException(String.Format("Unknown parameter set {0}", this.ParameterSetName));
                }

                // Get all worksheets already present
                string worksheetsApiResult = SnowflakeDriver.GetWorksheets(this.AuthContext.MainAppUrl, this.AuthContext.AppServerUrl, this.AuthContext.AccountUrl, this.AuthContext.OrganizationID, this.AuthContext.UserName, this.AuthContext.AuthTokenSnowsight);
                if (worksheetsApiResult.Length == 0)
                {
                    throw new ItemNotFoundException("Invalid response from listing worksheet entities");
                }

                JObject worksheetsPayloadObject = JObject.Parse(worksheetsApiResult);

                JArray entitiesArray = new JArray();
                if (JSONHelper.isTokenPropertyNull(worksheetsPayloadObject, "entities") == false)
                {
                    entitiesArray = (JArray)worksheetsPayloadObject["entities"];
                }
                logger.Info("Number of Entities={0}", entitiesArray.Count);

                List<Worksheet> worksheetsToExecuteList = new List<Worksheet>(entitiesArray.Count);
                foreach (JObject entityObject in entitiesArray)
                {
                    // Only deal with "query" objects, which are worksheets
                    if (JSONHelper.getStringValueFromJToken(entityObject, "entityType") != "query") continue;

                    Worksheet potentialTargetWorksheet = new Worksheet(entityObject, worksheetsPayloadObject, this.AuthContext);

                    switch (this.ParameterSetName)
                    {                 
                        case "WorksheetName":
                            if (String.Compare(this.WorksheetName, potentialTargetWorksheet.WorksheetName, true) == 0) 
                            {
                                logger.Info("Found Match by Name: {0}={1}", this.WorksheetName, potentialTargetWorksheet);
                                
                                worksheetsToExecuteList.Add(potentialTargetWorksheet);
                            }
                            break;

                        case "WorksheetID":                            
                            if (this.WorksheetID == potentialTargetWorksheet.WorksheetID) 
                            {
                                logger.Info("Found Match by ID: {0}={1}", this.WorksheetID, potentialTargetWorksheet);

                                worksheetsToExecuteList.Add(potentialTargetWorksheet);
                            }
                            break;

                        case "WorksheetFile":
                        case "WorksheetObject":
                            if (this.Worksheet.WorksheetID == potentialTargetWorksheet.WorksheetID) 
                            {
                                logger.Info("Found Match by ID: {0}={1}", this.Worksheet.WorksheetID, potentialTargetWorksheet);

                                worksheetsToExecuteList.Add(potentialTargetWorksheet);
                            }
                            else if (String.Compare(this.Worksheet.WorksheetName, potentialTargetWorksheet.WorksheetName, true) == 0) 
                            {
                                logger.Info("Found Match by Name: {0}={1}", this.Worksheet.WorksheetName, potentialTargetWorksheet);
                                
                                worksheetsToExecuteList.Add(potentialTargetWorksheet);
                            }
                            break;

                        default:
                            throw new ArgumentException(String.Format("Unknown parameter set {0}", this.ParameterSetName));
                    }
                }

                logger.Info("Number of Worksheets to Execute={0}", worksheetsToExecuteList.Count);
                loggerConsole.Info("Executing {0} Worksheets", worksheetsToExecuteList.Count);

                foreach (Worksheet worksheet in worksheetsToExecuteList)
                {
                    logger.Info("Running {0}", worksheet);
                    loggerConsole.Trace("Running Worksheet {0} ({1})", worksheet.WorksheetName, worksheet.WorksheetID);

                    // Execute the worksheet
                    string executeWorksheetApiResult = SnowflakeDriver.ExecuteWorksheet(
                        this.AuthContext.MainAppUrl, this.AuthContext.AppServerUrl, this.AuthContext.AccountUrl, this.AuthContext.UserName, this.AuthContext.AuthTokenSnowsight, 
                        worksheet.WorksheetID, worksheet.Query, worksheet.Parameters.ToString(Newtonsoft.Json.Formatting.None),
                        worksheet.Role, worksheet.Warehouse, worksheet.Database, worksheet.Schema);

                    // Check results
                    JObject executeWorksheetPayloadObject = JObject.Parse(executeWorksheetApiResult);
                    
                    JObject queriesObject = new JObject();
                    if (JSONHelper.isTokenPropertyNull(executeWorksheetPayloadObject["models"], "queries") == false)
                    {
                        queriesObject = (JObject)executeWorksheetPayloadObject["models"]["queries"];
                    }
                    JObject queryResultsObject = new JObject();
                    if (JSONHelper.isTokenPropertyNull(executeWorksheetPayloadObject["models"], "queryResults") == false)
                    {
                        queryResultsObject = (JObject)executeWorksheetPayloadObject["models"]["queryResults"];
                    }

                    JObject queryResultObject = (JObject)JSONHelper.getJTokenValueFromJToken(queryResultsObject, worksheet.WorksheetID);
                    if (queryResultObject != null)
                    {
                        string queryID = JSONHelper.getStringValueFromJToken(queryResultObject, "snowflakeQueryId");

                        DateTime dateTimeValue = DateTime.MinValue;
                        dateTimeValue = JSONHelper.getDateTimeValueFromJToken(queryResultObject, "modified");
                        if (dateTimeValue == DateTime.MinValue)
                        {
                            if (DateTime.TryParse(JSONHelper.getStringValueFromJToken(queryResultObject, "modified"), out dateTimeValue) == true) dateTimeValue = dateTimeValue.ToUniversalTime();
                        }
                        else
                        {
                            dateTimeValue = dateTimeValue.ToUniversalTime();
                        }

                        if (JSONHelper.isTokenNull(queryResultObject["error"]) == true)
                        {
                            logger.Info("Query {0} at {1} succeeded", queryID, dateTimeValue);
                            loggerConsole.Info("Query {0} at {1} succeeded", queryID, dateTimeValue);
                        }
                        else
                        {
                            string errorMessage = JSONHelper.getStringValueFromJToken(queryResultObject["error"], "message");

                            logger.Error("Query {0} at {1} failed with {2}", queryID, dateTimeValue, errorMessage);
                            loggerConsole.Error("Query {0} at {1} failed with {2}", queryID, dateTimeValue, errorMessage);
                        }
                    }
                }

                WriteObject(String.Format("Executed {0} Worksheets", worksheetsToExecuteList.Count));
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
