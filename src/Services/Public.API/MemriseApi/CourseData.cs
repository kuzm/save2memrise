using Newtonsoft.Json;

namespace Save2Memrise.Services.Public.API.MemriseApi
{
    public class CourseData
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("slug")]
        public string Slug { get; set; }

        [JsonProperty("num_levels")]
        public int NumLevels { get; set; }
    }
}