using System;
using System.Linq;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Save2Memrise.Services.Public.API.Controllers;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using System.IO;
using Newtonsoft.Json.Linq;
using HtmlAgilityPack;
using Serilog;
using OneOf;
using OneOf.Types;

namespace Save2Memrise.Services.Public.API.MemriseApi
{
    public sealed class WebsiteClient
    {
        public static readonly int CourseLimitOnDashboard = 7;
        public static readonly Uri MemriseAddress = new Uri("https://www.memrise.com");
        public static readonly Uri DecksAddress = new Uri("https://decks.memrise.com");
        
        private static readonly IDictionary<int, (string Short, string Long)> _termLangs = new Dictionary<int, (string, string)>()
            {
                {6, ("En", "English")},
                {4, ("De", "German")},
                {10, ("Ru", "Russian")}
            };

        private static readonly IDictionary<int, (string Short, string Long)> _definitionLangs = new Dictionary<int, (string, string)>()
            {
                {6, ("En", "English")},
                {879, ("De", "German")},
                {10, ("Ru", "Russian")}
            };

        private readonly ILogger _logger;
        private readonly HttpClient _httpClient;
        private readonly CookieContainer _cookieContainer;

        public WebsiteClient(ILogger logger, HttpClient httpClient, CookieContainer cookieContainer)
        {
            _logger = logger.ForContext<WebsiteClient>();
            _httpClient = httpClient;
            _cookieContainer = cookieContainer;
        }

        public static (string Short, string Long) GetTermLangLabel(int lang)
            => _termLangs[lang];

        public static (string Short, string Long) GetDefinitionLangLabel(int lang)
            => _definitionLangs[lang];

        public async Task<OneOf<Success, Forbidden, ServerError>> 
            Login(string username, string password)
        {
            var parameters = new List<KeyValuePair<string, string>> 
            {
                new KeyValuePair<string, string>("username", username),
                new KeyValuePair<string, string>("password", password),
                new KeyValuePair<string, string>("next", "")
            };

            var result = await WrapResponse(() => PostResourceAsForm(MemriseAddress, reqPath: "/login/", pagePath: "/login/", parameters: parameters));
            return await result.Match<Task<OneOf<Success, Forbidden, ServerError>>>(
                (HttpResponseMessage message) => 
                {
                    using (message) 
                    {
                        return JoinDecks();
                    }
                },
                (Forbidden error) => Task.FromResult<OneOf<Success, Forbidden, ServerError>>(error),
                (ServerError error) => Task.FromResult<OneOf<Success, Forbidden, ServerError>>(error)
            );
        }

        private async Task<OneOf<Success, Forbidden, ServerError>> JoinDecks()
        {
            var path = $"join/memrise-for-decks/";
            var result = await WrapResponse(() => GetResource(DecksAddress, path));
            return result.Match<OneOf<Success, Forbidden, ServerError>>(
                (HttpResponseMessage message) => 
                {
                    using (message) 
                    {
                        return new Success();
                    }
                },
                (Forbidden error) => error,
                (ServerError error) => error
            );
        }

        public async Task<OneOf<Success, Forbidden, ServerError>> 
            QuitCourse(string courseId)
        {
            string reqPath = "/ajax/courses/quit/";

            var parameters = new List<KeyValuePair<string, string>> 
            {
                new KeyValuePair<string, string>("course_id", courseId)
            };

            var result = await WrapResponse(() => PostResourceAsAjax(DecksAddress, reqPath: reqPath, pagePath: "/home/", parameters: parameters));
            return await result.Match<Task<OneOf<Success, Forbidden, ServerError>>>(
                (HttpResponseMessage message) => 
                {
                    using (message) 
                    {
                        return Task.FromResult<OneOf<Success, Forbidden, ServerError>>(new Success());
                    }
                },
                (Forbidden error) => Task.FromResult<OneOf<Success, Forbidden, ServerError>>(error),
                (ServerError error) => Task.FromResult<OneOf<Success, Forbidden, ServerError>>(error)
            );
        }

