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

Task("BuildChromeExt")
    .Does(() =>
{
    Information("Running `npm install`...");
    var exitCode = StartProcess("npm", 
        new ProcessSettings 
        { 
            WorkingDirectory = "src/BrowserExts/ChromeExt",
            Arguments = "install"
        });
    if (exitCode != 0)
    {
        throw new Exception($"Exit code: {exitCode}");
    }

    Information("Running `npm install --global gulp`...");
    exitCode = StartProcess("npm", 
        new ProcessSettings 
        { 
            WorkingDirectory = "src/BrowserExts/ChromeExt",
            Arguments = "install --global gulp"
        });
    if (exitCode != 0)
    {
        throw new Exception($"Exit code: {exitCode}");
    }

    Information("Running `gulp`...");
    exitCode = StartProcess("gulp", 
        new ProcessSettings 
        { 
            WorkingDirectory = "src/BrowserExts/ChromeExt"
        });
    if (exitCode != 0)
    {
        throw new Exception($"Exit code: {exitCode}");
    }
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