using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace CustomAction
{
    public class Network
    {
        private HttpWebResponse HttpResponse { get; set; }
        private HttpWebRequest HttpRequest { get; set; }
        private Uri _site;
        private const string LoginAPI = "/rvl/login" + "|||" + "POST";
        private const string Prefix = "https://";

        private object Execute(string url, string data, string httpmethod, IWebProxy proxy = null)
        {
            if (data == null)
                return null;

            _site = new Uri(url);
            HttpRequest = (HttpWebRequest)WebRequest.Create(_site);

            //Use proxy or set to null
            HttpRequest.Proxy = proxy;

            byte[] requestData = Encoding.UTF8.GetBytes(data);

            HttpRequest.Method = httpmethod;

            try
            {
                    HttpRequest.ContentType = "application/json";
                    HttpRequest.ContentLength = requestData.Length;
                    HttpRequest.Accept = "text/plain, application/json";
                    HttpRequest.AllowAutoRedirect = false;
                    HttpRequest.KeepAlive = false;
                    HttpRequest.Timeout = 30000;

                    //WRITE TO SERVER
                    var streamWriter = HttpRequest.GetRequestStream();
                    streamWriter.Write(requestData, 0, requestData.Length);
                    streamWriter.Flush();
                    streamWriter.Close();
            }
            catch
            {
            }

            //GET RESPONSE FROM SERVER (IF ANY)
            return RetrieveResponse();
        }

        private object RetrieveResponse()
        {
            try
            {
                HttpResponse = (HttpWebResponse)HttpRequest.GetResponse();

                using (var streamReader = new StreamReader(HttpResponse.GetResponseStream()))
                    return streamReader.ReadToEnd();
            }
            catch (WebException error)
            {
                if (error.Response != null)
                {
                    HttpResponse = (HttpWebResponse)error.Response;
                    return ReadResponseFromError(error, (int)HttpResponse.StatusCode);
                }
                return null;
            }
        }

        public bool Login(string address, string user, string pass, WebProxy proxy)
        {
            //Separate HTTPMethod from ApiCall
            var split = LoginAPI.Split(new[] { "|||" }, StringSplitOptions.None);
            var api = split[0];
            var loginUri = Prefix + address + api;

            var s = new StringBuilder();
            s.Append("{ \"name\":\"");
            s.Append(user);
            s.Append("\", \"password\": \"");
            s.Append(pass);
            s.Append("\"}");

            var status = Convert.ToString(Execute(loginUri, s.ToString(), "POST", proxy));

            switch (status)
            {
                case "":
                    return true;

                default:
                    return false;
            }
        }

        private static object ReadResponseFromError(WebException error, int httpcode)
        {
            string temp;
            using (var streamReader = new StreamReader(error.Response.GetResponseStream()))
                  temp = streamReader.ReadToEnd();

            if (httpcode > 0)
            {
                switch (httpcode)
                {
                    case 403:
                        return 403;
                    case 400:
                        return 400;
                    case 502:
                        return 502;
                    case 503:
                        return 503;
                    case 504:
                        return 504;

                    default:
                        if (StripHtml(temp) == String.Empty)
                            return httpcode;
                        return httpcode;
                }
            }
            return string.Empty;
        }

        private static string StripHtml(string htmlText)
        {
            var reg = new Regex("<[^>]+>", RegexOptions.IgnoreCase);
            return reg.Replace(htmlText, "");
        }
    }
}
