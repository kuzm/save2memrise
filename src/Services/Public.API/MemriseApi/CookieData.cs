using Newtonsoft.Json;

namespace Save2Memrise.Services.Public.API.MemriseApi
{
    public class CookieData
    {
        [JsonProperty("domain")]
        public string Domain { get; set; }

        [JsonProperty("path")]
        public string Path { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("value")]
        public string Value { get; set; }

        public override string ToString()
        {
            return $"[{nameof(CookieData)} " +
                $"Domain={Domain} Path={Path} " +
                $"Name={Name} Value={Value}]";
        }
    }
}