using System;
using System.Collections.Generic;

namespace Agent.Core.Data.Model
{
    public class DownloadUri
    {
        public string Uri = string.Empty;
        public List<string> Uris = new List<string>(); 
        public string Hash = String.Empty;
        public int FileSize;
        public string FileName = String.Empty;
    }
}