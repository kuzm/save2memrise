using Newtonsoft.Json;

namespace Save2Memrise.Services.Public.API.MemriseApi
{
    public class TermDefinition
    {
        public string ThingId { get; }
        public string LevelId { get; }
        public string Term { get; }
        public string Definition { get; }

        public TermDefinition(string thingId, string levelId, string term, string definition)
        {
            ThingId = thingId;
            LevelId = levelId;
            Term = term;
            Definition = definition;
        }
    }
}