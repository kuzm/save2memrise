using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Newtonsoft.Json;
using Microsoft.Extensions.Primitives;
using System.Net;
using Optional;
using Optional.Unsafe;
using Serilog;
using System.Net.Http;
using OneOf;
using OneOf.Types;

namespace Save2Memrise.Services.Public.API.Controllers
{
    public class GetCourseItemsData
    {
        [JsonProperty("items")]
        public IList<CourseItemData> Items { get; set; }
    }

    public class CourseItemData
    {
        [JsonProperty("term")]
        public string Term { get; set; }

        [JsonProperty("def")]
        public string Definition { get; set; }
    }

    public class PutCourseItemData
    {
        [JsonProperty("term")]
        public string Term { get; set; }

        [JsonProperty("termLang")]
        public int TermLang { get; set; }

        [JsonProperty("def")]
        public string Definition { get; set; }

        [JsonProperty("defLang")]
        public int DefinitionLang { get; set; }

        public override string ToString()
        {
            return $"[{nameof(PutCourseItemData)} Term={Term}, Definition={Definition}]";
        }
    }

    [Route("v1/courses")]
    public class CoursesController : Controller
    {
        private readonly ILogger _logger;

        public CoursesController(ILogger logger)
        {
            _logger = logger.ForContext<CoursesController>();
        }

        [HttpGet("{courseId}/items")]
        public async Task<IActionResult> GetCourseItems(
            [FromQuery(Name = "term_lang")] int termLang, 
            [FromQuery(Name = "def_lang")] int defLang, 
            [FromHeader(Name = "Memrise-Cookies")] string memriseCookies)
        {
            var memriseCookiesList = JsonConvert.DeserializeObject<List<MemriseApi.CookieData>>(memriseCookies);
            var cookieContainer = MemriseApi.CookieContainerFactory.Create(memriseCookiesList);
            using (var httpHandler = new HttpClientHandler())
            using (var httpClient = MemriseApi.HttpClientFactory.Create(_logger, httpHandler, cookieContainer))
            {
                var client = new MemriseApi.WebsiteClient(_logger, httpClient, cookieContainer);

                var courseName = GetCourseName(termLang: termLang, defLang: defLang);
                
                var findCourseResult = await client.FindCourse(courseName: courseName, limit: MemriseApi.WebsiteClient.CourseLimitOnDashboard);
                return await findCourseResult.Match<Task<IActionResult>>(
                    (MemriseApi.CourseData course) => 
                    {
                        return GerItemsFromFoundCourse(client, course);
                    },
                    (NotFound _) => 
                    {
                        return Task.FromResult<IActionResult>(NotFound());
                    },
                    (MemriseApi.Forbidden error) => 
                    {
                        return Task.FromResult<IActionResult>(
                            StatusCode((int)HttpStatusCode.Forbidden));
                    },
                    (MemriseApi.ServerError error) => 
                    {
                        return Task.FromResult<IActionResult>(
                            StatusCode((int)HttpStatusCode.BadGateway));
                    }
                );
            }
        }

        [HttpPut("{courseId}/items")]
        public async Task<IActionResult> PutCourseItem(
            string courseId, [FromBody] PutCourseItemData item,
            [FromHeader(Name = "Memrise-Cookies")] string memriseCookies)
        {
            var memriseCookiesList = JsonConvert.DeserializeObject<List<MemriseApi.CookieData>>(memriseCookies);
            var cookieContainer = MemriseApi.CookieContainerFactory.Create(memriseCookiesList);
            using (var httpHandler = new HttpClientHandler())
            using (var httpClient = MemriseApi.HttpClientFactory.Create(_logger, httpHandler, cookieContainer))
            {
                if (item == null)
                {
                    return BadRequest("Payload is empty");
                }
                
                item.Term = item.Term?.Trim();
                item.Definition = item.Definition?.Trim();

                if (string.IsNullOrEmpty(item.Term))
                {
                    return BadRequest("term is empty");
                }

                if (string.IsNullOrEmpty(item.Definition))
                {
                    return BadRequest("def is empty");
                }

                if (item.TermLang <= 0)
                {
                    return BadRequest("termLang is invalid");
                }

                if (item.DefinitionLang <= 0)
                {
                    return BadRequest("defLang is invalid");
                }

                var client = new MemriseApi.WebsiteClient(_logger, httpClient, cookieContainer);

                var courseName = GetCourseName(termLang: item.TermLang, defLang: item.DefinitionLang);

                var findCourseResult = await client.FindCourse(courseName: courseName, limit: MemriseApi.WebsiteClient.CourseLimitOnDashboard);
                return await findCourseResult.Match<Task<IActionResult>>(
                    (MemriseApi.CourseData course) => 
                    {
                        return UpsertTermDefinitionToFoundCourse(client, item, courseName, Option.Some(course));
                    },
                    (NotFound _) => 
                    {
                        return UpsertTermDefinitionToFoundCourse(client, item, courseName, Option.None<MemriseApi.CourseData>());
                    },
                    (MemriseApi.Forbidden error) => 
                    {
                        return Task.FromResult<IActionResult>(
                            StatusCode((int)HttpStatusCode.Forbidden));
                    },
                    (MemriseApi.ServerError error) => 
                    {
                        return Task.FromResult<IActionResult>(
                            StatusCode((int)HttpStatusCode.BadGateway));
                    }
                );
            }
        }

        private string GetCourseName(int termLang, int defLang)
        {
            var termLangLabel = MemriseApi.WebsiteClient.GetTermLangLabel(termLang).Short;
            var defLangLabel = MemriseApi.WebsiteClient.GetDefinitionLangLabel(defLang).Short;
            var courseName = $"Save2Memrise {termLangLabel}-{defLangLabel}";
            return courseName;
        }

