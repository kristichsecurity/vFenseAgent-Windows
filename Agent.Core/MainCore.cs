using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Agent.Core.ServerOperations;
using Agent.Core.Utils;
using Agent.Core.Net;
using System.Collections;

namespace Agent.Core
{
    public class AgentMain
    {
        private string AppName { get; set; }
        private static OperationManager _operationManager;
        private readonly IList<IAgentPlugin> _foundPlugins;
        private readonly Dictionary<string, IAgentPlugin> _registeredPlugins = new Dictionary<string, IAgentPlugin>();
  
        public AgentMain(string appName)
        {
            AppName = appName;
            Settings.Initialize(DateTime.Now.ToString("MMddyyyy"));
            Tools.InitAgentLogTimer();
            Logger.Log("Loading {0}.", LogLevel.Info, AppName);
            _foundPlugins = LoadPlugins<IAgentPlugin>(Settings.PluginDirectory);
            RegisterPlugins();
        }

        public void Run()
        {
            Logger.Log("Starting up TopPatch Windows Agent {0}",LogLevel.Info, RetrieveVersion());

            //Populate host/ip address of RV Server for use.
            Settings.ResolveServerAddress();

            // Populate Proxy server information from Agent Config File, if any.
            Settings.RetrieveProxySettings();

            _operationManager = new OperationManager(_registeredPlugins);
            NetworkManager.Initialize(Settings.GetServerAddress);
            NetworkManager.OnIncomingOperation += _operationManager.SeverResponseProcessor;
            _operationManager.SendResults += NetworkManager.SendMessage;

            NetworkManager.StartLogin();
            if (!String.IsNullOrEmpty(Settings.AgentId))
            {
                var operation = Tools.PrepareRebootResults();
                _operationManager.ProcessAfterRebootResults(operation);
            }

            foreach (var plugin in _registeredPlugins.Values)
            {
                plugin.Start();
            }

            _operationManager.InitialDataSender();
            NetworkManager.Start();
            
            //Resume Operations in RV Plugin
            _operationManager.ResumeOperations();
            Logger.Log("Ready.");
        }

        private static List<T> LoadPlugins<T>(string folder)
        {
            var files = Directory.GetFiles(folder, "*.dll");
            var tList = new List<T>();

            foreach (var file in files)
            {
                try
                {
                    var assembly = Assembly.LoadFile(file);
                    foreach (var type in assembly.GetTypes())
                    {
                        if (!type.IsClass || type.IsNotPublic) continue;

                        var interfaces = type.GetInterfaces();
                        if (!((IList) interfaces).Contains(typeof (T))) continue;
                        var obj = Activator.CreateInstance(type);
                        var t = (T)obj;
                        tList.Add(t);
                    }
                }
                catch (ReflectionTypeLoadException e)
                {                    
                    Logger.Log("Couldn't load plugins.", LogLevel.Error);
                    Logger.LogException(e);

                    foreach (var ex in e.LoaderExceptions)
                    {
                        Logger.LogException(ex);
                    }
                }
                catch (Exception e)
                {
                    Logger.Log("Couldn't load plugins.", LogLevel.Error);
                    Logger.LogException(e);
                }
            }
            return tList;
        }

        private void RegisterPlugins()
        {
            foreach (var plugin in _foundPlugins)
            {
                _registeredPlugins.Add(plugin.Name, plugin);
            }
        }

        private static string RetrieveVersion()
        {
            string topPatchRegistry;
            const string key = "Version";
            

            //64bit or 32bit Machine?
            if (Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE") == "x86"
                && Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432") == null)
                //32bit
                topPatchRegistry = @"SOFTWARE\TopPatch Inc.\TopPatch Agent";
            else
                //64bit
                topPatchRegistry = @"SOFTWARE\Wow6432Node\TopPatch Inc.\TopPatch Agent";


            //Retrieve the Version number from the TopPatch Agent Registry Key
            try 
            {
                using (var rKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(topPatchRegistry))
                {
                    var installedVersion = ((rKey == null) || (rKey.GetValue(key) == null)) ? String.Empty : rKey.GetValue(key).ToString();
                    return installedVersion;
                }
            } 
            catch (Exception) { return string.Empty; }
        }
    }
}
