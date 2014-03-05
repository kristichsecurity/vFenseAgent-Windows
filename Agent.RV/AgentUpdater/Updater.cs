using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using Agent.Core.Utils;
using Agent.RV.Data;
using Agent.RV.SupportedApps;
using Agent.RV.ThirdParty;
using Agent.RV.Uninstaller;

namespace Agent.RV.AgentUpdater
{
    public static class Updater
    {
        static private readonly string AgentUpdateDirectory = Path.Combine(Settings.BackupDirectory, "rvagent");
        static private string _fileName = string.Empty;
        static readonly WebClient WebClient = new WebClient();

        public static RvSofOperation DownloadUpdate(RvSofOperation operation)
        {
            var url = operation.InstallAgentUpdateDataList[0].ToString(); 
            var split = url.Split(new[] { '/' });
            var filename = split[split.Length - 1];
            var filepath = Path.Combine(AgentUpdateDirectory, filename);
            int fileSize;

            _fileName = filename;


            if (!Directory.Exists(Settings.BackupDirectory))
                Directory.CreateDirectory(Settings.BackupDirectory);

            if (!Directory.Exists(AgentUpdateDirectory))
                Directory.CreateDirectory(AgentUpdateDirectory);

            if (File.Exists(filepath))
                File.Delete(filepath);

                  try                        
                  {   
                     using (WebClient)
                     {
                        if (Settings.Proxy != null)
                            WebClient.Proxy = Settings.Proxy;

                        WebClient.OpenRead(url);
                        fileSize = Convert.ToInt32(WebClient.ResponseHeaders["Content-Length"]);
                        WebClient.DownloadFile(new Uri(url), filepath);
                     }

                  }
                  catch (Exception e)
                  {
                      Logger.Log("Could not download file {0}. Please check network settings and File URI.", LogLevel.Error, filename);
                      Logger.LogException(e);
                      var data = operation.InstallSupportedDataList[0];
                      var result = new RVsofResult();
                      result.AppId = data.Id;
                      result.Error = "RV Agent Update did not Download, check network and/or Download URI.";
                      operation.AddResult(result);
                      return operation;
                  }


                  if (File.Exists(filepath))
                  {
                      var downloadedAgent = new FileInfo(filepath);
                      var downloadedAgentSize = Convert.ToInt32(downloadedAgent.Length);

                      if (fileSize == downloadedAgentSize)
                      {
                          //Install Operation for the Update
                          var updateResults = InstallOperation(operation);
                          if (updateResults != null)
                              return updateResults;

                          //Installation of RV Agent Update Failed while Installing.
                          InstallSupportedData data = operation.InstallSupportedDataList[0];
                          var result = new RVsofResult();
                          result.AppId = data.Id;
                          result.Error = "RV Agent Update Failed while installing.";
                          result.Success = false.ToString();
                          operation.AddResult(result);
                          return operation;
                      }
                      else
                      {
                          //Downloaded failed, send back results
                          Logger.Log("RV Agent Update File download corrupted, unable to install the update.");
                          InstallSupportedData data = operation.InstallSupportedDataList[0];
                          var result = new RVsofResult();
                          result.AppId = data.Id;
                          result.Error = "File download corrupted, unable to install update.";
                          operation.AddResult(result);
                          return operation;
                      }

                  }
                  else
                  {
                      //Downloaded failed, send back results
                      Logger.Log("RV Agent Update did not Download, check network and/or Download URI.", LogLevel.Info);
                      InstallSupportedData data = operation.InstallSupportedDataList[0];
                      var result = new RVsofResult();
                      result.AppId = data.Id;
                      result.Error = "RV Agent Update did not Download, check network and/or Download URI.";
                      operation.AddResult(result);
                      return operation;

                  }
        }

        private static RvSofOperation InstallOperation(RvSofOperation windowsOperation)
        {
            InstallSupportedData data = windowsOperation.InstallSupportedDataList[0];
            //string uri = data.Uris; //TODO: This must be corrected for agent updater to work..
            string filepath = Path.Combine(AgentUpdateDirectory, _fileName);

            try
            {
                 InstallResult installResult = ExeInstall(filepath, data.CliOptions);

                 if (!installResult.Success)
                 {
                     var results = new RVsofResult();
                     results.Success = false.ToString();
                     results.AppId = data.Id;
                     results.Error = String.Format("Failed while installing RV Agent update: {0}. {1}. Exit code: {2}.",
                                                                filepath, installResult.ExitCodeMessage, installResult.ExitCode);
                     windowsOperation.AddResult(results);
                     return windowsOperation;
                 }

                 return null;
            }
            catch (Exception e)
            {
                    Logger.Log("Could not install RV Agent Update: ", LogLevel.Error);
                    Logger.LogException(e);

                    var result = new RVsofResult();
                    result.AppId = data.Id;
                    result.Error = String.Format("Failed to update RV Agent:  {0}.", e.Message);
                    windowsOperation.AddResult(result);
                    return windowsOperation;
            }
        }

        private static InstallResult ExeInstall(string exePath, string cliOptions)
        {
            var processInfo = new ProcessStartInfo();
            processInfo.FileName = exePath;
            processInfo.Arguments = cliOptions;
            processInfo.UseShellExecute = false;
            processInfo.CreateNoWindow = true;
            processInfo.RedirectStandardOutput = true;

            InstallResult installResult = RunProcess(processInfo);

            return installResult;
        }

        private static InstallResult RunProcess(ProcessStartInfo processInfo)
        {
            var result = new InstallResult();

            // The following WindowsUninstaller.WindowsExitCode used below might be Windows specific. 
            // Third party apps might not use same code. Good luck!
            try
            {
                using (Process process = Process.Start(processInfo))
                {
                    process.WaitForExit();

                    result.ExitCode = process.ExitCode;
                    result.ExitCodeMessage = new Win32Exception(process.ExitCode).Message;

                    switch (result.ExitCode)
                    {
                        case (int)WindowsUninstaller.WindowsExitCode.Restart:
                        case (int)WindowsUninstaller.WindowsExitCode.Reboot:
                            result.Restart = true;
                            result.Success = true;
                            break;
                        case (int)WindowsUninstaller.WindowsExitCode.Sucessful:
                            result.Success = true;
                            break;
                        default:
                            result.Success = false;
                            break;
                    }

                    StreamReader output = process.StandardOutput;
                    result.Output = output.ReadToEnd();
                }
            }
            catch (Exception e)
            {
                Logger.Log("Could not run {0}.", LogLevel.Error, processInfo.FileName);
                Logger.LogException(e);

                result.ExitCode = -1;
                result.ExitCodeMessage = String.Format("Error trying to run {0}.", processInfo.FileName);
                result.Output = String.Empty;
            }

            return result;
        }


    }


}