        //TODO return only editable courses
        public async Task<OneOf<DashboardData, Forbidden, ServerError>> 
            GetDashboard()
        {
            var mergedDashboard = new DashboardData
            {
                Courses = new List<CourseData>()
            };

            for(var offest = 0; ; offest += MemriseApi.WebsiteClient.CourseLimitOnDashboard)
            {
                var getDashboardResult = await GetDashboard(offset: offest, limit: MemriseApi.WebsiteClient.CourseLimitOnDashboard);
                if (getDashboardResult.TryPickT0(out var dashboard, out var remainder))
                {
                    if (dashboard.Courses.Count == 0)
                    {
                        break;
                    }

                    mergedDashboard.Courses.AddRange(dashboard.Courses);
                }
                else 
                {
                    return remainder.Match<OneOf<DashboardData, Forbidden, ServerError>>(
                        (MemriseApi.Forbidden error) => error,
                        (MemriseApi.ServerError error) => error);
                }
            }

            return mergedDashboard;
        }

        public async Task<OneOf<DashboardData, Forbidden, ServerError>> 
            GetDashboard(int offset, int limit)
        {
            var path = $"ajax/courses/dashboard/?courses_filter=most_recent&offset={offset}&limit={limit}&get_review_count=false";
            var result = await WrapResponse(() => GetResource(DecksAddress, path));
            return await result.Match<Task<OneOf<DashboardData, Forbidden, ServerError>>>(
                async (HttpResponseMessage message) => 
                {
                    using (message) 
                    {
                        return (await ParseResponse<DashboardData>(message))
                            .Match<OneOf<DashboardData, Forbidden, ServerError>>(
                                (DashboardData data) => data,
                                (ServerError error) => error
                            );
                    }
                },
                (Forbidden error) => Task.FromResult<OneOf<DashboardData, Forbidden, ServerError>>(error),
                (ServerError error) => Task.FromResult<OneOf<DashboardData, Forbidden, ServerError>>(error)
            );
        }

        public async Task<OneOf<CourseData, NotFound, Forbidden, ServerError>>
            FindCourse(string courseName, int limit)
        {
            int offset = 0;
            while(true)
            {
                var getDashboardResult = await GetDashboard(offset: offset, limit: CourseLimitOnDashboard);
                if (getDashboardResult.TryPickT0(out DashboardData dashboard, out var remainder))
                {
                    var course = dashboard?.Courses?
                        .FirstOrDefault(c => c.Name == courseName && c.NumLevels == 1);

                    if (course != null)
                    {
                        return course;
                    }
                    else if (course == null && dashboard.Courses.Count < CourseLimitOnDashboard)
                    {
                        return new NotFound();
                    }
                    else 
                    {
                        // Try next page
                        offset += CourseLimitOnDashboard;
                        continue;
                    }
                }
                
                return remainder.Match<OneOf<CourseData, NotFound, Forbidden, ServerError>>(
                    (Forbidden err) => err,
                    (ServerError err) => err
                );
            }
        }

