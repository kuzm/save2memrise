using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NSpec;
using NSpec.Domain;
using OneOf.Types;
using RichardSzalay.MockHttp;
using Serilog;
using Tests.Common;
using Shouldly;
using Save2Memrise.Services.Public.API.Handlers;
using Optional;
using Optional.Unsafe;
using System.Collections.Generic;
using System.Linq;

namespace Save2Memrise.Services.Public.API.IntegrationTests
{
    public class describe_MemriseWebsiteApi : nspec
    {
        readonly ILogger _logger = Log.Logger;

        MemriseApi.WebsiteClient _sut;
        MemriseOptions _memriseOptions;
        HttpClientHandler _httpHandler;
        HttpClient _httpClient;

        

        void before_each()
        {
            _memriseOptions = ConfigHelper.Get<MemriseOptions>("Memrise");

            var cookieContainer = new CookieContainer();
            
            _httpHandler = new HttpClientHandler();
            _httpClient = MemriseApi.HttpClientFactory.Create(_logger, _httpHandler, cookieContainer);
            _sut = new MemriseApi.WebsiteClient(_logger, _httpClient, cookieContainer);

            // We cannot remove existing courses and recreate new ones because 
            // course creation is protected by reCAPTCHA
        }

        void after_each()
        {
            _httpClient?.Dispose();
            _httpHandler?.Dispose();
        }

        void given_account_with_one_course()
        {
            // Pre-configured course
            var course = new MemriseApi.CourseData
            {
                Name = "Course 1",
                Id = "5505795",
                NumLevels = 1,
                Slug = "course-1"
            };

            beforeAsync = async () => 
            {
                await Login(username: "test-with-one-course", password: _memriseOptions.Password);
                await RemoveAllTermDefinitions(courseName: "Course 1");
            };

            itAsync["it should return dashboard with one course"] = async () => 
            {
                var dashboard = await GetDashboard();
                dashboard.Courses.ShouldNotBeNull();
                dashboard.Courses.Count.ShouldBe(1);

                {
                    var course1 = dashboard.Courses.First(c => c.Name == course.Name);
                    course1.Id.ShouldBe(course.Id);
                    course1.NumLevels.ShouldBe(1);
                    course1.Slug.ShouldBe(course.Slug);
                }
            };

            itAsync["it should get no term definitions"] = async () => 
            {
                var termDefs = await GetTermDefinitions(courseId: course.Id, slug: course.Slug);
                termDefs.ShouldNotBeNull();
                termDefs.Count.ShouldBe(0);
            };

            itAsync["it should get term definitions which has been added to course"] = async () => 
            {
                var levelIdOption = await GetSingleLevelId(courseId: course.Id, slug: course.Slug);
                levelIdOption.HasValue.ShouldBeTrue();
                
                var levelId = levelIdOption.ValueOrFailure();
                levelId.ShouldNotBeNullOrEmpty();

                var term1 = "term 1";
                var def1 = "def 1";
                await AddTermDefinition(levelId: levelId, term: term1, definition: def1);

                var term2 = "term 2";
                var def2 = "def 2";
                await AddTermDefinition(levelId: levelId, term: term2, definition: def2);

                var termDefs = await GetTermDefinitions(courseId: course.Id, slug: course.Slug);
                termDefs.ShouldNotBeNull();
                termDefs.Count.ShouldBe(2);

                termDefs[0].ThingId.ShouldNotBeNullOrEmpty();
                termDefs[0].Term.ShouldBe(term1);
                termDefs[0].Definition.ShouldBe(def1);

                termDefs[1].ThingId.ShouldNotBeNullOrEmpty();
                termDefs[1].Term.ShouldBe(term2);
                termDefs[1].Definition.ShouldBe(def2);
            };

            itAsync["it should get updated term definition which has been added to course"] = async () => 
            {
                var levelIdOption = await GetSingleLevelId(courseId: course.Id, slug: course.Slug);
                levelIdOption.HasValue.ShouldBeTrue();
                
                var levelId = levelIdOption.ValueOrFailure();
                levelId.ShouldNotBeNullOrEmpty();

                var term1 = "term 1";
                var def1 = "def 1";
                await AddTermDefinition(levelId: levelId, term: term1, definition: def1);

                var termDefs = await GetTermDefinitions(courseId: course.Id, slug: course.Slug);
                termDefs.ShouldNotBeNull();
                termDefs.Count.ShouldBe(1);

                var def2 = "def 2";
                await UpdateTermDefinition(thingId: termDefs[0].ThingId, definition: def2);

                var updatedTermDefs = await GetTermDefinitions(courseId: course.Id, slug: course.Slug);
                updatedTermDefs.ShouldNotBeNull();
                updatedTermDefs.Count.ShouldBe(1);

                updatedTermDefs[0].ThingId.ShouldNotBeNullOrEmpty();
                updatedTermDefs[0].Term.ShouldBe(term1);
                updatedTermDefs[0].Definition.ShouldBe(def2);
            };
        }

