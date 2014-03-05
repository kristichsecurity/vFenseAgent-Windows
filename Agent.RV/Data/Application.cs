using System;
using System.Collections.Generic;
using Agent.Core.Data;
using Agent.Core.Data.Model;

namespace Agent.RV.Data
{
    public class Application
    {
        public string Name                  = String.Empty;
        public string Version               = String.Empty;
        public string Description           = String.Empty;
        public List<DownloadUri> FileData   = new List<DownloadUri>();
        public string SupportUrl            = String.Empty;
        public string VendorSeverity        = String.Empty;
        public string VendorName            = String.Empty;
        public string VendorId              = String.Empty;
        public string KB                    = String.Empty; //Windows Update specific KB#
        public double InstallDate;
        public double ReleaseDate;
        public string Status                = String.Empty; //Installed, Available
        public string RebootRequired        = "no";
    }

    public class Dependencies
    {
        public string Name;
        public string Version;
        public string appId;
    }

    public class InstallUpdateData
    {
        public string Id = String.Empty;
        public string Name = String.Empty;
        public readonly List<DownloadUri> Uris = new List<DownloadUri>();
    }

    public class InstallCustomData //TODO: THIS IS OLD NOW - CLEAN UP
    {
        public string Id = String.Empty;
        public string Name = String.Empty;
        public List<DownloadUri> Uris = new List<DownloadUri>();
        public string CliOptions = String.Empty;
    }

    public class InstallSupportedData //TODO: THIS IS OLD NOW - CLEAN UP
    {
        public string OperationId = String.Empty;
        public string AppId = String.Empty;
        public string Name = String.Empty;
        public string RebootRequired = String.Empty;
        public string Error = String.Empty;
        public string Success = String.Empty;
        public List<String> Data = new List<string>(); 
        public List<DownloadUri> Uris = new List<DownloadUri>();
        public string CliOptions = String.Empty;
    }

    public class InstallAgentUpdatedData
    {
        public string Id = String.Empty;
        public string Name = String.Empty;
        public List<DownloadUri> Uris = new List<DownloadUri>();
        public string CliOptions = String.Empty;
    }

    public class UninstallData
    {
        public string Name = String.Empty;
        public string Id = String.Empty;
        public bool ThirdParty = false;
        public bool CustomThirdParty = false;
        public string CliOptions = String.Empty;
    }

}