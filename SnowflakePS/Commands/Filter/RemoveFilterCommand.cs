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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management.Automation;

namespace Snowflake.Powershell
{
    [Cmdlet
        (VerbsCommon.Remove,
        "SFFilter",
        DefaultParameterSetName="FilterName",
        SupportsPaging=false,
        SupportsShouldProcess=false)]
    [OutputType(typeof(String))]
    public class RemoveFilterCommand : PSCmdlet
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
            HelpMessage = "Keyword of Filter to delete",
            ParameterSetName = "FilterName")]
        public string FilterKeyword { get; set; }

        [Parameter(
            Mandatory = true,
            Position = 1,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true,
            HelpMessage = "Filter object of Filter to delete",
            ParameterSetName = "FilterObject")]
        public Filter Filter { get; set; }

        [Parameter(
            Mandatory = true,
            Position = 1,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true,
            HelpMessage = "File representation of Filter to delete",
            ParameterSetName = "FilterFile")]
        public string FilterFile { get; set; }

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
                        this.FilterKeyword = this.Filter.Keyword;

                        break;

                    case "FilterFile":
                        if (File.Exists(this.FilterFile) == false)
                        {
                            throw new FileNotFoundException(String.Format("No Filter file found at {0}", this.FilterFile));
                        }

                        this.Filter = JsonConvert.DeserializeObject<Filter>(FileIOHelper.ReadFileFromPath(this.FilterFile));
                        if (this.Filter == null)
                        {
                            throw new ArgumentNullException(String.Format("Unable to convert file found at {0} to Filter", this.FilterFile));
                        }

                        this.FilterKeyword = this.Filter.Keyword;

                        break;

                    case "FilterName":
                        break;

                    default:
                        throw new ArgumentException(String.Format("Unknown parameter set {0}", this.ParameterSetName));
                }

                // Check if this filter is built-in
                if (this.Filter != null && this.Filter.Scope == "global")
                {
                    throw new InvalidOperationException(String.Format("Unable to delete 'global' level filter {0}", this.Filter));
                }
                if (this.FilterKeyword.Length > 0 &&
                    (this.FilterKeyword.ToLower() == "datebucket" ||
                     this.FilterKeyword.ToLower() == "daterange" ||
                     this.FilterKeyword.ToLower() == "timezone"))
                {
                    throw new InvalidOperationException(String.Format("Unable to delete built-in filter {0}", this.FilterKeyword));
                }

                logger.Info("Deleting {0} Filter", this.FilterKeyword);
                loggerConsole.Info("Deleting {0} Filter", this.FilterKeyword);

                // Delete the Filter
                string filterDeleteApiResult = SnowflakeDriver.DeleteFilter(this.AuthContext, this.FilterKeyword);
                if (filterDeleteApiResult.Length == 0)
                {
                    throw new ItemNotFoundException("Invalid response from deleting Filter entity");
                }

                JObject filterDeleteResultObject = JObject.Parse(filterDeleteApiResult);
                if (JSONHelper.isTokenPropertyNull(filterDeleteResultObject, "error") == false)
                {
                    throw new InvalidOperationException(String.Format("Unable to delete 'org' level filter {0} because of {1}", this.FilterKeyword, filterDeleteResultObject["error"]["message"]));
                }
                
                if (JSONHelper.isTokenPropertyNull(filterDeleteResultObject, "inUseParams") == false)
                {
                    JArray objectsUsingFilterArray = (JArray)filterDeleteResultObject["inUseParams"];
                    if (objectsUsingFilterArray != null && objectsUsingFilterArray.Count > 0)
                    {
                        throw new InvalidOperationException(String.Format("Unable to delete filter {0} because it us used by {1} worksheets/dashboards", this.FilterKeyword, objectsUsingFilterArray.Count));
                    }
                }

                WriteObject(String.Format("Deleted {0} Filter", this.FilterKeyword));
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
