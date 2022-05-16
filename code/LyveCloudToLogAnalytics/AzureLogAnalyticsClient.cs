using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;

namespace LyveCloudToLogAnalytics
{
    public class AzureLogAnalyticsClient : IAzureLogAnalyticsClient
    {
        /* this class is responsible for posting data to Log Analytics. */

        private readonly string _customerId;
        private readonly string _sharedKey;
        private SocketsHttpHandler _httpClientHandler;


        public string TimeStampField { get; set; } = "time";

        public AzureLogAnalyticsClient(string customerId, string sharedKey)
        {
            _httpClientHandler = new SocketsHttpHandler()
            {
                AllowAutoRedirect = true,
                UseProxy = true,
            };
            _customerId = customerId;
            _sharedKey = sharedKey;


        }

        public async Task WriteLog(string logName, string logMessage)
        {

                var datestring = DateTime.UtcNow.ToString("r");
                var jsonBytes = Encoding.UTF8.GetBytes(logMessage);
                string stringToHash = "POST\n" + jsonBytes.Length + "\napplication/json\n" + "x-ms-date:" + datestring + "\n/api/logs";
                string hashedString = BuildSignature(stringToHash, _sharedKey);
                string signature = "SharedKey " + _customerId + ":" + hashedString;

                await PostData(signature, datestring, logName, logMessage);
        }

        // Build the API signature
        public static string BuildSignature(string message, string secret)
        {
            var encoding = new System.Text.ASCIIEncoding();
            byte[] keyByte = Convert.FromBase64String(secret);
            byte[] messageBytes = encoding.GetBytes(message);
            using (var hmacsha256 = new HMACSHA256(keyByte))
            {
                byte[] hash = hmacsha256.ComputeHash(messageBytes);
                return Convert.ToBase64String(hash);
            }


        }

        // Send a request to the POST API endpoint
        public async Task PostData(string signature, string date, string logName, string json)
        {   
            int tryCount = 0;

            string url = "https://" + _customerId + ".ods.opinsights.azure.com/api/logs?api-version=2016-04-01";

            using (HttpClient httpClient = new HttpClient(_httpClientHandler, disposeHandler: false))
            {
                httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
                httpClient.DefaultRequestHeaders.Add("time-generated-field", "time");
                httpClient.DefaultRequestHeaders.Add("Log-Type", logName);
                httpClient.DefaultRequestHeaders.Add("Authorization", signature);
                httpClient.DefaultRequestHeaders.Add("x-ms-date", date);

                System.Net.Http.HttpContent httpContent = new StringContent(json, Encoding.UTF8);
                httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");



                using (HttpResponseMessage response = await httpClient.PostAsync(new Uri(url), httpContent).ConfigureAwait(false))
                {
                    System.Net.Http.HttpContent responseContent = response.Content;
                    string result = responseContent.ReadAsStringAsync().Result;
                }
            }
        }
    }
}

