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
        "SFDashboards",
        DefaultParameterSetName="DashboardName",
        SupportsPaging=false,
        SupportsShouldProcess=false)]
    [OutputType(typeof(String))]
    public class GetDashboardsCommand : PSCmdlet
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
            HelpMessage = "Name or pattern of Dashboard to retrieve",
            ParameterSetName = "DashboardName")]
        public string DashboardNameRegex { get; set; }

        [Parameter(
            Mandatory = false,
            Position = 1,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true,
            HelpMessage = "ID of Dashboard to retrieve",
            ParameterSetName = "DashboardID")]
        public string DashboardID { get; set; }

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
                string dashboardsApiResult = SnowflakeDriver.GetDashboards(this.AuthContext);
                if (dashboardsApiResult.Length == 0)
                {
                    throw new ItemNotFoundException("Invalid response from listing Dashboard entities");
                }
                
                JObject dashboardsPayloadObject = JObject.Parse(dashboardsApiResult);

                JArray entitiesArray = new JArray();
                if (JSONHelper.isTokenPropertyNull(dashboardsPayloadObject, "entities") == false)
                {
                    entitiesArray = (JArray)dashboardsPayloadObject["entities"];
                }
                logger.Info("Number of Entities={0}", entitiesArray.Count);

                List<Dashboard> dashboardsList = new List<Dashboard>(entitiesArray.Count);
                foreach (JObject entityObject in entitiesArray)
                {
                    // Only deal with "folder" objects, which are dashboards
                    if (JSONHelper.getStringValueFromJToken(entityObject, "entityType") != "folder") continue;

                    Dashboard potentialTargetDashboard = new Dashboard(entityObject, dashboardsPayloadObject, this.AuthContext);

                    logger.Info(potentialTargetDashboard);

                    bool keepThisObject = false;

                    switch (this.ParameterSetName)
                    {                 
                        case "DashboardName":
                            if (this.DashboardNameRegex == null || this.DashboardNameRegex.Length == 0)
                            {
                                // If no parameter passed, assume them all
                                keepThisObject = true;
                            }
                            else
                            {
                                // Adjust wildcard regex into a real wildcard regex
                                // Just in case the users don't know how to use regex and just do a wildcard
                                if (String.Compare(this.DashboardNameRegex, "*", true) == 0)
                                {
                                    this.DashboardNameRegex = ".*";
                                }
                                Regex regexVersion = new Regex(this.DashboardNameRegex, RegexOptions.IgnoreCase);
                                Match match = regexVersion.Match(potentialTargetDashboard.DashboardName);
                                if (match != null)
                                {
                                    if (match.Success)
                                    {
                                        logger.Info("Found Match by Dashboard Name Regex: {0}={1}", this.DashboardNameRegex, potentialTargetDashboard);
                                        keepThisObject = true;
                                    }
                                }                                
                            }
                            break;

                        case "DashboardID":                            
                            if (this.DashboardID == potentialTargetDashboard.DashboardID) 
                            {
                                logger.Info("Found Match by ID: {0}={1}", this.DashboardID, potentialTargetDashboard);
                                keepThisObject = true;
                            }
                            break;

                        default:
                            throw new ArgumentException(String.Format("Unknown parameter set {0}", this.ParameterSetName));
                    }

                    if (keepThisObject == true)
                    {
                        loggerConsole.Trace("Found Dashboard {0} ({1}) with {2} Worksheets", potentialTargetDashboard.DashboardName, potentialTargetDashboard.DashboardID, potentialTargetDashboard.Worksheets.Count);

                        // Fill in the chart information
                        for (int i = 0; i < potentialTargetDashboard.Worksheets.Count; i++)
                        {
                            Worksheet worksheet = potentialTargetDashboard.Worksheets[i];

                            if (worksheet.Charts.Count > 0)
                            {
                                loggerConsole.Trace("{0}/{1}: Worksheet {2} ({3}) has {4} charts", i + 1, potentialTargetDashboard.Worksheets.Count, worksheet.WorksheetName, worksheet.WorksheetID, worksheet.Charts.Count);
                            }

                            foreach (Chart chart in worksheet.Charts)
                            {
                                loggerConsole.Trace("Worksheet {0} ({1}), details of Chart {2} ({3})", worksheet.WorksheetName, worksheet.WorksheetID, chart.ChartName, chart.ChartID);
                                
                                // Get chart details
                                string chartDetailApiResult = SnowflakeDriver.GetChart(this.AuthContext, chart.WorksheetID, chart.ChartID);
                                
                                if (chartDetailApiResult.Length == 0)
                                {
                                    logger.Warn("Invalid response from getting chart detail");
                                    continue;
                                }                            
                                
                                JObject chartDetailPayloadObject = JObject.Parse(chartDetailApiResult);

                                chart.AddConfigurationDetails(chartDetailPayloadObject);                    
                            }
                        }

                        dashboardsList.Add(potentialTargetDashboard);
                    }
                }

                dashboardsList = dashboardsList.OrderBy(d => d.DashboardName).ToList();

                loggerConsole.Info("Retrieved {0} Dashboards", dashboardsList.Count);

                WriteObject(dashboardsList.ToArray());                
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
