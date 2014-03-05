using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Agent.RV.ThirdParty
{
    public static class SupportedApplications
    {
        static IList<string> supportedApps = new List<string>();

        public static string Match(string appName)
        {
            bool match = JavaJREMatch(appName);
            if (match)
            {
                return "java";
            }

            return String.Empty;
        }

        private static bool JavaJREMatch(string appName)
        {
            Match match = Regex.Match(appName, RegExPattern.Java, RegexOptions.IgnoreCase);
            if (match.Success)
                return true;

            return false;
        }
    }
}