        public async Task<OneOf<IList<TermDefinition>, NotFound, Forbidden, ServerError>> 
            GetTermDefinitions(string courseId, string slug)
        {
            var path = $"course/{courseId}/{slug}/";
            var result = await WrapResponse(() => GetResource(DecksAddress, path));
            return await result.Match<Task<OneOf<IList<TermDefinition>, NotFound, Forbidden, ServerError>>>(
                async (HttpResponseMessage message) => 
                {
                    using (message) 
                    {
                        return (await ParseAsHtml(message))
                            .Match<OneOf<IList<TermDefinition>, NotFound, Forbidden, ServerError>>(
                                (HtmlDocument doc) => 
                                {
                                    var thingNodes = doc.DocumentNode
                                        .SelectNodes("//div[@class='thing text-text']");

                                    if (thingNodes == null)
                                    {
                                        _logger.Information("No term definitions found");
                                        return new List<TermDefinition>();
                                    }

                                    var termDefs = thingNodes.Select(thingNode => 
                                        {
                                            var thingId = thingNode.GetAttributeValue("data-thing-id", null);
                                            if (string.IsNullOrEmpty(thingId))
                                            {
                                                throw new InvalidOperationException("Thing ID not specified");
                                            }

                                            var termNode = thingNode.SelectSingleNode(
                                                "div[@class='col_a col text']/div[@class='text']");
                                            if (termNode == null)
                                            {
                                                throw new InvalidOperationException("Term node not found");
                                            }
                                            
                                            var defNode = thingNode.SelectSingleNode(
                                                "div[@class='col_b col text']/div[@class='text']");
                                            if (defNode == null)
                                            {
                                                throw new InvalidOperationException("Definition node not found");
                                            }

                                            var term = termNode.InnerText?.Trim();
                                            if (string.IsNullOrEmpty(term))
                                            {
                                                throw new InvalidOperationException("Term is empty");
                                            }

                                            var def = defNode.InnerText?.Trim();
                                            if (string.IsNullOrEmpty(def))
                                            {
                                                throw new InvalidOperationException("Definition is empty");
                                            }

                                            return new TermDefinition(thingId: thingId, term: term, definition: def);
                                        })
                                        .ToList();

                                    return termDefs;
                                },
                                (ServerError error) => error
                            );
                    }
                },
                (Forbidden error) => Task.FromResult<OneOf<IList<TermDefinition>, NotFound, Forbidden, ServerError>>(error),
                (ServerError error) => Task.FromResult<OneOf<IList<TermDefinition>, NotFound, Forbidden, ServerError>>(error)
            );
        }

        public async Task<OneOf<string, NotFound, Forbidden, ServerError>> 
            GetSingleLevelId(string courseId, string slug)
        {
            var path = $"course/{courseId}/{slug}/";
            var result = await WrapResponse(() => GetResource(DecksAddress, path));
            return await result.Match<Task<OneOf<string, NotFound, Forbidden, ServerError>>>(
                async (HttpResponseMessage message) => 
                {
                    using (message) 
                    {
                        return (await ParseAsHtml(message))
                            .Match<OneOf<string, NotFound, Forbidden, ServerError>>(
                                (HtmlDocument doc) => 
                                {
                                    var levelId = doc.DocumentNode
                                        .SelectSingleNode("/html/body")
                                        ?.Attributes["data-level-id"]?.Value;

                                    if (string.IsNullOrEmpty(levelId))
                                    {
                                        _logger.Error($"Failed to find a single course level");
                                        return (OneOf<string, NotFound, Forbidden, ServerError>) new NotFound();
                                    }

                                    return (OneOf<string, NotFound, Forbidden, ServerError>)levelId;
                                },
                                (ServerError error) => error
                            );
                    }
                },
                (Forbidden error) => Task.FromResult<OneOf<string, NotFound, Forbidden, ServerError>>(error),
                (ServerError error) => Task.FromResult<OneOf<string, NotFound, Forbidden, ServerError>>(error)
            );
        }

        public async Task<OneOf<Success, NotFound, Forbidden, ServerError>> 
            AddTermDefinition(string levelId, string term, string definition)
        {
            //TODO Return NotFound when not found

            string reqPath = "/ajax/level/thing/add/";

            var columns = new JObject();
            columns.Add("1", term);
            columns.Add("2", definition);

            var parameters = new List<KeyValuePair<string, string>> 
            {
                new KeyValuePair<string, string>("columns", columns.ToString()),
                new KeyValuePair<string, string>("level_id", levelId),
            };

            var result = await WrapResponse(() => PostResourceAsAjax(DecksAddress, reqPath: reqPath, pagePath: "/course/create/", parameters: parameters));
            return await result.Match<Task<OneOf<Success, NotFound, Forbidden, ServerError>>>(
                (HttpResponseMessage message) => 
                {
                    using (message) 
                    {
                        return Task.FromResult<OneOf<Success, NotFound, Forbidden, ServerError>>(new Success());
                    }
                },
                (Forbidden error) => Task.FromResult<OneOf<Success, NotFound, Forbidden, ServerError>>(error),
                (ServerError error) => Task.FromResult<OneOf<Success, NotFound, Forbidden, ServerError>>(error)
            );
        }