        void given_account_with_no_courses()
        {
            beforeAsync = async () => 
            {
                await Login(username: "test-with-no-courses", password: _memriseOptions.Password);
            };

            itAsync["it should return dashboard with no courses"] = async () => 
            {
                var dashboard = await GetDashboard();
                dashboard.Courses.ShouldNotBeNull();
                dashboard.Courses.Count.ShouldBe(0);
            };
        }

        void given_course_with_many_courses()
        {
            // Pre-configured course
            var course = new MemriseApi.CourseData
            {
                Name = "Course 1",
                Id = "5505814",
                NumLevels = 1,
                Slug = "course-1"
            };
            
            beforeAsync = async () => 
            {
                await Login(username: "test-with-many-courses", password: _memriseOptions.Password);
            };

            new Each<string, int, int>
            {
                {"number of courses is big", 10, MemriseApi.WebsiteClient.CourseLimitOnDashboard},
                {"number of courses is big with small page limit", 10, 3}
            }
            .Do((title, numberOfCourses, pageLimit) => 
            {
                itAsync[$"should find course on second dashboard page when {title}"] = async () => 
                {
                    var courseOption = await FindCourse(courseName: course.Name, limit: pageLimit);
                    courseOption.HasValue.ShouldBeTrue();
                    
                    var course1 = courseOption.ValueOrFailure();
                    course1.Name.ShouldBe(course.Name);
                    course1.Id.ShouldBe(course.Id);
                    course1.NumLevels.ShouldBe(1);
                    course1.Slug.ShouldBe(course.Slug);
                };
            });
        }

        private async Task Login(string username, string password)
        {
            var loginResult = await _sut.Login(username: username, password: password);
            loginResult.Switch(
                (Success _) => 
                {
                    _logger.Information("Logged in!");
                },
                (MemriseApi.Forbidden error) => 
                {
                    throw new AssertionException("Forbidden");
                },
                (MemriseApi.ServerError error) => 
                {
                    throw new AssertionException("Server error");
                }
            );
        }

        private async Task<MemriseApi.DashboardData> GetDashboard()
        {
            var getDashboardResult = await _sut.GetDashboard();
            return getDashboardResult.Match<MemriseApi.DashboardData>(
                (MemriseApi.DashboardData dashboard) => 
                {
                    _logger.Information("Received dashboard: " + JsonConvert.SerializeObject(dashboard));
                    return dashboard;
                },
                (MemriseApi.Forbidden error) => 
                {
                    throw new AssertionException("Forbidden");
                },
                (MemriseApi.ServerError error) => 
                {
                    throw new AssertionException("Server error");
                });
        }

        private async Task<MemriseApi.DashboardData> GetDashboard(int offset, int limit)
        {
            var getDashboardResult = await _sut.GetDashboard(offset: offset, limit: limit);
            return getDashboardResult.Match<MemriseApi.DashboardData>(
                (MemriseApi.DashboardData dashboard) => 
                {
                    _logger.Information("Received dashboard: " + JsonConvert.SerializeObject(dashboard));
                    return dashboard;
                },
                (MemriseApi.Forbidden error) => 
                {
                    throw new AssertionException("Forbidden");
                },
                (MemriseApi.ServerError error) => 
                {
                    throw new AssertionException("Server error");
                });
        }

        private async Task QuitCourse(string courseId)
        {
            var quitCourseResult = await _sut.QuitCourse(courseId);
            quitCourseResult.Switch(
                (Success _) => 
                {
                    _logger.Information("Quited");
                },
                (MemriseApi.Forbidden error) => 
                {
                    throw new AssertionException("Forbidden");
                },
                (MemriseApi.ServerError error) => 
                {
                    throw new AssertionException("Server error");
                }
            );
        }

        private async Task AddTermDefinition(string levelId, string term, string definition)
        {
            var addTermDefResult = await _sut.AddTermDefinition(
                levelId: levelId,
                term: term,
                definition: definition
            );

            addTermDefResult.Switch(
                (Success _) => 
                {
                    _logger.Information("Term definition added");
                },
                (NotFound _) => 
                {
                    throw new AssertionException("NotFound");
                },
                (MemriseApi.Forbidden error) => 
                {
                    throw new AssertionException("Forbidden");
                },
                (MemriseApi.ServerError error) => 
                {
                    throw new AssertionException("Server error");
                }
            );
        }

