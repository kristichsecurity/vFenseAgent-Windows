using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Agent.RV.SupportedApps;

namespace Agent.RV.ThirdParty
{
    public static class SupportedApplications
    {
        static IList<string> supportedApps = new List<string>();

        public static string Match(string appName)
        {
            bool match = JavaJreMatch(appName);
            if (match)
            {
                return "java";
            }

            return String.Empty;
        }

        private static bool JavaJreMatch(string appName)
        {
            var match = Regex.Match(appName, RegExPattern.Java, RegexOptions.IgnoreCase);
            return match.Success;
        }
    }
}
