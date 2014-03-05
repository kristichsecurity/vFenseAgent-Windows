using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using Agent.Core;
using Agent.Core.Data.Model;
using Agent.Core.Utils;
using Agent.RV.Utils;


namespace Agent.RV
{
    internal static class Downloader
    {
        public static Operations.SavedOpData DownloadFile(Operations.SavedOpData update, UpdateDirectories updateTypePath)
        {
            var uris = new List<DownloadUri>();
            string updateDirectory;

            switch (updateTypePath)
            {
                case UpdateDirectories.SupportedAppDir:
                    updateDirectory = Settings.SupportedAppDirectory;
                    break;
                case UpdateDirectories.CustomAppDir:
                    updateDirectory = Settings.CustomAppDirectory;
                    break;
                case UpdateDirectories.OSUpdateDir:
                    updateDirectory = Settings.UpdateDirectory;
                    break;
                default:
                    updateDirectory = Settings.UpdateDirectory;
                    break;
            }

            var savedir = Path.Combine(updateDirectory, update.filedata_app_id);

            foreach (var uriData in update.filedata_app_uris)
            {
                var tempDownloadUri = new DownloadUri();
                tempDownloadUri.FileName = uriData.file_name;
                tempDownloadUri.FileSize = uriData.file_size;
                tempDownloadUri.Hash = uriData.file_hash;

                //add uri link from WSUS if its enabled
                if (WSUS.IsWSUSEnabled())
                {
                    var tempAvilApp = Agent.RV.WindowsApps.WindowsUpdates.GetAvailableUpdates();
                    foreach (var application in tempAvilApp)
                    {
                        if (application.Name == update.filedata_app_name)
                        {
                            foreach (var availUri in application.FileData)
                            {
                                tempDownloadUri.Uris.Add(availUri.Uri);
                            }
                        }
                    }
                }

                foreach (var uri in uriData.file_uris)
                    tempDownloadUri.Uris.Add(uri);
                
                uris.Add(tempDownloadUri);
            }

            if (Directory.Exists(savedir))
                Directory.Delete(savedir, true);
            Directory.CreateDirectory(savedir);

            foreach (var file in uris)
            {
                // Just in case the web server is using a self-signed cert. 
                // Webclient won't validate the SSL/TLS cerficate if it's not trusted.
                var tempCallback = ServicePointManager.ServerCertificateValidationCallback;
                ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };

                var filepath = Path.Combine(savedir, file.FileName);

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
                                    var localFileHashMd5 = RvUtils.Md5HashFile(filepath).ToLower();
                                    var localFileHashSha1 = RvUtils.Sha1HashFile(filepath).ToLower();
                                    var localFileHashSha256 = RvUtils.Sha256HashFile(filepath).ToLower();

                                    Logger.Log("Download Complete,  {0}", LogLevel.Info, file.FileName);
                                    Logger.Log("Checking Hashes...");
                                    Logger.Log("Incoming Hash: {0}", LogLevel.Info, file.Hash);
                                    Logger.Log("Local MD5 Hash: {0}", LogLevel.Info, localFileHashMd5);
                                    Logger.Log("Local SHA1 Hash: {0}", LogLevel.Info, localFileHashSha1);
                                    Logger.Log("Local SHA256 Hash: {0}", LogLevel.Info, localFileHashSha256);
                                    downloaded = true;

                                    if (localFileHashMd5 != file.Hash.ToLower() && localFileHashSha1 != file.Hash.ToLower() && localFileHashSha256 != file.Hash.ToLower())
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
                            update.error = String.Empty;
                            update.success = true.ToString().ToLower();
                        }
                    }
                }
                catch (Exception e)
                {
                    //Critical exception occurred.
                    update.error = "Application did not download, Exception occured, refer to log for details.";
                    update.success = false.ToString().ToLower();
                    Logger.Log("One or more Application Files were not downloaded successfully; {0}.",
                    LogLevel.Error, file.FileName);
                    Logger.LogException(e);
                    return update;
                }

                ServicePointManager.ServerCertificateValidationCallback = tempCallback;
            }

            return update;
        }

        public enum UpdateDirectories
        {
            SupportedAppDir,
            CustomAppDir,
            OSUpdateDir
        }
    }
}
