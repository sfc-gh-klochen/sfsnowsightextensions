
using System;

namespace Snowflake.Powershell
{
    public class AppUserContext : EntityBase
    {
        public string AppServerUrl { get; set; }
        public string AuthTokenSnowsight { get; set; }
        public string AuthTokenMaster { get; set; }
        public string AuthTokenSession { get; set; }
        public string ClientID { get; set; }
        public string CSRFToken { get; set; }
        public string UserID { get; set; }
        public string UserName { get; set; }
        public string ServerVersion { get; set; }

        public override string FileSystemSafeName
        {
            get 
            {
                return String.Format("AppUserContext.{0}.{1}.json", FileIOHelper.GetFileSystemSafeString(this.AccountName), FileIOHelper.GetFileSystemSafeString(this.UserName));
            }
            set
            {
                this._FileSystemSafeName = value;
            }
        }

        public override String ToString()
        {
            return String.Format(
                "AppUserContext: User {0}({1})@{2}({3}), Snowsight at {4} served by {5}",
                this.UserName,
                this.UserID,
                this.AccountFullName,
                this.OrganizationID,
                this.AccountUrl, 
                this.AppServerUrl);
        }        
    }
}