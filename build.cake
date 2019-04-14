var target = Argument("target", "Default");
var env = Argument("env", "local");
var versionMetadata = Argument("version-metadata", "");

Information($"env: {env}");
if (env != "local" && env != "prod" && env != "prod-blue" && env != "prod-green")
{
    throw new ArgumentException(nameof(env));
}

Task("Default")
    .IsDependentOn("Build")
    .Does(() =>
    {
    });

Task("WriteVersionMetadata")
    .Does(() => 
    {
        System.IO.File.WriteAllText("version-metadata.txt", versionMetadata);
    });

Task("Build")
    .IsDependentOn("BuildPublicApi")
    .IsDependentOn("BuildChromeExt")
    .Does(() =>
    {
    });

Task("BuildPublicApi")
    .IsDependentOn("WriteVersionMetadata")
    .Does(() =>
    {
        DotNetCoreBuild(".");
    });

Task("BuildChromeExt")
    .IsDependentOn("WriteVersionMetadata")
    .Does(() =>
    {
        var basePath = "src/BrowserExts/ChromeExt";

        CopyFile(basePath + $"/config.{env}.js", basePath + "/config.js");

        Information("Running `npm install`...");
        var exitCode = StartProcess("npm", 
            new ProcessSettings 
            { 
                WorkingDirectory = basePath,
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
                WorkingDirectory = basePath,
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
                WorkingDirectory = basePath
            });
        if (exitCode != 0)
        {
            throw new Exception($"Exit code: {exitCode}");
        }
    });

Task("UnitTest")
    .IsDependentOn("BuildPublicApi")
    .Does(() =>
    {
        var settings = new DotNetCoreRunSettings
        {
            WorkingDirectory = "test/Services/Public.API.UnitTests"
        };
        DotNetCoreRun("Public.API.UnitTests.csproj", "", settings);
    });

Task("IntegrationTest")
    .IsDependentOn("BuildPublicApi")
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