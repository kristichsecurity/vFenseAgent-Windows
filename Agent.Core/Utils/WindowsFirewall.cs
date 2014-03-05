using System;
using Microsoft.Win32;

namespace Agent.Core.Utils
{
    public static class WindowsFirewall
    {
        //two locations on registry for stadard profile and public profile for the firewall 
        private const string FirewallStd = @"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\StandardProfile";
        private const string FirewallPbc = @"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\PublicProfile";

        //check status on standard profile
        private static bool firewall_statusStd()
        {
            var firewallEnable = false;

            try
            {
                RegistryKey stdFirewall = Registry.LocalMachine.OpenSubKey(FirewallStd);

                if (Convert.ToInt16(stdFirewall.GetValue("EnableFirewall")) != null)
                {
                    if (Convert.ToInt16(stdFirewall.GetValue("EnableFirewall")) == 1)
                        firewallEnable = true;
                }
                stdFirewall.Close();
            }
            catch{}

            return firewallEnable;
        }

        //check status on public profile
        private static bool firewall_statusPbc()
        {
            var firewallEnable = false;

            try
            {
                RegistryKey stdFirewall = Registry.LocalMachine.OpenSubKey(FirewallPbc);

                if (Convert.ToInt16(stdFirewall.GetValue("EnableFirewall")) != null)
                {
                    if (Convert.ToInt16(stdFirewall.GetValue("EnableFirewall")) == 1)
                        firewallEnable = true;
                }
                stdFirewall.Close();
            }
            catch
            {
                firewallEnable = true;
            }

            return firewallEnable;
        }

        //run status for public and standard firewall status
        public static bool IsProtectionEnabled()
        {
            var fwstandard = firewall_statusStd();
            var fwpublic = firewall_statusPbc();

            if (fwstandard == false || fwpublic == false)
                return false;
            return true;
        }
    }
}
