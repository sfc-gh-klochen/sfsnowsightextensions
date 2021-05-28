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
        "SFWorksheet",
        DefaultParameterSetName="WorksheetFile",
        SupportsPaging=false,
        SupportsShouldProcess=false)]
    [OutputType(typeof(String))]
    public class NewWorksheetCommand : PSCmdlet
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
            HelpMessage = "Worksheet object of Worksheet to create or update",
            ParameterSetName = "WorksheetObject")]
        public Worksheet Worksheet { get; set; }

        [Parameter(
            Mandatory = true,
            Position = 1,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true,
            HelpMessage = "File representation of Worksheet to create or update",
            ParameterSetName = "WorksheetFile")]
        public string WorksheetFile { get; set; }

        [Parameter(
            Mandatory = false,
            Position = 2,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true,
            HelpMessage = "What to do when the Worksheet already exists")]
        [ValidateSet ("Overwrite", "CreateNew", "Skip")]
        public string ActionIfExists { get; set; } = "Skip";

        [Parameter(
            Mandatory = false,
            Position = 3,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true,
            HelpMessage = "Should the Worksheet be executed")]
        public SwitchParameter Execute { get; set; }

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

                    default:
                        throw new ArgumentException(String.Format("Unknown parameter set {0}", this.ParameterSetName));
                }

                // Get all worksheets already present
                string worksheetsApiResult = SnowflakeDriver.GetWorksheets(this.AuthContext.AppServerUrl, this.AuthContext.AccountUrl, this.AuthContext.OrganizationID, this.AuthContext.UserName, this.AuthContext.AuthTokenSnowsight);
                if (worksheetsApiResult.Length == 0)
                {
                    throw new ItemNotFoundException("Invalid response from listing worksheet entities");
                }

                Worksheet targetWorksheetToReplace = null;

                // See if worksheet we want to create already exists
                // First, try to find it by the worksheet ID
                // Would probably only work for the worksheets going from same deployment and user back into the same deployment
                JObject worksheetsPayloadObject = JObject.Parse(worksheetsApiResult);

                JArray entitiesArray = new JArray();
                if (JSONHelper.isTokenPropertyNull(worksheetsPayloadObject, "entities") == false)
                {
                    entitiesArray = (JArray)worksheetsPayloadObject["entities"];
                }
                logger.Info("Number of Entities={0}", entitiesArray.Count);
                
                foreach (JObject entityObject in entitiesArray)
                {
                    // Only deal with "query" objects, which are worksheets
                    if (JSONHelper.getStringValueFromJToken(entityObject, "entityType") != "query") continue;

                    Worksheet potentialTargetWorksheet = new Worksheet(entityObject, worksheetsPayloadObject, this.AuthContext);

                    if (this.Worksheet.WorksheetID == potentialTargetWorksheet.WorksheetID) 
                    {
                        targetWorksheetToReplace = potentialTargetWorksheet;

                        logger.Info("Found Match by ID: {0}=={1}", this.Worksheet, targetWorksheetToReplace);
                        break;
                    }
                }

                // If didn't find it by the entity ID.
                // Second, try to find it by the worksheet name
                if (targetWorksheetToReplace == null)
                {
                    foreach (JObject entityObject in entitiesArray)
                    {
                        // Only deal with "query" objects, which are worksheets
                        if (JSONHelper.getStringValueFromJToken(entityObject, "entityType") != "query") continue;

                        Worksheet potentialTargetWorksheet = new Worksheet(entityObject, worksheetsPayloadObject, this.AuthContext);

                        if (this.Worksheet.WorksheetName == potentialTargetWorksheet.WorksheetName)
                        {
                            // Found first matching worksheet with the same name and folder
                            targetWorksheetToReplace = potentialTargetWorksheet;

                            logger.Info("Found Match by Name: {0}=={1}", this.Worksheet, targetWorksheetToReplace);
                            break;
                        }
                    }
                }

                // At this point, we either have the worksheet to update, or the worksheet we're trying to import is new
                if (targetWorksheetToReplace != null)
                {
                    // Updating existing worksheet
                    switch (this.ActionIfExists)
                    {
                        case "Overwrite":
                            logger.Info("Found {0} to overwrite and ActionIfExists={1}, will overwrite", targetWorksheetToReplace, this.ActionIfExists);
                            loggerConsole.Info("Existing Worksheet {0} ({1}) in Folder {2} will be overwritten because ActionIfExists is {2}", targetWorksheetToReplace.WorksheetName, targetWorksheetToReplace.WorksheetID, targetWorksheetToReplace.FolderName, this.ActionIfExists);
                            
                            break;
                        
                        case "CreateNew":
                            logger.Info("Found {0} to overwrite but ActionIfExists={1}, will instead create new", targetWorksheetToReplace, this.ActionIfExists);
                            loggerConsole.Info("Existing Worksheet {0} ({1}) in Folder {2} will be ignored and new Worksheet will be created because ActionIfExists is {3}", targetWorksheetToReplace.WorksheetName, targetWorksheetToReplace.WorksheetID, targetWorksheetToReplace.FolderName, this.ActionIfExists);

                            targetWorksheetToReplace = null;
                        
                            break;

                        case "Skip":
                            logger.Info("Found {0} to overwrite but ActionIfExists={1}, will skip", targetWorksheetToReplace, this.ActionIfExists);
                            loggerConsole.Info("Existing Worksheet {0} ({1}) in Folder {2} will be ignored and nothing will be done because ActionIfExists is {3}", targetWorksheetToReplace.WorksheetName, targetWorksheetToReplace.WorksheetID, targetWorksheetToReplace.FolderName, this.ActionIfExists);
                        
                            return;

                        default:
                            throw new ArgumentException(String.Format("Unknown ActionIfExists parameter {0}", this.ActionIfExists));
                    }
                }
                else
                {
                    logger.Info("No match for {0}, new one will be created", this.Worksheet);
                    loggerConsole.Info("Creating new Worksheet {0}", this.Worksheet.WorksheetName);
}

                Worksheet createdOrUpdatedWorksheet = null;

                // Now actually make modifications
                if (targetWorksheetToReplace != null)
                {
                    // Updating existing worksheet
                    string updateWorksheetApiResult = SnowflakeDriver.UpdateWorksheet(
                        this.AuthContext.AppServerUrl, this.AuthContext.AccountUrl, this.AuthContext.UserName, this.AuthContext.AuthTokenSnowsight, 
                        targetWorksheetToReplace.WorksheetID, this.Worksheet.Query, this.Worksheet.Role, this.Worksheet.Warehouse, this.Worksheet.Database, this.Worksheet.Schema);

                    worksheetsPayloadObject = JObject.Parse(updateWorksheetApiResult);

                    if (updateWorksheetApiResult.Length == 0)
                    {
                        throw new ItemNotFoundException("Invalid response from updating existing worksheet");
                    }

                    JObject updateWorksheetPayloadObject = JObject.Parse(updateWorksheetApiResult);

                    createdOrUpdatedWorksheet = new Worksheet(targetWorksheetToReplace.WorksheetID, updateWorksheetPayloadObject, this.AuthContext);

                    logger.Info("Returning updated {0}", createdOrUpdatedWorksheet);
                }
                else
                {
                    // Creating new worksheet
                    string createWorksheetApiResult = SnowflakeDriver.CreateWorksheet(
                        this.AuthContext.AppServerUrl, this.AuthContext.AccountUrl, this.AuthContext.OrganizationID, this.AuthContext.UserName, this.AuthContext.AuthTokenSnowsight, 
                        this.Worksheet.WorksheetName);

                    if (createWorksheetApiResult.Length == 0)
                    {
                        throw new ItemNotFoundException("Invalid response from creating new worksheet");
                    }

                    JObject createWorksheetPayloadObject = JObject.Parse(createWorksheetApiResult);
                    string newWorksheetID = JSONHelper.getStringValueFromJToken(createWorksheetPayloadObject, "pid");
                    logger.Info("New WorksheetID={0}", newWorksheetID);

                    string updateWorksheetApiResult = SnowflakeDriver.UpdateWorksheet(
                        this.AuthContext.AppServerUrl, this.AuthContext.AccountUrl, this.AuthContext.UserName, this.AuthContext.AuthTokenSnowsight, 
                        newWorksheetID, this.Worksheet.Query, this.Worksheet.Role, this.Worksheet.Warehouse, this.Worksheet.Database, this.Worksheet.Schema);

                    if (updateWorksheetApiResult.Length == 0)
                    {
                        throw new ItemNotFoundException("Invalid response from updating existing worksheet");
                    }

                    JObject updateWorksheetPayloadObject = JObject.Parse(updateWorksheetApiResult);

                    createdOrUpdatedWorksheet = new Worksheet(newWorksheetID, updateWorksheetPayloadObject, this.AuthContext);

                    logger.Info("Returning new {0}", createdOrUpdatedWorksheet);
                }

                if (this.Execute.IsPresent == true)
                {
                    logger.Info("Running Worksheet {0}", createdOrUpdatedWorksheet);
                    loggerConsole.Trace("Running Worksheet {0} ({1})", createdOrUpdatedWorksheet.WorksheetName, createdOrUpdatedWorksheet.WorksheetID);

                    string executeWorksheetApiResult = SnowflakeDriver.ExecuteWorksheet(
                        this.AuthContext.AppServerUrl, this.AuthContext.AccountUrl, this.AuthContext.UserName, this.AuthContext.AuthTokenSnowsight, 
                        createdOrUpdatedWorksheet.WorksheetID, createdOrUpdatedWorksheet.Query, this.Worksheet.Parameters.ToString(Newtonsoft.Json.Formatting.None),
                        createdOrUpdatedWorksheet.Role, createdOrUpdatedWorksheet.Warehouse, createdOrUpdatedWorksheet.Database, createdOrUpdatedWorksheet.Schema);
                }

                loggerConsole.Info("Returning Worksheet {0} ({1})", createdOrUpdatedWorksheet.WorksheetName, createdOrUpdatedWorksheet.WorksheetID);

                WriteObject(createdOrUpdatedWorksheet);
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