        private async Task<IActionResult> GerItemsFromFoundCourse(
            MemriseApi.WebsiteClient client, 
            MemriseApi.CourseData course)
        {
            _logger.Debug($"Course found: {course.Id}");
            var getTermDefsResult = await client.GetTermDefinitions(courseId: course.Id, slug: course.Slug);
            return getTermDefsResult.Match<IActionResult>(
                (IList<MemriseApi.TermDefinition> termDefs) => 
                {
                    var response = new GetCourseItemsData
                    {
                        Items = termDefs
                            .Select(termDef => 
                                new CourseItemData
                                {
                                    Term = termDef.Term,
                                    Definition = termDef.Definition
                                })
                            .ToList()
                    };
                    return Ok(response);
                },
                (NotFound _) => 
                {
                    return NotFound();
                },
                (MemriseApi.Forbidden _) => 
                {
                    return StatusCode((int)HttpStatusCode.Forbidden);
                },
                (MemriseApi.ServerError _) => 
                {
                    return StatusCode((int)HttpStatusCode.BadGateway);
                }
            );
        }

        private Task<IActionResult> UpsertTermDefinitionToFoundCourse(
            MemriseApi.WebsiteClient client, 
            PutCourseItemData item, 
            string courseName,
            Option<MemriseApi.CourseData> courseOption)
        {
            return courseOption.Match<Task<IActionResult>>(
                some: async course => 
                {
                    _logger.Debug($"Course found: {course.Id}");
                    return await UpsertTermDefinitionToCourseLevel(client, course, item);
                },
                none: async () => 
                {
                    var createCourseResult = await CreateCourse(client, item, courseName);
                    return await createCourseResult.Match<Task<IActionResult>>(
                        async (MemriseApi.CourseData newCourse) => 
                        {
                            _logger.Debug($"Course created: {newCourse?.Id}");
                            return await UpsertTermDefinitionToCourseLevel(client, newCourse, item);
                        },
                        (IActionResult result) => 
                        {
                            return Task.FromResult(result);
                        }
                    );
                }
            );
        }

        private async Task<IActionResult> 
            UpsertTermDefinitionToCourseLevel(MemriseApi.WebsiteClient client, MemriseApi.CourseData course, PutCourseItemData item)
        {
            var getSingleLevelIdResult = await client.GetSingleLevelId(course.Id, course.Slug);
            return await getSingleLevelIdResult.Match<Task<IActionResult>>(
                async (string levelId) => 
                {
                    _logger.Debug($"Level found: {levelId}");
                    var addTermDefResult = await client.UpsertTermDefinition(
                        courseId: course.Id,
                        slug: course.Slug,
                        levelId: levelId,
                        term: item.Term,
                        definition: item.Definition);

                    return addTermDefResult.Match<IActionResult>(
                        (Success _) => Ok(new object()),
                        (NotFound _) => NotFound(),
                        (MemriseApi.Forbidden _) => StatusCode((int)HttpStatusCode.Forbidden),
                        (MemriseApi.ServerError _) => StatusCode((int)HttpStatusCode.BadGateway)
                    );
                },
                (NotFound error) => 
                {
                    _logger.Error($"Failed to find a single course level");
                    return Task.FromResult<IActionResult>(StatusCode((int)HttpStatusCode.InternalServerError));
                },
                (MemriseApi.Forbidden error) => 
                {
                    _logger.Error($"Forbidden to access Memrise");
                    return Task.FromResult<IActionResult>(StatusCode((int)HttpStatusCode.Forbidden));
                },
                (MemriseApi.ServerError error) => 
                {
                    _logger.Error($"Failed to access Memrise");
                    return Task.FromResult<IActionResult>(StatusCode((int)HttpStatusCode.BadGateway));
                }
            );
        }

        private async Task<OneOf<MemriseApi.CourseData, IActionResult>> CreateCourse(
            MemriseApi.WebsiteClient client, PutCourseItemData item, string courseName)
        {
            var createCourseResult = await client.CreateCourse(courseName, termLang: item.TermLang, definitionLang: item.DefinitionLang);
            return await createCourseResult.Match<Task<OneOf<MemriseApi.CourseData, IActionResult>>>(
                async (Success _) => 
                {
                    var findCourseResult = await client.FindCourse(courseName: courseName, limit: 1);
                    return findCourseResult.Match<OneOf<MemriseApi.CourseData, IActionResult>>(
                        (MemriseApi.CourseData course) => 
                        {
                            return course;
                        }, 
                        (NotFound notFound) => 
                        {
                            _logger.Error("Created course {CourseName} was not found", courseName);
                            return StatusCode((int)HttpStatusCode.InternalServerError);
                        },
                        (MemriseApi.Forbidden error) => 
                        {
                            return StatusCode((int)HttpStatusCode.Forbidden);
                        },
                        (MemriseApi.ServerError error) => 
                        {
                            return StatusCode((int)HttpStatusCode.BadGateway);
                        }
                    );
                },
                (MemriseApi.Forbidden error) => 
                {
                    _logger.Error($"Forbidden to access Memrise");
                    return Task.FromResult<OneOf<MemriseApi.CourseData, IActionResult>>(StatusCode((int)HttpStatusCode.Forbidden));
                },
                (MemriseApi.ServerError error) => 
                {
                    _logger.Error($"Failed to access Memrise");
                    return Task.FromResult<OneOf<MemriseApi.CourseData, IActionResult>>(StatusCode((int)HttpStatusCode.BadGateway));
                }
            );
        }
    }
}
