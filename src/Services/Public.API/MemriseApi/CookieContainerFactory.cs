using System;
using System.Collections.Generic;
using System.Net;

namespace Save2Memrise.Services.Public.API.MemriseApi
{
    public class CookieContainerFactory
    {
        public static CookieContainer Create(List<CookieData> cookies)
        {
            var baseAddress = WebsiteClient.DecksAddress;
            var cookieContainer = new CookieContainer();
            foreach (var cookie in cookies)
            {
                if (cookie.Domain.EndsWith("memrise.com") && cookie.Path == "/")
                {
                    var cookieUrl = $"{baseAddress.Scheme}://{baseAddress.Host}{cookie.Path}";
                    cookieContainer.Add(new Uri(cookieUrl), new Cookie(cookie.Name, cookie.Value));
                }
            }

            return cookieContainer;
        }
    }
}