using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Reflection;

namespace Snowflake.Powershell
{
    public class EntityBase
    {
        // Version metadata
        public string _CreatedWith { get; set; } = "Snowflake Snowsight Extensions";
        public string _CreatedVersion { get; set; } = Assembly.GetExecutingAssembly().GetName().Version.ToString();

        // Account level metadata
        public string AccountName { get; set; }
        public string AccountFullName { get; set; }
        public string AccountUrl { get; set; }
        public string OrganizationID { get; set; }
        public string Region { get; set; }

        internal string _FileSystemSafeName;
        public virtual string FileSystemSafeName { get; set;}

        public void SaveToFile(string fileName)
        {
            string fullPath = Path.GetFullPath(fileName);
            FileIOHelper.WriteObjectToFile(this, fullPath);
        }

        public void SaveToFolder(string folderName)
        {
            this.SaveToFile(Path.Combine(folderName, this.FileSystemSafeName));
        }
   }
}