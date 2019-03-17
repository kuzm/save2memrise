using System;

namespace Save2Memrise.Services.Public.API
{
    public class DictionaryItem
    {
        public string Term { get; set; }
        public string Definition { get; set; }

        public override string ToString()
        {
            return $"[{nameof(DictionaryItem)} Term={Term}, Definition={Definition}]";
        }
    }
}