        private async Task RemoveTermDefinition(string levelId, string thingId)
        {
            var removeTermDefResult = await _sut.RemoveTermDefinition(
                levelId: levelId,
                thingId: thingId
            );

            removeTermDefResult.Switch(
                (Success _) => 
                {
                    _logger.Information("Term definition removed");
                },
                (NotFound _) => 
                {
                    throw new AssertionException("NotFound");
                },
                (MemriseApi.Forbidden error) => 
                {
                    throw new AssertionException("Forbidden");
                },
                (MemriseApi.ServerError error) => 
                {
                    throw new AssertionException("Server error");
                }
            );
        }

        private async Task RemoveAllTermDefinitions(string courseName)
        {
            var courseOption = await FindCourse(courseName, limit: 5);
            var course = courseOption.ValueOrFailure();
            var termDefs = await GetTermDefinitions(courseId: course.Id, slug: course.Slug);
            foreach(var termDef in termDefs)
            {
                await RemoveTermDefinition(levelId: termDef.LevelId, thingId: termDef.ThingId);
            }
        }

        private async Task UpdateTermDefinition(string thingId, string definition)
        {
            var addTermDefResult = await _sut.UpdateTermDefinition(
                thingId: thingId,
                definition: definition
            );

            addTermDefResult.Switch(
                (Success _) => 
                {
                    _logger.Information("Term definition updated");
                },
                (NotFound _) => 
                {
                    throw new AssertionException("NotFound");
                },
                (MemriseApi.Forbidden error) => 
                {
                    throw new AssertionException("Forbidden");
                },
                (MemriseApi.ServerError error) => 
                {
                    throw new AssertionException("Server error");
                }
            );
        }

        private async Task UpsetTermDefinition(string courseId, string slug, string levelId, string term, string definition)
        {
            var addTermDefResult = await _sut.UpsertTermDefinition(
                courseId: courseId,
                slug: slug,
                levelId: levelId,
                term: term,
                definition: definition
            );

            addTermDefResult.Switch(
                (Success _) => 
                {
                    _logger.Information("Term definition updated");
                },
                (NotFound _) => 
                {
                    throw new AssertionException("NotFound");
                },
                (MemriseApi.Forbidden error) => 
                {
                    throw new AssertionException("Forbidden");
                },
                (MemriseApi.ServerError error) => 
                {
                    throw new AssertionException("Server error");
                }
            );
        }

        private async Task<Option<MemriseApi.CourseData>> FindCourse(string courseName, int limit)
        {
            var findCourseResult = await _sut.FindCourse(courseName: courseName, limit: limit);
            
            return findCourseResult.Match(
                (MemriseApi.CourseData course) => 
                {
                    _logger.Information("Course found");
                    return Option.Some(course);
                },
                (NotFound _) => 
                {
                    return Option.None<MemriseApi.CourseData>();
                },
                (MemriseApi.Forbidden error) => 
                {
                    throw new AssertionException("Forbidden");
                },
                (MemriseApi.ServerError error) => 
                {
                    throw new AssertionException("Server error");
                }
            );
        }

        private async Task<Option<string>> GetSingleLevelId(string courseId, string slug)
        {
            var getLevelIdResult = await _sut.GetSingleLevelId(courseId: courseId, slug: slug);
            
            return getLevelIdResult.Match(
                (string levelId) => 
                {
                    _logger.Information("Level ID found");
                    return Option.Some(levelId);
                },
                (NotFound _) => 
                {
                    return Option.None<string>();
                },
                (MemriseApi.Forbidden error) => 
                {
                    throw new AssertionException("Forbidden");
                },
                (MemriseApi.ServerError error) => 
                {
                    throw new AssertionException("Server error");
                }
            );
        }

        private async Task<IList<MemriseApi.TermDefinition>> GetTermDefinitions(string courseId, string slug)
        {
            var getTermDefsResult = await _sut.GetTermDefinitions(courseId: courseId, slug: slug);
            
            return getTermDefsResult.Match(
                (IList<MemriseApi.TermDefinition> termDefs) => 
                {
                    _logger.Information("Term defs found");
                    return termDefs;
                },
                (NotFound _) => 
                {
                    throw new AssertionException("NotFound");
                },
                (MemriseApi.Forbidden error) => 
                {
                    throw new AssertionException("Forbidden");
                },
                (MemriseApi.ServerError error) => 
                {
                    throw new AssertionException("Server error");
                }
            );
        }
    }
}