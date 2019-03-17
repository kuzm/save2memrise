var target = Argument("target", "Default");

Task("Default")
    .IsDependentOn("UnitTest")
    .Does(() =>
{
});

Task("Build")
    .Does(() =>
{
    DotNetCoreBuild(".");
});

Task("UnitTest")
    .IsDependentOn("Build")
    .Does(() =>
{
    var settings = new DotNetCoreRunSettings
    {
        WorkingDirectory = "test/Services/Public.API.UnitTests"
    };
    DotNetCoreRun("Public.API.UnitTests.csproj", "", settings);
});

Task("IntegrationTest")
    .IsDependentOn("Build")
    .Does(() =>
{
    var settings = new DotNetCoreRunSettings
    {
        WorkingDirectory = "test/Services/Public.API.IntegrationTests"
    };
    DotNetCoreRun("Public.API.IntegrationTests.csproj", "", settings);
});

Task("Test")
    .IsDependentOn("UnitTest")
    .IsDependentOn("IntegrationTest")
  .Does(() =>
{
});

RunTarget(target);