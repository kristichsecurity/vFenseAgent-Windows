using System;
using System.Security.AccessControl;
using System.Timers;
using System.Net;
using Agent.Core.ServerOperations;
using Newtonsoft.Json.Linq;
using Agent.Core.Utils;
using RestSharp;

namespace Agent.Core.Net 
{
    public static class NetworkManager
    {
        private const string Prefix = "https://";
        private static Timer _timer;
        public delegate void IncomingOperationHandler(string data);
        public static event IncomingOperationHandler OnIncomingOperation;
        private static RestClient _client;
        private static RestRequest _request;
        private static string _user = string.Empty;
        private static string _pass = string.Empty;

        public static void Initialize(string address, int secondsToCheckin = 60000)
        {
            //DISABLE THIS WHEN SSL IS WORKING ON SERVER.
            ServicePointManager.ServerCertificateValidationCallback =
                delegate { return true; };

            var serverAddress = Prefix + address;
            _user = Settings.User;
            _pass = Settings.Pass;

            _client = new RestClient(serverAddress) {CookieContainer = new CookieContainer(), Proxy = Settings.Proxy};

            _timer = new Timer(secondsToCheckin);
            _timer.Elapsed += AgentCheckin;
        }

        /// <summary>
        /// Prepares and sends the JSON-formatted message to the server to "check-in".
        /// </summary>
        /// <param name="sender">The source of the timer event.</param>
        /// <param name="e">An ElapsedEventArgs object that contains the event data.</param>
        private static void AgentCheckin(object sender, ElapsedEventArgs e)
        {
            try
            {
                if (String.IsNullOrEmpty(Settings.AgentId))
                {
                    return;
                }

                var json = new JObject();
                json[OperationKey.Operation] = OperationValue.CheckIn;
                json[OperationKey.OperationId] = Settings.EmptyValue;
                json[OperationKey.AgentId] = Settings.AgentId;

                SendMessage(json.ToString(), ApiCalls.CoreCheckIn());
            }
            catch (Exception ex)
            {
                Logger.Log("Error when attempting to checking with RV Server: {0}", LogLevel.Warning, ex.Message);
            }
        }

        /// <summary>
        /// Starts the timer to have the agent checking in with the server every minute..
        /// This will also allow the agent to retrieve any operations in queue from the server
        /// that it needs to process.
        /// </summary>
        public static void Start()
        {   
            _timer.Enabled = true;
        }

        public static void StartLogin()
        {
            AttemptLogin();
        }

        /// <summary>
        /// Will authenticate with server and save cookie in container to reuse.
        /// </summary>
        /// <param name="address"></param>
        /// <returns>True if Authenticated; False otherwise.</returns>
        private static bool Login()
        {
            const string apiCall = ApiCalls.Login;
            var split = apiCall.Split(new[] { "|||" }, StringSplitOptions.None);
            var api = split[0];
            
            var json = new JObject();
            json["name"]     = _user;
            json["password"] = _pass;

            _request = new RestRequest() {Resource = api, Method = Method.POST};
            _request.AddParameter("application/json; charset=utf-8", json.ToString(), ParameterType.RequestBody);
            _request.RequestFormat = DataFormat.Json; 

            //Submit request and retrieve response
            var response = _client.Execute(_request);

            //Process response from server
            switch (response.ResponseStatus)
            {
                    case ResponseStatus.Completed:
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        Logger.Log("Successfully Logged in to RV Server");
                        return true;
                    }
                    return false;
                    
                    case ResponseStatus.None:
                        Logger.Log("Received empty response from server...");
                        return false;
                    case ResponseStatus.TimedOut:
                        Logger.Log("Connection timed out...");
                        return false;
                    case ResponseStatus.Error:
                        Logger.Log("Received HTTP Error: {0}", LogLevel.Error, response.StatusCode);
                        return false;
                    case ResponseStatus.Aborted:
                        Logger.Log("Connection was aborted.");
                        return false;

                default:
                    Logger.Log("Server did not respond...");
                    return false;
            }

        }

        /// <summary>
        /// Uses Login method to retry a connection. Calls AtemptReLogin.
        /// </summary>
        /// <returns></returns>
        private static void AttemptLogin()
        {
            _timer.Enabled = false;

            if (AttemptReLogin(60000))
            {
                Logger.Log("Communication Established OK.", LogLevel.Debug);
                _timer.Enabled = true;
            }
        }