        public async Task<OneOf<Success, NotFound, Forbidden, ServerError>> 
            UpdateTermDefinition(string thingId, string definition)
        {
            string reqPath = "ajax/thing/cell/update/";

            // Update definition
            {
                var parameters = new List<KeyValuePair<string, string>> 
                {
                    new KeyValuePair<string, string>("thing_id", thingId),
                    new KeyValuePair<string, string>("cell_id", "2"),
                    new KeyValuePair<string, string>("cell_type", "column"),
                    new KeyValuePair<string, string>("new_val", definition),
                };

                var result = await WrapResponse(() => PostResourceAsAjax(DecksAddress, reqPath: reqPath, pagePath: "/course/create/", parameters: parameters));
                if (result.TryPickT0(out HttpResponseMessage message, out var remainder))
                {
                    using (message) {}
                }
                else 
                {
                    return remainder.Match<OneOf<Success, NotFound, Forbidden, ServerError>>(
                        (Forbidden error) => error,
                        (ServerError error) => error
                    );
                }
            }

            return new Success();
        }


        public async Task<OneOf<Success, NotFound, Forbidden, ServerError>> 
            UpsertTermDefinition(string courseId, string slug, string levelId, string term, string definition)
        {
            var getTermDefsResult = await GetTermDefinitions(courseId: courseId, slug: slug);
            if (getTermDefsResult.TryPickT0(out IList<TermDefinition> termDefs, out var remainder))
            {
                var foundTermDef = termDefs.FirstOrDefault(termDef => termDef.Term == term);
                if (foundTermDef == null)
                {
                    return await AddTermDefinition(levelId: levelId, term: term, definition: definition);
                }
                else 
                {
                    return await UpdateTermDefinition(thingId: foundTermDef.ThingId, definition: definition);
                }
            }
            else 
            {
                return remainder.Match<OneOf<Success, NotFound, Forbidden, ServerError>>(
                    (NotFound error) => error,
                    (Forbidden error) => error,
                    (ServerError error) => error
                );
            }
        }

        private async Task<OneOf<TData, ServerError>> ParseResponse<TData>(HttpResponseMessage message)
            where TData: class
        {
            try 
            {
                using (HttpContent content = message.Content)
                using (var stream = await content.ReadAsStreamAsync())
                using (var reader = new StreamReader(stream))
                using (var jsonReader = new JsonTextReader(reader))
                {
                    var js = new JsonSerializer();
                    var data = js.Deserialize<TData>(jsonReader);
                    return data;
                }
            }
            catch (JsonException ex) 
            {
                _logger.Error(ex, "Failed to parse response");
                return new ServerError();
            }
            catch (HttpRequestException ex)
            {
                _logger.Error(ex, "Failed to read response content");
                return new ServerError();
            }
        }

