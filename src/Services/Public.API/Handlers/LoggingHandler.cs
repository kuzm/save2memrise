using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Save2Memrise.Services.Public.API.Extensions;
using Serilog;
using Serilog.Context;

namespace Save2Memrise.Services.Public.API.Handlers
{
    public class LoggingHandler : DelegatingHandler
    {
        private readonly ILogger _logger;
        public LoggingHandler(ILogger logger, HttpMessageHandler innerHandler)
            : base(innerHandler)
        {
            _logger = logger.ForContext<LoggingHandler>();
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var watch = new Stopwatch();
            watch.Start();
            string requestContent = null;
            HttpResponseMessage response = null;
            string responseContent = null;
            try 
            {
                if (request.Content != null)
                {
                    requestContent = await request.Content.ReadAsStringAsync();
                }

                response = await base.SendAsync(request, cancellationToken);
                
                if (response.Content != null)
                {
                    responseContent = await response.Content.ReadAsStringAsync();
                }
                
                return response;
            } 
            catch (Exception ex)
            {
                var requestUri = request.RequestUri;
                var httpMethod = request.Method;
                var elapsed = (int) watch.Elapsed.TotalMilliseconds;
                using (LogContext.PushProperty("RequestContent", requestContent?.Truncate(256)))
                using (LogContext.PushProperty("Exception", ex.ToString()))
                {
                    _logger.Error("Request {HttpMethod} {RequestUri} failed in {Elapsed} ms: {Reason}",
                        httpMethod, requestUri, elapsed, ex.Message);
                }

                throw;
            }
            finally
            {
                var requestUri = request.RequestUri;
                var httpMethod = request.Method;
                var statusCode = $"{(int)response.StatusCode} {response.StatusCode}";
                var elapsed = (int) watch.Elapsed.TotalMilliseconds;
                using (LogContext.PushProperty("RequestContent", requestContent?.Truncate(256)))
                using (LogContext.PushProperty("ResponseContent", responseContent?.Truncate(256)))
                {
                    _logger.Information("Request {HttpMethod} {RequestUri} responded {StatusCode} in {Elapsed} ms",
                        httpMethod, requestUri, statusCode, elapsed);
                }
            }
        }
    }
}