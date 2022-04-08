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

namespace Snowflake.Powershell
{
    [Cmdlet
        (VerbsCommon.Get,
        "SFFilters",
        SupportsPaging=false,
        SupportsShouldProcess=false)]
    [OutputType(typeof(String))]
    public class GetFiltersCommand : PSCmdlet
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
                string organizationAndUserContextResult = SnowflakeDriver.GetOrganizationAndUserContext(this.AuthContext);
                if (organizationAndUserContextResult.Length == 0)
                {
                    throw new ItemNotFoundException(String.Format("Invalid response from getting organization context for user {0}@{1}", this.AuthContext.UserName, this.AuthContext.AccountName));
                }

                JObject organizationAndUserContextObject = JObject.Parse(organizationAndUserContextResult);

                // Check for expired token
                if (JSONHelper.getStringValueFromJToken(organizationAndUserContextObject, "Title") == "Sign in required" && 
                    JSONHelper.getStringValueFromJToken(organizationAndUserContextObject, "PageType") == "notFound")
                {
                    throw new ItemNotFoundException(String.Format("Access denied getting organization context for user {0}@{1}", this.AuthContext.UserName, this.AuthContext.AccountName));
                }

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

                List<Filter> filtersList = new List<Filter>(entitiesArray.Count);
                foreach (JObject entityObject in entitiesArray)
                {
                    Filter filter = new Filter(entityObject, this.AuthContext);

                    logger.Info(filter);
                    loggerConsole.Trace("Found Filter {0} ({1}) [{2}]", filter.DisplayName, filter.Keyword, filter.Type);

                    // Enrich the query filters with the query information
                    if (filter.Type == "query" )
                    {
                        // Sometimes Query filters get saved in a buggy way and don't point to a valid worksheet
                        if (filter.WorksheetID.Length == 0)
                        {
                            // Enrich the query filters with the query information
                            logger.Warn("Filter {0} says query, but has no worksheet ", filter);
                            loggerConsole.Warn("Filter {0} ({1}) [{2}] does not have a valid worksheet associated. Skipping", filter.DisplayName, filter.Keyword, filter.Type);

                            continue;
                        }            
                        else
                        {
                            logger.Info("Getting query detail for {0}", filter);
                            loggerConsole.Trace("Getting Query Details for Filter {0} ({1}) [{2}]", filter.DisplayName, filter.Keyword, filter.Type);

                            string worksheetApiResult = SnowflakeDriver.GetWorksheet(this.AuthContext, filter.WorksheetID);

                            if (worksheetApiResult.Length == 0)
                            {
                                logger.Warn("Worksheet {0} didn't exist", filter.WorksheetID);
                                loggerConsole.Warn("Worksheet {0} didn't exist", filter.WorksheetID);
                            }
                            else
                            {
                                JObject worksheetsPayloadObject = JObject.Parse(worksheetApiResult);

                                filter.Worksheet = new Worksheet(filter.WorksheetID, worksheetsPayloadObject, this.AuthContext);

                                logger.Info(filter.Worksheet);
                                loggerConsole.Trace("Found Worksheet {0} ({1})", filter.Worksheet.WorksheetName, filter.Worksheet.WorksheetID);
                            }
                        }
                    }

                    filtersList.Add(filter);
                }

                filtersList = filtersList.OrderBy(w => w.Scope).ThenBy(w => w.DisplayName).ToList();

                loggerConsole.Info("Retrieved {0} Filters", filtersList.Count);

                WriteObject(filtersList.ToArray());
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