        private async Task<OneOf<HtmlDocument, ServerError>> ParseAsHtml(HttpResponseMessage message)
        {
            try 
            {
                using (HttpContent content = message.Content)
                {
                    using (var stream = await content.ReadAsStreamAsync())
                    {
                        var doc = new HtmlDocument();
                        doc.Load(stream);

                        return doc;
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.Error(ex, "Failed to read response content");
                return new ServerError();
            }
        }

        private async Task<HttpResponseMessage> GetResource(Uri baseAddress, string path)
        {
            var response = await _httpClient.GetAsync(new Uri(baseAddress, path));
            return response;
        }

        private async Task<HttpResponseMessage> PostResourceAsForm(Uri baseAddress, string reqPath, string pagePath, IList<KeyValuePair<string, string>> parameters)
        {
            string csrftoken = await RetrieveCsrfToken(baseAddress, path: pagePath);
            _logger.Debug($"RetrieveCsrfToken=>{csrftoken}");

            parameters.Add(new KeyValuePair<string, string>("csrfmiddlewaretoken", csrftoken));

            return await PostResource(
                baseAddress: baseAddress,
                reqPath: reqPath, 
                pagePath: pagePath, 
                csrftoken: csrftoken, 
                parameters: parameters);
        }

        private async Task<HttpResponseMessage> PostResourceAsAjax(Uri baseAddress, string reqPath, string pagePath, IList<KeyValuePair<string, string>> parameters)
        {
            string csrftoken = "";
            var uri = new Uri(baseAddress, reqPath);
            CookieCollection cookies = _cookieContainer.GetCookies(uri);
            foreach (Cookie cookie in cookies)
            {
                if (cookie.Name == "csrftoken" && cookie.Domain == baseAddress.Host)
                {
                    csrftoken = cookie.Value;
                    break;
                }
            }

            _logger.Debug($"RetrieveCsrfToken=>{csrftoken}");

            return await PostResource(
                baseAddress: baseAddress,
                reqPath: reqPath, 
                pagePath: pagePath, 
                csrftoken: csrftoken, 
                parameters: parameters);
        }

        private async Task<HttpResponseMessage> PostResource(Uri baseAddress, string reqPath, string pagePath, string csrftoken, IList<KeyValuePair<string, string>> parameters)
        {
            parameters.Add(new KeyValuePair<string, string>("csrfmiddlewaretoken", csrftoken));
            HttpContent reqContent = new FormUrlEncodedContent(parameters);

            var reqMsg = new HttpRequestMessage(HttpMethod.Post, new Uri(baseAddress, reqPath));
            reqMsg.Headers.Add("Origin", baseAddress.ToString());
            reqMsg.Headers.Add("Referer", new Uri(baseAddress, reqPath).ToString());
            reqMsg.Headers.Add("Connection", "keep-alive");
            reqMsg.Headers.Add("Pragma", "no-cache");
            reqMsg.Headers.Add("Cache-Control", "no-cache");
            reqMsg.Headers.Add("Upgrade-Insecure-Requests", "1");
            reqMsg.Headers.Add("User-Agent", "Mozilla/5.0");
            reqMsg.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8");
            reqMsg.Headers.Add("Accept-Encoding", "gzip, deflate, br");
            reqMsg.Headers.Add("Accept-Language", "en-US,en");
            reqMsg.Headers.Add("X-CSRFToken", csrftoken);

            reqMsg.Content = reqContent;

            using (HttpResponseMessage res = await _httpClient.SendAsync(reqMsg))
            using (HttpContent resContent = res.Content)
            {
                res.EnsureSuccessStatusCode();
                return res;
            }
        }

        private async Task<string> RetrieveCsrfToken(Uri baseAddress, string path)
        {
            using (HttpResponseMessage res = await _httpClient.GetAsync(new Uri(baseAddress, path)))
            using (HttpContent resContent = res.Content)
            {
                res.EnsureSuccessStatusCode();

                var resStr = await resContent.ReadAsStringAsync(); 
                var regex = new Regex(@"name=(\""|\')csrfmiddlewaretoken(\""|\') value=(\""|\')(?<token>[^(\""|\')]+)",
                          RegexOptions.None, TimeSpan.FromMilliseconds(150));
                var match = regex.Match(resStr);
                if (match.Success)
                {
                    var token = match.Result("${token}");
                    _logger.Debug($"csrfToken={token}");
                    return token;
                }
            }
            return null;
        }

        private async Task<OneOf<HttpResponseMessage, Forbidden, ServerError>> WrapResponse(Func<Task<HttpResponseMessage>> sendFunc)
        {
            try
            {
                var response = await sendFunc();

                if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    _logger.Warning("Forbidden to access Memrise API");
                    return new Forbidden();
                }

                response.EnsureSuccessStatusCode();

                return response;
            }
            catch (HttpRequestException ex)
            {
                _logger.Warning(ex, "Failed to communicate with server");
                return new ServerError();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unexpected error occurred when sending request to Memrise API");
                throw;
            }
        }
    }
}