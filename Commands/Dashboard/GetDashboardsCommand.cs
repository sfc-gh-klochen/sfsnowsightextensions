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
        "SFDashboards",
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
                string dashboardsApiResult = SnowflakeDriver.GetDashboards(this.AuthContext.AppServerUrl, this.AuthContext.AccountUrl, this.AuthContext.OrganizationID, this.AuthContext.UserName, this.AuthContext.AuthTokenSnowsight);
                if (dashboardsApiResult.Length == 0)
                {
                    throw new ItemNotFoundException("Invalid response from listing dashboard entities");
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

                    Dashboard dashboard = new Dashboard(entityObject, dashboardsPayloadObject, this.AuthContext);

                    // Fill in the chart information
                    foreach (Worksheet worksheet in dashboard.Worksheets)
                    {
                        if (worksheet.Charts.Count > 0)
                        {
                            loggerConsole.Trace("Worksheet {0} ({1}) has {2} charts", worksheet.WorksheetName, worksheet.WorksheetID, worksheet.Charts.Count);
                        }

                        foreach (Chart chart in worksheet.Charts)
                        {
                            loggerConsole.Trace("Getting information about Chart {0} ({1}) in {2} ({3})", chart.ChartName, chart.ChartID, worksheet.WorksheetName, worksheet.WorksheetID);
                            
                            // Get chart details
                            string chartDetailApiResult = SnowflakeDriver.GetChart(this.AuthContext.AppServerUrl, this.AuthContext.AccountUrl, this.AuthContext.OrganizationID, this.AuthContext.UserName, this.AuthContext.AuthTokenSnowsight, chart.WorksheetID, chart.ChartID);
                            
                            if (chartDetailApiResult.Length == 0)
                            {
                                throw new ItemNotFoundException("Invalid response from getting chart detail");
                            }
                            
                            JObject chartDetailPayloadObject = JObject.Parse(chartDetailApiResult);

                            chart.AddConfigurationDetails(chartDetailPayloadObject);                    
                        }
                    }

                    logger.Info(dashboard);
                    loggerConsole.Trace("Found Dashboard {0} ({1})", dashboard.DashboardName, dashboard.DashboardID);

                    dashboardsList.Add(dashboard);
                }

                dashboardsList = dashboardsList.OrderBy(d => d.DashboardName).ToList();

                loggerConsole.Info("Retrieved {0} dashboards", dashboardsList.Count);

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
