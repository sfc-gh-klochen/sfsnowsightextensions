namespace Snowflake.Powershell
{
    public class Config
    {
        public bool DebugEnabled {get;set;}
        public LocalPath LocalPath {get;set;}
    }

    public class LocalPath
    {
       public string Worksheet {get;set;}
       public string Dashboard {get;set;}
       public string Filter {get;set;}
       public string Folder {get;set;}
       public string QueryProfile {get;set;}
    }
}