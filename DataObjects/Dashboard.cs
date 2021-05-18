using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Snowflake.Powershell
{
    public class Dashboard : EntityBase
    {
        // Dashboard metadata
        public string OwnerUserID { get; set; }
        public string OwnerUserName { get; set; }
        public string URL { get; set; }
        public string DashboardID { get; set; }
        public string DashboardName { get; set; }

        // Dashboard Dates
        public DateTime RefreshedUtc { get; set; }
        public DateTime ViewedUtc { get; set; }
        public DateTime ModifiedUtc { get; set; }

        // Dashboard Context
        public string Role { get; set; }
        public string Warehouse { get; set; }
        public string Database { get; set; }
        public string Schema { get; set; }

        // Dashboard Contents
        public JObject Contents { get; set; }
        public int NumRows { get; set; }
        public int NumWidgets { get; set; }
        public List<Worksheet> Worksheets { get; set; } = new List<Worksheet>();

        // File name
        public override string FileSystemSafeName 
        { 
            get
            {
                return String.Format("Dashboard.{0}.{1}.{2}.json", FileIOHelper.GetFileSystemSafeString(this.AccountName), FileIOHelper.GetShortenedEntityNameForFileSystem(this.DashboardName, 30), FileIOHelper.GetFileSystemSafeString(this.DashboardID));
            }
            set
            {
                this._FileSystemSafeName = value;
            }
        }

        public Dashboard()
        {}

        public Dashboard(JObject entityObject, JObject dashboardsPayloadObject, AppUserContext authContext)
        {
            JObject queriesObject = new JObject();
            if (JSONHelper.isTokenPropertyNull(dashboardsPayloadObject["models"], "queries") == false)
            {
                queriesObject = (JObject)dashboardsPayloadObject["models"]["queries"];
            }
            JObject usersObject = new JObject();
            if (JSONHelper.isTokenPropertyNull(dashboardsPayloadObject["models"], "users") == false)
            {
                usersObject = (JObject)dashboardsPayloadObject["models"]["users"];
            }
            JObject foldersObject = new JObject();
            if (JSONHelper.isTokenPropertyNull(dashboardsPayloadObject["models"], "folders") == false)
            {
                foldersObject = (JObject)dashboardsPayloadObject["models"]["folders"];
            }

            this.AccountName = authContext.AccountName;
            this.AccountFullName = authContext.AccountFullName;
            this.AccountUrl = authContext.AccountUrl;
            this.OrganizationID = authContext.OrganizationID;
            this.Region = authContext.Region;

            this.DashboardID = JSONHelper.getStringValueFromJToken(entityObject, "entityId");

            JObject entityDetailObject = (JObject)JSONHelper.getJTokenValueFromJToken(foldersObject, this.DashboardID);
            if (entityDetailObject != null)
            {
                this.DashboardName = JSONHelper.getStringValueFromJToken(entityDetailObject, "name");

                this.OwnerUserID = JSONHelper.getStringValueFromJToken(entityDetailObject, "ownerId");

                JObject userDetailObject = (JObject)JSONHelper.getJTokenValueFromJToken(usersObject, this.OwnerUserID);
                if (userDetailObject != null)
                {
                    this.OwnerUserName = JSONHelper.getStringValueFromJToken(userDetailObject, "username");
                }

                this.URL = JSONHelper.getStringValueFromJToken(entityDetailObject, "url");

                DateTime dateTimeValue = DateTime.MinValue;
                this.ModifiedUtc = JSONHelper.getDateTimeValueFromJToken(entityDetailObject, "modified");
                if (this.ModifiedUtc == DateTime.MinValue)
                {
                    if (DateTime.TryParse(JSONHelper.getStringValueFromJToken(entityDetailObject, "modified"), out dateTimeValue) == true) this.ModifiedUtc = dateTimeValue.ToUniversalTime();
                }
                else
                {
                    this.ModifiedUtc = this.ModifiedUtc.ToUniversalTime();
                }
                this.RefreshedUtc = JSONHelper.getDateTimeValueFromJToken(entityDetailObject, "refreshed");
                if (this.RefreshedUtc == DateTime.MinValue)
                {
                    if (DateTime.TryParse(JSONHelper.getStringValueFromJToken(entityDetailObject, "refreshed"), out dateTimeValue) == true) this.RefreshedUtc = dateTimeValue.ToUniversalTime();
                }
                else
                {
                    this.RefreshedUtc = this.RefreshedUtc.ToUniversalTime();
                }
                this.ViewedUtc = JSONHelper.getDateTimeValueFromJToken(entityDetailObject, "viewed");
                if (this.ViewedUtc == DateTime.MinValue)
                {
                    if (DateTime.TryParse(JSONHelper.getStringValueFromJToken(entityObject, "viewed"), out dateTimeValue) == true) this.ViewedUtc = dateTimeValue.ToUniversalTime();
                }
                else
                {
                    this.ViewedUtc = this.ViewedUtc.ToUniversalTime();
                }

                this.Role = JSONHelper.getStringValueFromJToken(entityDetailObject["executionContext"], "role");
                this.Warehouse = JSONHelper.getStringValueFromJToken(entityDetailObject["executionContext"], "warehouse");
                this.Database = JSONHelper.getStringValueFromJToken(entityDetailObject["executionContext"], "database");
                this.Schema = JSONHelper.getStringValueFromJToken(entityDetailObject["executionContext"], "schema");

                JObject widgetsObject = new JObject();
                if (JSONHelper.isTokenPropertyNull(entityDetailObject["settings"], "dashboard") == false)
                {
                    widgetsObject = (JObject)entityDetailObject["settings"]["dashboard"];
                }

                this.Contents = widgetsObject;
                
                if (JSONHelper.isTokenPropertyNull(widgetsObject, "rows") == false)
                {
                    JArray rowsArray = (JArray)widgetsObject["rows"];
                    foreach (JObject rowObject in rowsArray)
                    {
                        this.NumRows++;

                        if (JSONHelper.isTokenPropertyNull(rowObject, "cells") == false)
                        {
                            JArray cellsArray = (JArray)rowObject["cells"];
                            foreach (JObject cellObject in cellsArray)
                            {
                                // Only deal with "query" objects, which are worksheets
                                if (JSONHelper.getStringValueFromJToken(cellObject, "type") != "query") continue;

                                string worksheetID = JSONHelper.getStringValueFromJToken(cellObject, "pid");

                                // Only add once
                                // We can see the same Worksheet for both table and Chart
                                if (this.Worksheets.Count(w => w.WorksheetID == worksheetID) == 0) 
                                {
                                    Worksheet worksheet = new Worksheet(worksheetID, dashboardsPayloadObject, authContext);
                                    this.Worksheets.Add(worksheet);
                                }

                                this.NumWidgets++;
                            }
                        }
                    }
                }
            }
        }

        public Dashboard(string dashboardID, JObject dashboardsPayloadObject, AppUserContext authContext)
        {
            JObject queriesObject = new JObject();
            if (JSONHelper.isTokenPropertyNull(dashboardsPayloadObject["models"], "queries") == false)
            {
                queriesObject = (JObject)dashboardsPayloadObject["models"]["queries"];
            }
            JObject usersObject = new JObject();
            if (JSONHelper.isTokenPropertyNull(dashboardsPayloadObject["models"], "users") == false)
            {
                usersObject = (JObject)dashboardsPayloadObject["models"]["users"];
            }
            JObject foldersObject = new JObject();
            if (JSONHelper.isTokenPropertyNull(dashboardsPayloadObject["models"], "folders") == false)
            {
                foldersObject = (JObject)dashboardsPayloadObject["models"]["folders"];
            }

            this.AccountName = authContext.AccountName;
            this.AccountFullName = authContext.AccountFullName;
            this.AccountUrl = authContext.AccountUrl;
            this.OrganizationID = authContext.OrganizationID;
            this.Region = authContext.Region;

            this.DashboardID = dashboardID;

            JObject entityDetailObject = (JObject)JSONHelper.getJTokenValueFromJToken(foldersObject, this.DashboardID);
            if (entityDetailObject != null)
            {
                this.DashboardName = JSONHelper.getStringValueFromJToken(entityDetailObject, "name");

                this.OwnerUserID = JSONHelper.getStringValueFromJToken(entityDetailObject, "ownerId");

                JObject userDetailObject = (JObject)JSONHelper.getJTokenValueFromJToken(usersObject, this.OwnerUserID);
                if (userDetailObject != null)
                {
                    this.OwnerUserName = JSONHelper.getStringValueFromJToken(userDetailObject, "username");
                }

                this.URL = JSONHelper.getStringValueFromJToken(entityDetailObject, "url");

                DateTime dateTimeValue = DateTime.MinValue;
                this.ModifiedUtc = JSONHelper.getDateTimeValueFromJToken(entityDetailObject, "modified");
                if (this.ModifiedUtc == DateTime.MinValue)
                {
                    if (DateTime.TryParse(JSONHelper.getStringValueFromJToken(entityDetailObject, "modified"), out dateTimeValue) == true) this.ModifiedUtc = dateTimeValue.ToUniversalTime();
                }
                else
                {
                    this.ModifiedUtc = this.ModifiedUtc.ToUniversalTime();
                }
                this.RefreshedUtc = JSONHelper.getDateTimeValueFromJToken(entityDetailObject, "refreshed");
                if (this.RefreshedUtc == DateTime.MinValue)
                {
                    if (DateTime.TryParse(JSONHelper.getStringValueFromJToken(entityDetailObject, "refreshed"), out dateTimeValue) == true) this.RefreshedUtc = dateTimeValue.ToUniversalTime();
                }
                else
                {
                    this.RefreshedUtc = this.RefreshedUtc.ToUniversalTime();
                }
                // this.ViewedUtc = JSONHelper.getDateTimeValueFromJToken(entityDetailObject, "viewed");
                // if (this.ViewedUtc == DateTime.MinValue)
                // {
                //     if (DateTime.TryParse(JSONHelper.getStringValueFromJToken(entityObject, "viewed"), out dateTimeValue) == true) this.ViewedUtc = dateTimeValue.ToUniversalTime();
                // }
                // else
                // {
                //     this.ViewedUtc = this.ViewedUtc.ToUniversalTime();
                // }

                this.Role = JSONHelper.getStringValueFromJToken(entityDetailObject["executionContext"], "role");
                this.Warehouse = JSONHelper.getStringValueFromJToken(entityDetailObject["executionContext"], "warehouse");
                this.Database = JSONHelper.getStringValueFromJToken(entityDetailObject["executionContext"], "database");
                this.Schema = JSONHelper.getStringValueFromJToken(entityDetailObject["executionContext"], "schema");

                JObject widgetsObject = new JObject();
                if (JSONHelper.isTokenPropertyNull(entityDetailObject["settings"], "dashboard") == false)
                {
                    widgetsObject = (JObject)entityDetailObject["settings"]["dashboard"];
                }

                this.Contents = widgetsObject;
                
                if (JSONHelper.isTokenPropertyNull(widgetsObject, "rows") == false)
                {
                    JArray rowsArray = (JArray)widgetsObject["rows"];
                    foreach (JObject rowObject in rowsArray)
                    {
                        this.NumRows++;

                        if (JSONHelper.isTokenPropertyNull(rowObject, "cells") == false)
                        {
                            JArray cellsArray = (JArray)rowObject["cells"];
                            foreach (JObject cellObject in cellsArray)
                            {
                                // Only deal with "query" objects, which are worksheets
                                if (JSONHelper.getStringValueFromJToken(cellObject, "type") != "query") continue;

                                if (JSONHelper.getStringValueFromJToken(cellObject, "displayMode") == "table") 
                                {
                                    Worksheet worksheet = new Worksheet(JSONHelper.getStringValueFromJToken(cellObject, "pid"), dashboardsPayloadObject, authContext);
                                    this.Worksheets.Add(worksheet);
                                }
                                else if (JSONHelper.getStringValueFromJToken(cellObject, "displayMode") == "chart")
                                {
                                    // TODO
                                }

                                this.NumWidgets++;
                            }
                        }
                    }
                }
            }
        }

        public override String ToString()
        {
            return String.Format(
                "Dashboard: {0} ({1}) in account {2} is owned by {3} ({4})",
                this.DashboardName,
                this.DashboardID,
                this.AccountFullName,
                this.OwnerUserName,
                this.OwnerUserID);
        }        
    }
}