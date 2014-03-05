namespace Agent.RV.ThirdParty
{
    public static class RegExPattern
    {
        public static string Java = @"Java [0-9][0-9]? Update [0-9][0-9]?";
        public static string AdobeReader = @"Adobe Reader";
    }

    public static class RegistryInstallKey
    {
        // Internet Explore
        public static string FlashActiveX64 = @"\HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Adobe Flash Player ActiveX";
        public static string FlashActiveX32 = @"\HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Adobe Flash Player ActiveX";

        // Firefox
        public static string FlashPluginX64 = @"\HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Adobe Flash Player Plugin";
        public static string FlashPluginX32 = @"\HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Adobe Flash Player Plugin";
    }
}
