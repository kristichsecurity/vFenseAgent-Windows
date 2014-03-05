using Microsoft.Win32;

namespace Agent.RV.Utils
{
    public static class WSUS
    {

        private const string WinUpdate = @"Software\Policies\Microsoft\Windows\WindowsUpdate";
        private const string InternetCom = @"SYSTEM\Internet\Communication Management\Internet Communication";
        private const string AutoUpdate = @"Software\Policies\Microsoft\Windows\WindowsUpdate\AU";


        //WSUS Server URL
        private static string _wsusServer = "";

        public static string GetServerWSUS
        {
            get
            {
                return _wsusServer;
            }
        }

        public static bool IsWSUSEnabled()
        {
            bool WSUSKeyON = false;

            //"Software\Policies\Microsoft\Windows\WindowsUpdate"
            var rkwinupdate = Registry.LocalMachine.OpenSubKey(WinUpdate);
            //Software\Policies\Microsoft\Windows\WindowsUpdate\AU
            var rkWSUS = Registry.LocalMachine.OpenSubKey(AutoUpdate);


            //First check 'HKEY_LOCAL_MACHINE\Software\Policies\Microsoft\Windows\WindowsUpdate\AU'
            //For: UseWUServer - Without this set to 1 (On) the other keys (WUServer and WUStatusServer) will be ignored.
            try
            {
                if (rkWSUS != null)
                {
                    var wuServer = rkWSUS.GetValue("UseWUServer");
                    if (wuServer != null)
                    {
                        var useWuServer = int.Parse(string.Format("{0}", wuServer));
                        rkWSUS.Close();
                        if (useWuServer == 1)
                            WSUSKeyON = true;
                        else
                            WSUSKeyON = false;
                    }
                }
            }
            catch
            {
                WSUSKeyON = false;
            }


            //If the above key is found and set to "1" then proceed.
            try
            {
                if (!WSUSKeyON) return false;
                if (rkwinupdate == null) return false;

                var wuServer = (string)rkwinupdate.GetValue("WUServer");
                var wuStatusServer = (string)rkwinupdate.GetValue("WUStatusServer");

                if (wuServer == wuStatusServer)
                {
                    _wsusServer = wuServer;
                    rkwinupdate.Close();
                    return true;
                }
                rkwinupdate.Close();
                return false;
            }
            catch
            {
                if (rkwinupdate != null) rkwinupdate.Close();
                return false;
            }
        }

        public static bool IsWindowsUpdateAccessDisabled()
        {
            //"Software\Policies\Microsoft\Windows\WindowsUpdate"
            var rkwinupdate = Registry.LocalMachine.OpenSubKey(WinUpdate);

            try
            {
                if (rkwinupdate != null)
                {
                    var winupdate = rkwinupdate.GetValue("DisableWindowsUpdateAccess");
                    if (winupdate != null)
                    {
                        var updateAccess = int.Parse(string.Format("{0}", winupdate));
                        rkwinupdate.Close();
                        return updateAccess != 0;
                    }
                }
            }
            catch
            {
                if (rkwinupdate != null) rkwinupdate.Close();
                return false;
            }
            if (rkwinupdate != null) rkwinupdate.Close();
            return false;
        }

        //If enabled, blocks access to "http://windowsupdate.microsoft.com"
        public static bool IsInternetCommWinUpdateAccessDisabled()
        {
            //"SYSTEM\Internet\Communication Management\Internet Communication"
            var rkInterCom = Registry.LocalMachine.OpenSubKey(InternetCom);

            try
            {
                if (rkInterCom != null)
                {
                    var winupdate = rkInterCom.GetValue("DisableWindowsUpdateAccess");
                    if (winupdate != null)
                    {
                        var updateAccess = int.Parse(string.Format("{0}", winupdate));
                        rkInterCom.Close();

                        if (updateAccess == 0) //0 = Not configured, 
                            return false;

                        if (updateAccess == 1) //1 = update access disabled
                            return true;
                    }
                }
            }
            catch
            {
                if (rkInterCom != null) rkInterCom.Close();
                return false;
            }
            if (rkInterCom != null) rkInterCom.Close();

            return false;
        }

        //Returns the Automatic Update option that was setup.
        public static AutomaticUpdateStatus GetAutomaticUpdatesOptions()
        {
            //Software\Policies\Microsoft\Windows\WindowsUpdate\AU
            var rkautoupdate = Registry.LocalMachine.OpenSubKey(AutoUpdate);

            try
            {
                if (rkautoupdate != null)
                {
                    var autoUpdate = rkautoupdate.GetValue("AUOptions");
                    if (autoUpdate != null)
                    {
                        var auOptions = int.Parse(string.Format("{0}", autoUpdate));
                        rkautoupdate.Close();

                        switch (auOptions)
                        {
                            case 0:
                                return AutomaticUpdateStatus.Error;
                            case 2:
                                return AutomaticUpdateStatus.NotifyBeforeDownload;
                            case 3:
                                return AutomaticUpdateStatus.AutomaticDownloadAndNotifyOfInstall;
                            case 4:
                                return AutomaticUpdateStatus.AutomaticDownloadAndScheduleInstall;
                            case 5:
                                return AutomaticUpdateStatus.AutomaticUpdatesIsRequiredAndUsersCanConfigureIt;

                        }

                    }
                }
            }
            catch
            {
                if (rkautoupdate != null) rkautoupdate.Close();
                return AutomaticUpdateStatus.Error;
            }
            if (rkautoupdate != null) rkautoupdate.Close();
            return AutomaticUpdateStatus.Error;
        }

        public static bool IsAutomaticUpdatesEnabled()
        {
            //Software\Policies\Microsoft\Windows\WindowsUpdate\AU
            var rkautoupdate = Registry.LocalMachine.OpenSubKey(AutoUpdate);

            try
            {
                if (rkautoupdate != null) //check the registry key exsist
                {
                    var autoUpdate = rkautoupdate.GetValue("NoAutoUpdate");
                    if (autoUpdate == null) return false;

                    var noAutoUpdate = int.Parse(string.Format("{0}", autoUpdate));
                    if (noAutoUpdate == 0) //0 - Automatic Updates are Enabled
                        return true;
                    if (noAutoUpdate == 1) //1 - Automatic Updates are disabled
                        return false;
                }
            }
            catch
            {
                if (rkautoupdate != null) rkautoupdate.Close();
                return false;
            }
            finally
            {
                if (rkautoupdate != null) rkautoupdate.Close();
            }

            return false;
        }

        public enum AutomaticUpdateStatus
        {
            Error = 0,
            NotifyBeforeDownload = 2,
            AutomaticDownloadAndNotifyOfInstall = 3,
            AutomaticDownloadAndScheduleInstall = 4,
            AutomaticUpdatesIsRequiredAndUsersCanConfigureIt = 5
        }
    }
}
