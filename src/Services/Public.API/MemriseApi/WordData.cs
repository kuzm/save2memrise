using Newtonsoft.Json;

namespace Save2Memrise.Services.Public.API.MemriseApi
{
    public class TermDefinition
    {
        public string ThingId { get; }
        public string Term { get; }
        public string Definition { get; }

        public TermDefinition(string thingId, string term, string definition)
        {
            ThingId = thingId;
            Term = term;
            Definition = definition;
        }
    }
}