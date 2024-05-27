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
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management.Automation;
using System.Text.RegularExpressions;

namespace Snowflake.Powershell
{
    [Cmdlet
        (VerbsCommon.Get,
        "SFWorksheets",
        DefaultParameterSetName="WorksheetName",
        SupportsPaging=false,
        SupportsShouldProcess=false)]
    [OutputType(typeof(String))]
    public class GetWorksheetsCommand : PSCmdlet
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
            Mandatory = false,
            Position = 1,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true,
            HelpMessage = "Name or pattern of Worksheet to retrieve",
            ParameterSetName = "WorksheetName")]
        public string WorksheetNameRegex { get; set; }

        [Parameter(
            Mandatory = false,
            Position = 1,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true,
            HelpMessage = "ID of Worksheet to retrieve",
            ParameterSetName = "WorksheetID")]
        public string WorksheetID { get; set; }

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
                string worksheetsApiResult = SnowflakeDriver.GetWorksheets(this.AuthContext);
                if (worksheetsApiResult.Length == 0)
                {
                    throw new ItemNotFoundException(String.Format("Invalid response from listing Worksheet entities"));
                }

                JObject worksheetsPayloadObject = JObject.Parse(worksheetsApiResult);

                JArray entitiesArray = new JArray();
                if (JSONHelper.isTokenPropertyNull(worksheetsPayloadObject, "entities") == false)
                {
                    entitiesArray = (JArray)worksheetsPayloadObject["entities"];
                }
                logger.Info("Number of Entities={0}", entitiesArray.Count);

                List<Worksheet> worksheetsList = new List<Worksheet>(entitiesArray.Count);
                foreach (JObject entityObject in entitiesArray)
                {
                    // Only deal with "query" objects, which are worksheets
                    if (JSONHelper.getStringValueFromJToken(entityObject, "entityType") != "query") continue;

                    Worksheet potentialTargetWorksheet = new Worksheet(entityObject, worksheetsPayloadObject, this.AuthContext);
                    logger.Info(potentialTargetWorksheet);

                    bool keepThisObject = false;

                    switch (this.ParameterSetName)
                    {                 
                        case "WorksheetName":
                            if (this.WorksheetNameRegex == null || this.WorksheetNameRegex.Length == 0)
                            {
                                // If no parameter passed, assume them all
                                keepThisObject = true;
                            }
                            else
                            {
                                // Adjust wildcard regex into a real wildcard regex
                                // Just in case the users don't know how to use regex and just do a wildcard
                                if (String.Compare(this.WorksheetNameRegex, "*", true) == 0)
                                {
                                    this.WorksheetNameRegex = ".*";
                                }
                                Regex regexVersion = new Regex(this.WorksheetNameRegex, RegexOptions.IgnoreCase);
                                Match match = regexVersion.Match(potentialTargetWorksheet.WorksheetName);
                                if (match != null)
                                {
                                    if (match.Success)
                                    {
                                        logger.Info("Found Match by Worksheet Name Regex: {0}={1}", this.WorksheetNameRegex, potentialTargetWorksheet);
                                        keepThisObject = true;
                                    }
                                }
                            }
                            break;

                        case "WorksheetID":                            
                            if (this.WorksheetID == potentialTargetWorksheet.WorksheetID) 
                            {
                                logger.Info("Found Match by ID: {0}={1}", this.WorksheetID, potentialTargetWorksheet);
                                keepThisObject = true;
                            }
                            break;

                        default:
                            throw new ArgumentException(String.Format("Unknown parameter set {0}", this.ParameterSetName));
                    }

                    if (keepThisObject == true)
                    {
                        loggerConsole.Trace("Found Worksheet {0} ({1})", potentialTargetWorksheet.WorksheetName, potentialTargetWorksheet.WorksheetID);

                        worksheetsList.Add(potentialTargetWorksheet);
                    }
                }

                worksheetsList = worksheetsList.OrderBy(w => w.FolderName).ThenBy(w => w.WorksheetName).ToList();

                loggerConsole.Info("Retrieved {0} Worksheets", worksheetsList.Count);

                WriteObject(worksheetsList.ToArray());
            }
            catch (Exception ex)
            {
                logger.Error("{0} threw {1} ({2})", this.GetType().Name, ex.Message, ex.Source);
                logger.Error(ex);

                if (ex is ItemNotFoundException)
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
