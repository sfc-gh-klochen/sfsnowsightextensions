using System;

namespace Snowflake.Powershell
{
    public class Folder : EntityBase
    {
        // Folder metadata
        public string FolderID { get; set; }
        public string FolderName { get; set; }
        public string OwnerUserID { get; set; }
        public string OwnerUserName { get; set; }
        public int Version { get; set; }
        public string URL { get; set; }

        // Folder Dates
        public DateTime RefreshedUtc { get; set; }
        public DateTime ModifiedUtc { get; set; }

        // File name
        public override string FileSystemSafeName
        {
            get 
            {
                return String.Format("Folder.{0}.{1}.{2}.json", FileIOHelper.GetFileSystemSafeString(this.AccountName), FileIOHelper.GetShortenedEntityNameForFileSystem(this.FolderName, 30), FileIOHelper.GetFileSystemSafeString(this.FolderID));
            }
            set
            {
                this._FileSystemSafeName = value;
            }
        }

        public override String ToString()
        {
            return String.Format(
                "Folder: {0} ({1}) in {2} owned by {3}({4})",
                this.FolderName,
                this.FolderID,
                this.AccountFullName,
                this.OwnerUserName,
                this.OwnerUserID);
        }        
    }
}