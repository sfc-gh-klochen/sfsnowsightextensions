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
        "SFFolders",
        SupportsPaging=false,
        SupportsShouldProcess=false)]
    [OutputType(typeof(String))]
    public class GetFoldersCommand : PSCmdlet
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
                string foldersApiResult = SnowflakeDriver.GetFolders(this.AuthContext.AppServerUrl, this.AuthContext.AccountUrl, this.AuthContext.OrganizationID, this.AuthContext.UserName, this.AuthContext.AuthTokenSnowsight);
                if (foldersApiResult.Length == 0)
                {
                    throw new ItemNotFoundException(String.Format("Invalid response from listing folder entities"));
                }
                
                List<Folder> foldersList = new List<Folder>();

                JObject foldersPayloadObject = JObject.Parse(foldersApiResult);

                JArray entitiesArray = new JArray();
                if (JSONHelper.isTokenPropertyNull(foldersPayloadObject, "entities") == false)
                {
                    entitiesArray = (JArray)foldersPayloadObject["entities"];
                }
                JObject foldersObject = new JObject();
                if (JSONHelper.isTokenPropertyNull(foldersPayloadObject["models"], "folders") == false)
                {
                    foldersObject = (JObject)foldersPayloadObject["models"]["folders"];
                }
                JObject usersObject = new JObject();
                if (JSONHelper.isTokenPropertyNull(foldersPayloadObject["models"], "users") == false)
                {
                    usersObject = (JObject)foldersPayloadObject["models"]["users"];
                }
                logger.Info("Number of Entities={0}", entitiesArray.Count);
                logger.Info("Number of Folders={0}", foldersPayloadObject.Children().Count());
                logger.Info("Number of Users={0}", usersObject.Children().Count());

                foldersList = new List<Folder>(entitiesArray.Count);
                foreach (JObject entityObject in entitiesArray)
                {
                    // Only deal with "query" objects, which are worksheets
                    if (JSONHelper.getStringValueFromJToken(entityObject, "entityType") != "folder") continue;

                    Folder folder = new Folder();
                    folder.AccountName = this.AuthContext.AccountName;
                    folder.AccountFullName = this.AuthContext.AccountFullName;
                    folder.AccountUrl = this.AuthContext.AccountUrl;
                    folder.OrganizationID = this.AuthContext.OrganizationID;
                    folder.Region = this.AuthContext.Region;

                    folder.FolderID = JSONHelper.getStringValueFromJToken(entityObject, "entityId");
                    folder.FolderName = JSONHelper.getStringValueFromJToken(entityObject["info"], "name");
                    
                    JObject entityDetailObject = (JObject)JSONHelper.getJTokenValueFromJToken(foldersObject, folder.FolderID);
                    if (entityDetailObject != null)
                    {
                        folder.OwnerUserID = JSONHelper.getStringValueFromJToken(entityDetailObject, "ownerId");

                        JObject userDetailObject = (JObject)JSONHelper.getJTokenValueFromJToken(usersObject, folder.OwnerUserID);
                        if (userDetailObject != null)
                        {
                            folder.OwnerUserName = JSONHelper.getStringValueFromJToken(userDetailObject, "username");
                        }

                        folder.Version = JSONHelper.getIntValueFromJToken(entityDetailObject, "version");
                        folder.URL = JSONHelper.getStringValueFromJToken(entityDetailObject, "url");

                        DateTime dateTimeValue = DateTime.MinValue;
                        // modified date seems to get parsed into dates
                        folder.ModifiedUtc = JSONHelper.getDateTimeValueFromJToken(entityDetailObject, "modified");
                        if (folder.ModifiedUtc == DateTime.MinValue)
                        {
                            if (DateTime.TryParse(JSONHelper.getStringValueFromJToken(entityDetailObject, "modified"), out dateTimeValue) == true) folder.ModifiedUtc = dateTimeValue.ToUniversalTime();
                        }
                        dateTimeValue = DateTime.MinValue;
                        folder.RefreshedUtc = JSONHelper.getDateTimeValueFromJToken(entityDetailObject, "refreshed");
                        if (folder.RefreshedUtc == DateTime.MinValue)
                        {
                            if (DateTime.TryParse(JSONHelper.getStringValueFromJToken(entityDetailObject, "refreshed"), out dateTimeValue) == true) folder.RefreshedUtc = dateTimeValue.ToUniversalTime();
                        }                        

                        logger.Info(folder);
                        loggerConsole.Trace("Found Folder {0} ({1})", folder.FolderName, folder.FolderID);

                        foldersList.Add(folder);
                    }
                }

                foldersList = foldersList.OrderBy(f => f.FolderName).ToList();

                loggerConsole.Info("Retrieved {0} folders", foldersList.Count);

                WriteObject(foldersList.ToArray());
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
