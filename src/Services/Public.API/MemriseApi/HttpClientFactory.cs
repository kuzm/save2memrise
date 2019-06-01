using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using Save2Memrise.Services.Public.API.Handlers;
using Serilog;

namespace Save2Memrise.Services.Public.API.MemriseApi
{
    public class HttpClientFactory
    {
        public static HttpClient Create(ILogger logger, HttpClientHandler httpHandler, CookieContainer cookieContainer)
        {
            var baseAddress = WebsiteClient.DecksAddress;
            httpHandler.CookieContainer = cookieContainer;
            httpHandler.AllowAutoRedirect = true;

            //TODO This is not effective as it can lead to socket exhaustion. 
            // However, we need to set different cookies per each user. 
            var httpClient = new HttpClient(new LoggingHandler(logger, httpHandler))
            {
                BaseAddress = baseAddress
            };

            return httpClient;
        }
    }
}