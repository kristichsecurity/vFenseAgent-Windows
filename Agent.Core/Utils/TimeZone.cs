using System;

namespace Agent.Core.Utils
{
    public static class GetTimeZone
    {

        public static MyTimeZone GetMyTimeZone()
        {
            var timezone = new MyTimeZone();

            var thisis = System.TimeZone.CurrentTimeZone.GetUtcOffset(DateTime.Now);

            int houroffset = thisis.Hours;
            int minutesoffset = thisis.Minutes;
            int timeoffset = 0;

            timezone.utc_offset = thisis.ToString();
            //set the time for Zones with half hours in it, itll add the minutes to the hours
            //exp: 5:30 = 35, 30 minutes plus 5 <-- hours 
            if (minutesoffset != 0)
                timeoffset = int.Parse(houroffset.ToString()) + int.Parse(minutesoffset.ToString());
            else
                timeoffset = houroffset;
            string timeZone = "";

            switch (timeoffset)
            {
                case (0):
                    timeZone = "Zulu";
                    break;
                case (1):
                    timeZone = "Central European";
                    break;
                case (2):
                    timeZone = "Eastern European";
                    break;
                case (3):
                    timeZone = "Baghdad";
                    break;
                case (33)://3:30
                    timeZone = "Tehran";
                    break;
                case (4):
                    timeZone = "Moscow";
                    break;
                case (34):// 4:30
                    timeZone = "Kabul";
                    break;
                case (5):
                    timeZone = "Karachi";
                    break;
                case (35)://5:30
                    timeZone = "New Delhi";
                    break;
                case (50)://5:45
                    timeZone = "Kathmandu";
                    break;
                case (6):
                    timeZone = "Astana";
                    break;
                case (36)://6:30
                    timeZone = "Yangon";
                    break;
                case (7):
                    timeZone = "Novasibirsk";
                    break;
                case (8):
                    timeZone = "China Coast";
                    break;
                case (9):
                    timeZone = "Japan Standard";
                    break;
                case (39)://9:30
                    timeZone = "Darwin";
                    break;
                case (10):
                    timeZone = "Guam Standard";
                    break;
                case (11):
                    timeZone = "Vladivostok";
                    break;
                case (12):
                    timeZone = "New Zealand Standard";
                    break;
                case (13):
                    timeZone = "Samoa";
                    break;
                case (-1):
                    timeZone = "West Africa";
                    break;
                case (-2):
                    timeZone = "Azores";
                    break;
                case (-3):
                    timeZone = "Greenland";
                    break;
                case (-33)://-3:30
                    timeZone = "Newfoundland";
                    break;
                case (-4):
                    timeZone = "Atlantic Standard";
                    break;
                case (-34)://-4:30
                    timeZone = "Caracas";
                    break;
                case (-5):
                    timeZone = "Eastern Standard Time";
                    break;
                case (-6):
                    timeZone = "Central Standard";
                    break;
                case (-7):
                    timeZone = "Mountain Standard";
                    break;
                case (-8):
                    timeZone = "Pacific Standard";
                    break;
                case (-9):
                    timeZone = "Yukon Standard";
                    break;
                case (-10):
                    timeZone = "Alaska-Hawaii Standard";
                    break;
                case (-11):
                    timeZone = "Coordinated Universal Time-11";
                    break;
                case (-12):
                    timeZone = "International Date Line West";
                    break;
                default:
                    timeZone = "Unable to get Time Zone";
                    break;
            }
            timezone.time_zone = timeZone;

            return timezone;
        }

        public class MyTimeZone
        {
            public string utc_offset = string.Empty;
            public string time_zone = String.Empty;
        }

    }

}


