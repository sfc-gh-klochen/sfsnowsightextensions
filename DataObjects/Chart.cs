using System;
using Newtonsoft.Json.Linq;

namespace Snowflake.Powershell
{
    public class Chart : EntityBase
    {
        // Chart metadata
        public int Version { get; set; }
        public string URL { get; set; }
        public string WorksheetID { get; set; }
        public string WorksheetName { get; set; }
        public string ChartID { get; set; }
        public string ChartName { get; set; }

        // Chart Dates
        public DateTime ModifiedUtc { get; set; }

        // Configuration
        public JObject Configuration { get; set; }

        // File name
        public override string FileSystemSafeName 
        { 
            get
            {
                return String.Format("Chart.{0}.{1}.{2}.{3}.{4}.json", FileIOHelper.GetFileSystemSafeString(this.AccountName), FileIOHelper.GetShortenedEntityNameForFileSystem(this.WorksheetName, 30), FileIOHelper.GetFileSystemSafeString(this.WorksheetID), FileIOHelper.GetFileSystemSafeString(this.ChartName), FileIOHelper.GetFileSystemSafeString(this.ChartID));
            }
            set
            {
                this._FileSystemSafeName = value;
            }
        }

        public Chart()
        {}

        public Chart (Worksheet worksheet, JObject chartInfoPayloadObject, AppUserContext authContext)
        {
            this.AccountName = authContext.AccountName;
            this.AccountFullName = authContext.AccountFullName;
            this.AccountUrl = authContext.AccountUrl;
            this.OrganizationID = authContext.OrganizationID;
            this.Region = authContext.Region;

            this.WorksheetID = worksheet.WorksheetID;
            this.WorksheetName = worksheet.WorksheetName;

            this.ChartID = JSONHelper.getStringValueFromJToken(chartInfoPayloadObject, "id"); 
            this.ChartName = JSONHelper.getStringValueFromJToken(chartInfoPayloadObject, "path"); 
            
            this.Version = JSONHelper.getIntValueFromJToken(chartInfoPayloadObject, "version");
        }

        public void AddConfigurationDetails(JObject chartsPayloadObject)
        {
            JObject chartsObject = new JObject();
            if (JSONHelper.isTokenPropertyNull(chartsPayloadObject["models"], "charts2") == false)
            {
                chartsObject = (JObject)chartsPayloadObject["models"]["charts2"];
            }

            JObject entityDetailObject = (JObject)JSONHelper.getJTokenValueFromJToken(chartsObject, String.Format("{0}-{1}", this.WorksheetID, this.ChartID));
            if (entityDetailObject != null)
            {
                DateTime dateTimeValue = DateTime.MinValue;
                // modified date seems to get parsed into dates
                this.ModifiedUtc = JSONHelper.getDateTimeValueFromJToken(entityDetailObject, "modified");
                if (this.ModifiedUtc == DateTime.MinValue)
                {
                    if (DateTime.TryParse(JSONHelper.getStringValueFromJToken(entityDetailObject, "modified"), out dateTimeValue) == true) this.ModifiedUtc = dateTimeValue.ToUniversalTime();
                }
                else
                {
                    this.ModifiedUtc = this.ModifiedUtc.ToUniversalTime();
                }
                
                this.Configuration = entityDetailObject;
            }
        }

        public override String ToString()
        {
            return String.Format(
                "Chart: {0} ({1}) in Worksheet {2} ({3}) in account {4}",
                this.ChartName,
                this.ChartID,
                this.WorksheetName,
                this.WorksheetID,
                this.AccountFullName);
        }        
    }
}