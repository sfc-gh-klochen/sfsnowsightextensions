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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.IO;
using System.Management.Automation;

namespace Snowflake.Powershell
{
    [Cmdlet
        (VerbsCommon.New,
        "SFFilter",
        DefaultParameterSetName="WorksheetFile",
        SupportsPaging=false,
        SupportsShouldProcess=false)]
    [OutputType(typeof(String))]
    public class NewFilterCommand : PSCmdlet
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
            HelpMessage = "Filter object of Filter to create or update",
            ParameterSetName = "FilterObject")]
        public Filter Filter { get; set; }

        [Parameter(
            Mandatory = true,
            Position = 1,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true,
            HelpMessage = "File representation of Filter to create or update",
            ParameterSetName = "FilterFile")]
        public string FilterFile { get; set; }

        [Parameter(
            Mandatory = false,
            Position = 2,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true,
            HelpMessage = "What to do when the Filter already exists")]
        [ValidateSet ("Overwrite", "Skip")]
        public string ActionIfExists { get; set; } = "Skip";

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
                    case "FilterObject":
                        break;

                    case "FilterFile":
                        if (File.Exists(this.FilterFile) == false)
                        {
                            throw new FileNotFoundException(String.Format("No Worksheet file found at {0}", this.FilterFile));
                        }

                        this.Filter = JsonConvert.DeserializeObject<Filter>(FileIOHelper.ReadFileFromPath(this.FilterFile));
                        if (this.Filter == null)
                        {
                            throw new ArgumentNullException(String.Format("Unable to convert file found at {0} to Filter", this.FilterFile));
                        }

                        break;

                    default:
                        throw new ArgumentException(String.Format("Unknown parameter set {0}", this.ParameterSetName));
                }

                // Check if this filter is built-in
                if (this.Filter != null && this.Filter.Scope == "global")
                {
                    throw new InvalidOperationException(String.Format("Unable to create or update 'global' level filter {0} because it should already exist", this.Filter));
                }
                if (this.Filter.Keyword.ToLower() == "datebucket" ||
                    this.Filter.Keyword.ToLower() == "daterange" ||
                    this.Filter.Keyword.ToLower() == "timezone")
                {
                    throw new InvalidOperationException(String.Format("Unable to create or update built-in filter {0}", this.Filter.Keyword));
                }

                Filter targetFilterToReplace = null;

                // Get list of current Filters in case the Filter we're creating already exists
                string organizationAndUserContextResult = SnowflakeDriver.GetOrganizationAndUserContext(this.AuthContext).Item1;
                if (organizationAndUserContextResult.Length == 0)
                {
                    throw new ItemNotFoundException(String.Format("Invalid response from getting organization context for user {0}@{1}", this.AuthContext.UserName, this.AuthContext.AccountName));
                }

                JObject organizationAndUserContextObject = JObject.Parse(organizationAndUserContextResult);

                JArray entitiesArray = new JArray();
                if (JSONHelper.isTokenPropertyNull(organizationAndUserContextObject, "Org") == false)
                {
                    JObject organizationObject = (JObject)organizationAndUserContextObject["Org"];
                    if (JSONHelper.isTokenPropertyNull(organizationObject, "settings") == false)
                    {
                        JObject settingsObject = (JObject)organizationObject["settings"];
                        if (JSONHelper.isTokenPropertyNull(settingsObject, "paramConfigs") == false)
                        {
                            entitiesArray = (JArray)settingsObject["paramConfigs"];
                        }
                    }
                }
                logger.Info("Number of Entities={0}", entitiesArray.Count);

                foreach (JObject entityObject in entitiesArray)
                {
                    Filter potentialTargetFilter = new Filter(entityObject, this.AuthContext);

                    if (this.Filter.Keyword == potentialTargetFilter.Keyword) 
                    {
                        targetFilterToReplace = potentialTargetFilter;

                        logger.Info("Found Match by ID: {0}={1}", this.Filter, targetFilterToReplace);

                        // Enrich the query filters with the query information
                        if (targetFilterToReplace.Type == "query" )
                        {
                            // Sometimes Query filters get saved in a buggy way and don't point to a valid worksheet
                            if (targetFilterToReplace.WorksheetID.Length == 0)
                            {
                                // Enrich the query filters with the query information
                                logger.Warn("Filter {0} says query, but has no worksheet ", targetFilterToReplace);
                                loggerConsole.Warn("Filter {0} ({1}) [{2}] does not have a valid worksheet associated. Skipping", targetFilterToReplace.DisplayName, targetFilterToReplace.Keyword, targetFilterToReplace.Type);

                                continue;
                            }            
                            else
                            {
                                logger.Info("Getting query detail for {0}", targetFilterToReplace);
                                loggerConsole.Trace("Getting Query Details for Filter {0} ({1}) [{2}]", targetFilterToReplace.DisplayName, targetFilterToReplace.Keyword, targetFilterToReplace.Type);

                                string worksheetApiResult = SnowflakeDriver.GetWorksheet(this.AuthContext, targetFilterToReplace.WorksheetID);

                                if (worksheetApiResult.Length == 0)
                                {
                                    logger.Warn("Worksheet {0} didn't exist", targetFilterToReplace.WorksheetID);
                                    loggerConsole.Warn("Worksheet {0} didn't exist", targetFilterToReplace.WorksheetID);
                                }
                                else
                                {
                                    JObject worksheetsPayloadObject = JObject.Parse(worksheetApiResult);

                                    targetFilterToReplace.Worksheet = new Worksheet(targetFilterToReplace.WorksheetID, worksheetsPayloadObject, this.AuthContext);

                                    logger.Info(targetFilterToReplace.Worksheet);
                                    loggerConsole.Trace("Found Worksheet {0} ({1})", targetFilterToReplace.Worksheet.WorksheetName, targetFilterToReplace.Worksheet.WorksheetID);
                                }
                                

                            }
                        }

                        break;
                    }
                }

                // At this point, we either have the filter to update, or the filter we're trying to import is new
                if (targetFilterToReplace != null)
                {
                    // Updating existing worksheet
                    switch (this.ActionIfExists)
                    {
                        case "Overwrite":
                            logger.Info("Found {0} to overwrite and ActionIfExists={1}, will overwrite", targetFilterToReplace, this.ActionIfExists);
                            loggerConsole.Info("Existing Worksheet {0} ({1}) will be overwritten because ActionIfExists is {2}", targetFilterToReplace.DisplayName, targetFilterToReplace.Keyword, this.ActionIfExists);
                            
                            break;
                        
                        case "Skip":
                            logger.Info("Found {0} to overwrite but ActionIfExists={1}, will skip", targetFilterToReplace, this.ActionIfExists);
                            loggerConsole.Info("Existing Worksheet {0} ({1}) will be ignored and nothing will be done because ActionIfExists is {2}", targetFilterToReplace.DisplayName, targetFilterToReplace.Keyword, this.ActionIfExists);
                        
                            return;

                        default:
                            throw new ArgumentException(String.Format("Unknown ActionIfExists parameter {0}", this.ActionIfExists));
                    }
                }
                else
                {
                    logger.Info("No match for {0}, new one will be created", this.Filter);
                    loggerConsole.Info("Creating new Filter {0} ({1})", this.Filter.DisplayName, this.Filter.Keyword);
                }

                Filter createdOrUpdatedFilter = null;
                Worksheet createdOrUpdatedWorksheet = null;
                
                // Now actually make modifications
                if (targetFilterToReplace != null)
                {
                    // Updating existing filter
                    if (this.Filter.Type == "query")
                    {
                        // Updating existing worksheet
                        string updateWorksheetApiResult = SnowflakeDriver.UpdateWorksheet(this.AuthContext, targetFilterToReplace.Worksheet.WorksheetID, this.Filter.Worksheet.Query, this.Filter.Worksheet.Role, this.Filter.Worksheet.Warehouse, this.Filter.Worksheet.Database, this.Filter.Worksheet.Schema);

                        JObject worksheetsPayloadObject = JObject.Parse(updateWorksheetApiResult);

                        if (updateWorksheetApiResult.Length == 0)
                        {
                            throw new ItemNotFoundException("Invalid response from updating existing Worksheet");
                        }

                        JObject updateWorksheetPayloadObject = JObject.Parse(updateWorksheetApiResult);

                        createdOrUpdatedWorksheet = new Worksheet(targetFilterToReplace.Worksheet.WorksheetID, updateWorksheetPayloadObject, this.AuthContext);

                        logger.Info("Returning updated {0}", createdOrUpdatedWorksheet);

                        // Update pointer to the Worksheet for the filter
                        this.Filter.Configuration["settings"]["source"]["pid"] = targetFilterToReplace.Worksheet.WorksheetID;
                    }
                }
                else
                {
                    // Creating new filter
                    if (this.Filter.Type == "query")
                    {
                        // Precreate worksheet and get its ID

                        // Creating new worksheet
                        string createWorksheetApiResult = SnowflakeDriver.CreateWorksheetForFilter(this.AuthContext, this.Filter.Worksheet.WorksheetName, this.Filter.Worksheet.Role, this.Filter.Worksheet.Warehouse, this.Filter.Worksheet.Database, this.Filter.Worksheet.Schema);

                        if (createWorksheetApiResult.Length == 0)
                        {
                            throw new ItemNotFoundException("Invalid response from creating new Worksheet");
                        }

                        JObject createWorksheetPayloadObject = JObject.Parse(createWorksheetApiResult);
                        string newWorksheetID = JSONHelper.getStringValueFromJToken(createWorksheetPayloadObject, "pid");
                        logger.Info("New WorksheetID={0}", newWorksheetID);

                        string updateWorksheetApiResult = SnowflakeDriver.UpdateWorksheet(this.AuthContext, newWorksheetID, this.Filter.Worksheet.Query, this.Filter.Worksheet.Role, this.Filter.Worksheet.Warehouse, this.Filter.Worksheet.Database, this.Filter.Worksheet.Schema);

                        if (updateWorksheetApiResult.Length == 0)
                        {
                            throw new ItemNotFoundException("Invalid response from updating existing worksheet");
                        }

                        JObject updateWorksheetPayloadObject = JObject.Parse(updateWorksheetApiResult);

                        createdOrUpdatedWorksheet = new Worksheet(newWorksheetID, updateWorksheetPayloadObject, this.AuthContext);

                        logger.Info("Created new {0}", createdOrUpdatedWorksheet);

                        // Update pointer to the Worksheet for the filter
                        this.Filter.Configuration["settings"]["source"]["pid"] = newWorksheetID;
                    }
                }

                if (createdOrUpdatedWorksheet != null)
                {
                    logger.Info("Running Worksheet {0}", createdOrUpdatedWorksheet);
                    loggerConsole.Trace("Running Worksheet {0} ({1})", createdOrUpdatedWorksheet.WorksheetName, createdOrUpdatedWorksheet.WorksheetID);

                    string executeWorksheetApiResult = SnowflakeDriver.ExecuteWorksheet(this.AuthContext, 
                        createdOrUpdatedWorksheet.WorksheetID, createdOrUpdatedWorksheet.Query, createdOrUpdatedWorksheet.Parameters.ToString(Newtonsoft.Json.Formatting.None),
                        createdOrUpdatedWorksheet.Role, createdOrUpdatedWorksheet.Warehouse, createdOrUpdatedWorksheet.Database, createdOrUpdatedWorksheet.Schema);                        
                }                

                // Save new or existing worksheet filter configuration
                string createOrUpdateFilterApiResult = SnowflakeDriver.CreateOrUpdateFilter(this.AuthContext, this.Filter.Keyword, this.Filter.Configuration.ToString(Newtonsoft.Json.Formatting.None));

                if (createOrUpdateFilterApiResult.Length == 0)
                {
                    throw new ItemNotFoundException("Invalid response from creating or updating Filter");
                }

                // Parse the result of filter create or update
                JObject createOrUpdateFilterPayloadObject = JObject.Parse(createOrUpdateFilterApiResult);

                entitiesArray = new JArray();
                if (JSONHelper.isTokenPropertyNull(createOrUpdateFilterPayloadObject, "organization") == false)
                {
                    JObject organizationObject = (JObject)createOrUpdateFilterPayloadObject["organization"];
                    if (JSONHelper.isTokenPropertyNull(organizationObject, "settings") == false)
                    {
                        JObject settingsObject = (JObject)organizationObject["settings"];
                        if (JSONHelper.isTokenPropertyNull(settingsObject, "paramConfigs") == false)
                        {
                            entitiesArray = (JArray)settingsObject["paramConfigs"];
                        }
                    }
                }
                logger.Info("Number of Entities={0}", entitiesArray.Count);

                foreach (JObject entityObject in entitiesArray)
                {
                    Filter potentialTargetFilter = new Filter(entityObject, this.AuthContext);

                    if (this.Filter.Keyword == potentialTargetFilter.Keyword) 
                    {
                        createdOrUpdatedFilter = potentialTargetFilter;

                        logger.Info("Found Match by ID: {0}={1}", this.Filter, createdOrUpdatedFilter);
                        break;
                    }
                }

                if (createdOrUpdatedFilter == null)
                {
                    throw new FileNotFoundException(String.Format("Creation succeeded? But no created Filter found for {0}", this.Filter.Keyword));
                }
                else
                {
                    createdOrUpdatedFilter.Worksheet = createdOrUpdatedWorksheet;

                    logger.Info("Returning created or updated {0}", createdOrUpdatedFilter);

                    WriteObject(createdOrUpdatedFilter);
                }
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
