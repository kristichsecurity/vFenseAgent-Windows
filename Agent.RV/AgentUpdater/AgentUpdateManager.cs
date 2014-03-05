using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using Agent.Core;
using Agent.Core.Data.Model;
using Agent.Core.Utils;
using Agent.RV.Utils;

namespace Agent.RV.AgentUpdater
{
    public static class AgentUpdateManager
    {
        //Backup Directory = C:\ProgramData\TopPatchBackup\
        public static readonly string AgentUpdateDirectory = Path.Combine(Settings.TempDirectory, "RVAgentUpdate");

        public static RVsofResult AddAppDetailsToResults(RVsofResult results)
        {
            results.Data.Name           = String.Empty;
            results.Data.Description    = String.Empty;
            results.Data.Kb             = String.Empty;
            results.Data.ReleaseDate    = 0.0;
            results.Data.VendorSeverity = String.Empty;
            results.Data.VendorName     = String.Empty;
            results.Data.VendorId       = String.Empty;
            results.Data.Version        = String.Empty;
            results.Data.SupportUrl     = String.Empty;
            return results;
        }

        public static Operations.SavedOpData DownloadUpdate(Operations.SavedOpData update)
        {
            var uris = new List<DownloadUri>();

            foreach (var uriData in update.filedata_app_uris)
            {
                var tempDownloadUri = new DownloadUri();
                tempDownloadUri.FileName = uriData.file_name;
                tempDownloadUri.FileSize = uriData.file_size;
                tempDownloadUri.Hash = uriData.file_hash;

                foreach (var uri in uriData.file_uris)
                        tempDownloadUri.Uris.Add(uri);

                uris.Add(tempDownloadUri);
            }

            try
            {
                if (Directory.Exists(AgentUpdateDirectory))
                    Directory.Delete(AgentUpdateDirectory, true);
                Directory.CreateDirectory(AgentUpdateDirectory);
            }catch{}


            foreach (var file in uris)
            {
                // Just in case the web server is using a self-signed cert. 
                // Webclient won't validate the SSL/TLS cerficate if it's not trusted.
                var tempCallback = ServicePointManager.ServerCertificateValidationCallback;
                ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };

                var filepath = Path.Combine(AgentUpdateDirectory, file.FileName);

                try
                {
                    using (var client = new WebClient())
                    {
                        if (Settings.Proxy != null)
                            client.Proxy = Settings.Proxy;

                        var downloaded = false;
                        foreach (var uriSingle in file.Uris)
                        {
                            try
                            {
                                var splitted = uriSingle.Split(new[] { "//" }, StringSplitOptions.RemoveEmptyEntries);
                                var splitted2 = splitted[1].Split(new[] { '/' });
                                var relayserver = splitted2[0];

                                if (downloaded) break;

                                Logger.Log("Attempting to download {1} from {0} with file size of {2}.", LogLevel.Info, relayserver, file.FileName, file.FileSize);
                                client.DownloadFile(uriSingle, filepath);

                                if (File.Exists(filepath))
                                {
                                    var localFileHash = RvUtils.Md5HashFile(filepath).ToLower();
                                    Logger.Log("Download Complete,  {0}", LogLevel.Info, file.FileName);
                                    Logger.Log("Checking MD5 Hash...");
                                    Logger.Log("Incoming Hash: {0}", LogLevel.Info, file.Hash);
                                    Logger.Log("Local Hash: {0}", LogLevel.Info, localFileHash);
                                    downloaded = true;

                                    if (localFileHash != file.Hash.ToLower())
                                    {
                                        Logger.Log("Local file {0} Hash did not match remote's. Retrying with a different server.", LogLevel.Info, file.FileName);
                                        update.error = "Local file Hash did not match remote. Bad file integrity. ";
                                        update.success = false.ToString().ToLower();
                                        downloaded = false;
                                    }
                                }
                                else
                                {
                                    Logger.Log("File {0} did not download. Retrying with a different server.", LogLevel.Info, file.FileName);
                                    update.error = "File did not download successfully, it was not found on disk. Please check download server.";
                                    update.success = false.ToString().ToLower();
                                    downloaded = false;
                                }
                            }
                            catch (Exception e)
                            {
                                Logger.Log("File {0} failed to download correctly... Possible connection issue, Retrying with a different server.", LogLevel.Info, file.FileName);
                                update.error = "File did not download correctly, Exception message: " + e.Message + ". Please check download server connectivity.";
                                update.success = false.ToString().ToLower();
                                downloaded = false;
                            }
                        }

                        //Check if the file was successfully downloaded and return.
                        if (downloaded)
                        {
                            update.error   = String.Empty;
                            update.success = true.ToString().ToLower();
                        }
                    }
                }
                catch (Exception e)
                {
                    //Critical exception occurred.
                    update.error   = "Agent update did not download successfully, Exception occured, refer to log for details.";
                    update.success = false.ToString().ToLower();
                    Logger.Log("One or more Agent update Files were not downloaded successfully; {0}.",
                    LogLevel.Error, file.FileName);
                    Logger.LogException(e);
                    return update;
                }

                ServicePointManager.ServerCertificateValidationCallback = tempCallback;
            }

            return update;
        }
    }


}