        /// <summary>
        /// Takes care of calling Login Method and if it fails it will do it again at set interval
        /// </summary>
        /// <param name="retryTimeout"></param>
        /// <returns></returns>
        private static bool AttemptReLogin(int retryTimeout)
        {
            while (true)
            {
                try
                {
                    if (Login())
                        return true;
                    System.Threading.Thread.Sleep(retryTimeout);
                }
                catch
                {
                    System.Threading.Thread.Sleep(retryTimeout);
                }
            } 
        }


        /// <summary>
        /// Sends Data to the Server.
        /// </summary>
        /// <param name="message">Message to be sent</param>
        /// <param name="apicall"></param>
        /// <returns>True if message was sent successfully; false otherwise.</returns>
        public static string SendMessage(string message, string apicall)
        {
            try
            {
                var split       = apicall.Split(new[] {"|||"}, StringSplitOptions.None);
                var api         = split[0];
                var httpmethod  = split[1];

                //Identify HTTP Method and prepare parameters.
                switch (httpmethod)
                {
                    case HttpMethods.Post:
                        _request = new RestRequest() { Resource = api, Method = Method.POST };
                        _request.AddParameter("application/json; charset=utf-8", message, ParameterType.RequestBody);
                        _request.RequestFormat = DataFormat.Json;
                        break;

                    case HttpMethods.Get:
                        _request = new RestRequest() { Resource = api, Method = Method.GET};
                        break;

                    case HttpMethods.Put:
                        _request = new RestRequest() { Resource = api, Method = Method.PUT };
                        _request.AddParameter("application/json; charset=utf-8", message, ParameterType.RequestBody);
                        _request.RequestFormat = DataFormat.Json;
                        break;

                    default:
                        Logger.Log("Invalid HttpMethod, please check ApiCalls.");
                        break;
                }
              
                //Submit Message and retrieve response
                var response = _client.Execute(_request);
                

                //Process response from server
                switch (response.ResponseStatus)
                {
                    case ResponseStatus.Completed:
                        Logger.Log("Sent message: {0}", LogLevel.Info, message);

                        if (response.StatusCode == HttpStatusCode.OK)
                        {
                            Logger.Log("Message was received by RV Server, status: {0}", LogLevel.Info, response.StatusCode);
                            try
                            {
                                var jsonObject = JObject.Parse(response.Content);
                                var operationType = jsonObject["rv_status_code"].ToString();
                                var data = jsonObject["data"].ToString();

                                switch (operationType)
                                {
                                    case "3001": //New Agent
                                        Logger.Log("New agent operation completed successfully: {0}", LogLevel.Info, response.Content);
                                        OnIncomingOperation(response.Content);
                                        break;

                                    case "3003": //Agent Checkin
                                        if (data == "[]" || String.IsNullOrEmpty(data))
                                        {
                                            Logger.Log("Agent checked in with server OK, Empty server queue.");
                                            return response.StatusCode.ToString();
                                        }

                                        Logger.Log("Agent Checked in with server OK, Retrieved queue for Agent: {0}", LogLevel.Debug, data);
                                        Logger.Log("Preparing to process operation data received from RV Server...");
                                        OnIncomingOperation(response.Content);
                                        break;

                                    case "3005": //Start up
                                        Logger.Log("Startup Agent completed, status: {0}", LogLevel.Info, response.Content);
                                        OnIncomingOperation(response.Content);
                                        break;

                                    default:
                                        Logger.Log("Incoming message from server: {0}", LogLevel.Debug, response.Content);
                                        OnIncomingOperation(response.Content);
                                        return response.StatusCode.ToString();
                                }
                            }
                            catch (Exception e)
                            {
                                Logger.Log("Received exception when parsing status code from server data, {0}", LogLevel.Error, e.Message);
                                Logger.Log("Displaying content received from server:   {0}", LogLevel.Debug, response.Content);
                                return response.StatusCode.ToString();
                            }
                        }
                        return response.StatusCode.ToString();

                    case ResponseStatus.None:
                        Logger.Log("Received empty response from server...");
                        return response.StatusCode.ToString();

                    case ResponseStatus.TimedOut:
                        Logger.Log("Connection timed out...");
                        return response.StatusCode.ToString();

                    case ResponseStatus.Error:
                        Logger.Log("Received HTTP Error: {0}", LogLevel.Error, response.StatusCode);
                        return response.StatusCode.ToString();

                    case ResponseStatus.Aborted:
                        Logger.Log("Connection was aborted.");
                        return response.StatusCode.ToString();

                    default:
                        Logger.Log("Server did not respond...");
                        return response.StatusCode.ToString();
                }  

            }
            catch (Exception e)
            {
                Logger.LogException(e);
                return null;
            }
        }
    }
}