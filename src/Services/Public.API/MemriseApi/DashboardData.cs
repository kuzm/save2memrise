using System.Collections.Generic;
using Newtonsoft.Json;

namespace Save2Memrise.Services.Public.API.MemriseApi
{
    public class DashboardData
    {
        [JsonProperty("courses")]
        public List<CourseData> Courses { get; set; }
    }
}