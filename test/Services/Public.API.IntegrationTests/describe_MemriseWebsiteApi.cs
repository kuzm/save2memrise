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

        HttpClientHandler _httpHandler;
        HttpClient _httpClient;

        async Task before_each()
        {
            var memriseOptions = ConfigHelper.Get<MemriseOptions>("Memrise");

            var cookieContainer = new CookieContainer();
            
            _httpHandler = new HttpClientHandler();
            _httpClient = MemriseApi.HttpClientFactory.Create(_logger, _httpHandler, cookieContainer);
            {
                _sut = new MemriseApi.WebsiteClient(_logger, _httpClient, cookieContainer);
                
                // Log in
                var loginResult = await _sut.Login(username: memriseOptions.Username, password: memriseOptions.Password);
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

                // We cannot clean up existing courses and recreate a new ones because 
                // course creation is protected by reCAPTCHA
            }
        }

        void after_each()
        {
            _httpClient?.Dispose();
            _httpHandler?.Dispose();
        }

        void act_each()
        {
        }

        async Task it_should_return_dashboard_with_courses()
        {
            var dashboard = await GetDashboard();
            dashboard.Courses.ShouldNotBeNull();
            dashboard.Courses.Count.ShouldBe(2);

            {
                var course1 = dashboard.Courses.First(c => c.Name == "course1");
                course1.Id.ShouldBe("5491547");
                course1.NumLevels.ShouldBe(1);
                course1.Slug.ShouldBe("course1");
            }

            {
                var course2 = dashboard.Courses.First(c => c.Name == "course2");
                course2.Id.ShouldBe("5491555");
                course2.NumLevels.ShouldBe(1);
                course2.Slug.ShouldBe("course2");
            }
        }

        //TODO Course creation is not supported anymore
        /* 
        async Task it_should_return_empty_dashboard()
        {
            var dashboard = await GetDashboard(offset: 0, limit: MemriseApi.WebsiteClient.CourseLimitOnDashboard);
            dashboard.Courses.ShouldNotBeNull();
            dashboard.Courses.Count.ShouldBe(0);
        }
        
        async Task it_should_return_dashboard_with_created_course()
        {
            var name = "Course 1";
            var termLang = 8;
            var definitionLang = 5;
            await CreateCourse(name, termLang, definitionLang);

            var dashboard = await GetDashboard(offset: 0, limit: MemriseApi.WebsiteClient.CourseLimitOnDashboard);
            dashboard.Courses.ShouldNotBeNull();
            dashboard.Courses.Count.ShouldBe(1);
            
            var course = dashboard.Courses[0];
            course.Id.ShouldNotBeNullOrEmpty();
            course.Name.ShouldBe(name);
            course.NumLevels.ShouldBe(1);
            course.Slug.ShouldNotBeNullOrEmpty();
        }

        async Task it_should_find_course_on_first_dashboard_page()
        {
            var name1 = "Course 1";
            var name2 = "Course 1";
            var name3 = "Course 1";
            var termLang = 8;
            var definitionLang = 5;
            
            await CreateCourse(name: name1, termLang: termLang, definitionLang: definitionLang);
            await CreateCourse(name: name2, termLang: termLang, definitionLang: definitionLang);
            await CreateCourse(name: name3, termLang: termLang, definitionLang: definitionLang);

            var courseOption = await FindCourse(courseName: name3, limit: 2);
            courseOption.HasValue.ShouldBeTrue();
            
            var course = courseOption.ValueOrFailure();
            course.Name.ShouldBe(name3);
            course.Id.ShouldNotBeNullOrEmpty();
            course.NumLevels.ShouldBe(1);
            course.Slug.ShouldNotBeNullOrEmpty();
        }

        void given_many_courses_were_created()
        {
            new Each<string, int, int>
            {
                {"number of courses is not too many", 3, 2},
                {"number of courses is big", 10, MemriseApi.WebsiteClient.CourseLimitOnDashboard},
                {"number of courses is big with small page limit", 10, 3}
            }
            .Do((title, numberOfCourses, pageLimit) => 
            {
                itAsync[$"should find course on second dashboard page when {title}"] = async () => 
                {
                    var courseNames = new string[numberOfCourses];
                    for(int i = 0; i < numberOfCourses; ++i)
                    {
                        var name = $"Course {i+1}";
                        courseNames[i] = name;
                        var termLang = 8;
                        var definitionLang = 5;
                        await CreateCourse(name: name, termLang: termLang, definitionLang: definitionLang);
                    }
                    
                    var courseOption = await FindCourse(courseName: courseNames[0], limit: pageLimit);
                    courseOption.HasValue.ShouldBeTrue();
                    
                    var course = courseOption.ValueOrFailure();
                    course.Name.ShouldBe(courseNames[0]);
                    course.Id.ShouldNotBeNullOrEmpty();
                    course.NumLevels.ShouldBe(1);
                    course.Slug.ShouldNotBeNullOrEmpty();
                };
            });
        }

        async Task it_should_get_no_term_definitions_from_newly_created_course()
        {
            var courseName = "Course 1";
            var termLang = 8;
            var definitionLang = 5;
            
            await CreateCourse(name: courseName, termLang: termLang, definitionLang: definitionLang);
            
            var courseOption = await FindCourse(courseName: courseName, limit: 2);
            courseOption.HasValue.ShouldBeTrue();
            
            var course = courseOption.ValueOrFailure();
            var termDefs = await GetTermDefinitions(courseId: course.Id, slug: course.Slug);
            termDefs.ShouldNotBeNull();
            termDefs.Count.ShouldBe(0);
        }

        async Task it_should_get_term_definitions_which_has_been_added_to_course()
        {
            var courseName = "Course 1";
            var termLang = 8;
            var definitionLang = 5;
            
            await CreateCourse(name: courseName, termLang: termLang, definitionLang: definitionLang);
            
            var courseOption = await FindCourse(courseName: courseName, limit: 2);
            courseOption.HasValue.ShouldBeTrue();
            
            var course = courseOption.ValueOrFailure();
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
        }

        async Task it_should_get_updated_term_definition_which_has_been_added_to_course()
        {
            var courseName = "Course 1";
            var termLang = 8;
            var definitionLang = 5;
            
            await CreateCourse(name: courseName, termLang: termLang, definitionLang: definitionLang);
            
            var courseOption = await FindCourse(courseName: courseName, limit: 2);
            courseOption.HasValue.ShouldBeTrue();
            
            var course = courseOption.ValueOrFailure();
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
        }*/

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

        /* private async Task CreateCourse(string name, int termLang, int definitionLang)
        {
            var createCourseResult = await _sut.CreateCourse(
                name: name,
                termLang: termLang,
                definitionLang: definitionLang
            );
            createCourseResult.Switch(
                (Success _) => 
                {
                    _logger.Information("Course created");
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
        }*/

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