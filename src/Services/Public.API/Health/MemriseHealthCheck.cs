
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using App.Metrics.Health;

namespace Save2Memrise.Services.Public.API
{
    public class MemriseHealthCheck : HealthCheck
    {
        public MemriseHealthCheck()
            : base(name: "Memrise Health Check") { }

        protected async override ValueTask<HealthCheckResult> CheckAsync(
            CancellationToken cancellationToken = default(CancellationToken))
        {
            try 
            {
                var httpClient = new HttpClient();
                var response = await httpClient.GetAsync("https://www.memrise.com");
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return HealthCheckResult.Healthy();
                } 
                
                return HealthCheckResult.Unhealthy("Status code: {0}", response.StatusCode);
            } 
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy(ex);
            }
        }
    }
}