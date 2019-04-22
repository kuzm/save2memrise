var target = Argument("target", "Default");
var env = Argument("env", "local");
var versionMetadata = Argument("version-metadata", "");
var awsRegion = Argument("aws-region", "");
var awsAccountId = Argument("aws-account-id", "");
var webS3Bucket = Argument("web-s3-bucket", "");
var cdnDistributionId = Argument("cdn-distribution-id", "");

Information($"env: {env}");
if (env != "local" && env != "prod" && env != "prod-blue" && env != "prod-green")
{
    throw new ArgumentException(nameof(env));
}

class Version
{
    public readonly string Base;
    public readonly string Metadata;
    public readonly string FullVersion;

    public Version(string versionBase, string metadata)
    {
        Base = !string.IsNullOrEmpty(versionBase) ? versionBase : throw new ArgumentException(nameof(versionBase));
        Metadata = metadata;

        FullVersion = string.IsNullOrEmpty(Metadata)
            ? Base
            : $"{Base}-{Metadata}";
    }
}
Version version;

Task("Default")
    .IsDependentOn("Build")
    .Does(() =>
    {
    });

Task("ConfigureVersion")
    .Does(() => 
    {
        System.IO.File.WriteAllText("version-metadata.txt", versionMetadata);
        var versionBase = System.IO.File.ReadAllText("version.txt").Trim();
        version = new Version(versionBase, versionMetadata);
        Information($"Full version: {version.FullVersion}");
    });

Task("Build")
    .IsDependentOn("ConfigureVersion")
    .IsDependentOn("BuildPublicApi")
    .IsDependentOn("BuildChromeExt")
    .Does(() =>
    {
    });

Task("BuildPublicApi")
    .IsDependentOn("ConfigureVersion")
    .Does(() =>
    {
        DotNetCoreBuild(".");
    });

Task("BuildChromeExt")
    .IsDependentOn("ConfigureVersion")
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

Task("LoginToECR")
    .Does(() =>
    {
        if (string.IsNullOrEmpty(awsRegion))
            throw new InvalidOperationException(nameof(awsRegion));
        
        Information("Running `aws ecr get-login`...");
        var exitCode = StartProcess("sh", 
            new ProcessSettings 
            { 
                WorkingDirectory = ".",
                Arguments = new ProcessArgumentBuilder()
                    .Append("-c")
                    .AppendQuoted($"$(aws ecr get-login --no-include-email --region {awsRegion})")
            });
        if (exitCode != 0)
        {
            throw new Exception($"Exit code: {exitCode}");
        }
    });

Task("BuildPublicApiDockerImage")
    .IsDependentOn("BuildPublicApi")
    .Does(() =>
    {
        var imageRepoName = "save2memrise";
        
        Information("Running `docker build`...");
        var exitCode = StartProcess("docker", 
            new ProcessSettings 
            { 
                WorkingDirectory = ".",
                Arguments = $"build --build-arg APP_VERSION={version.FullVersion} --tag {imageRepoName}:{version.FullVersion} --file src/Services/Public.API/Dockerfile ."
            });
        if (exitCode != 0)
        {
            throw new Exception($"Exit code: {exitCode}");
        }
    });

Task("PushPublicApiDockerImage")
    .IsDependentOn("LoginToECR")
    .IsDependentOn("BuildPublicApiDockerImage")
    .Does(() =>
    {
        if (string.IsNullOrEmpty(awsRegion))
            throw new ArgumentException(nameof(awsRegion)); 
        if (string.IsNullOrEmpty(awsAccountId))
            throw new ArgumentException(nameof(awsAccountId)); 

        var imageRepoName = "save2memrise";

        Information("Running `docker tag`...");
        var exitCode = StartProcess("docker", 
            new ProcessSettings 
            { 
                WorkingDirectory = ".",
                Arguments = $"tag {imageRepoName}:{version.FullVersion} {awsAccountId}.dkr.ecr.{awsRegion}.amazonaws.com/{imageRepoName}:{version.FullVersion}"
            });
        if (exitCode != 0)
        {
            throw new Exception($"Exit code: {exitCode}");
        }


        Information("Running `docker push`...");
        exitCode = StartProcess("docker", 
            new ProcessSettings 
            { 
                WorkingDirectory = ".",
                Arguments = $"push {awsAccountId}.dkr.ecr.{awsRegion}.amazonaws.com/{imageRepoName}:{version.FullVersion}"
            });
        if (exitCode != 0)
        {
            throw new Exception($"Exit code: {exitCode}");
        }

        var imagedefinitionsJson = $"[{{\"name\":\"public-api\",\"imageUri\":\"{awsAccountId}.dkr.ecr.{awsRegion}.amazonaws.com/{imageRepoName}:{version.FullVersion}\"}}]";
        System.IO.File.WriteAllText("imagedefinitions.json", imagedefinitionsJson);
    });

Task("DeployPublicApi")
    .IsDependentOn("UnitTest")
    .IsDependentOn("PushPublicApiDockerImage")
    .Does(() =>
    {});

Task("PushChromeExtFrameToS3")
    .IsDependentOn("BuildChromeExt")
    .Does(() =>
    {
        if (string.IsNullOrEmpty(webS3Bucket))
            throw new InvalidOperationException(nameof(webS3Bucket));
        
        Information("Running `aws s3 cp`...");
        var exitCode = StartProcess("aws", 
            new ProcessSettings 
            { 
                WorkingDirectory = "src/BrowserExts/ChromeExt",
                Arguments = $"s3 cp --recursive --acl public-read ./build/ s3://{webS3Bucket}/"
            });
        if (exitCode != 0)
        {
            throw new Exception($"Exit code: {exitCode}");
        }
    });

Task("InvalidateCloudFrontCache")
    .Does(() =>
    {
        if (string.IsNullOrEmpty(cdnDistributionId))
            throw new InvalidOperationException(nameof(cdnDistributionId));
        
        Information("Running `aws cloudfront create-invalidation`...");
        var exitCode = StartProcess("aws", 
            new ProcessSettings 
            { 
                WorkingDirectory = ".",
                Arguments = $"cloudfront create-invalidation --distribution-id {cdnDistributionId} --paths \"/*\""
            });
        if (exitCode != 0)
        {
            throw new Exception($"Exit code: {exitCode}");
        }
    }); 

Task("DeployChromeExtFrame")
    .IsDependentOn("BuildChromeExt")
    .IsDependentOn("PushChromeExtFrameToS3")
    .IsDependentOn("InvalidateCloudFrontCache")
    .Does(() =>
    {});


Task("BuildBuilderDockerImage")
    .IsDependentOn("ConfigureVersion")
    .Does(() =>
    {
        var imageRepoName = "save2memrise/build";

        Information("Running `docker build`...");
        var exitCode = StartProcess("docker", 
            new ProcessSettings 
            { 
                WorkingDirectory = ".",
                Arguments = $"build --file build.Dockerfile --tag {imageRepoName}:{version.FullVersion} ."
            });
        if (exitCode != 0)
        {
            throw new Exception($"Exit code: {exitCode}");
        }
    });

Task("PushBuilderDockerImage")
    .IsDependentOn("LoginToECR")
    .IsDependentOn("BuildBuilderDockerImage")
    .Does(() =>
    {
        if (string.IsNullOrEmpty(awsRegion))
            throw new ArgumentException(nameof(awsRegion)); 
        if (string.IsNullOrEmpty(awsAccountId))
            throw new ArgumentException(nameof(awsAccountId)); 

        var imageRepoName = "save2memrise/build";

        Information("Running `docker tag`...");
        var exitCode = StartProcess("docker", 
            new ProcessSettings 
            { 
                WorkingDirectory = ".",
                Arguments = $"tag {imageRepoName}:{version.FullVersion} {awsAccountId}.dkr.ecr.{awsRegion}.amazonaws.com/{imageRepoName}:{version.FullVersion}"
            });
        if (exitCode != 0)
        {
            throw new Exception($"Exit code: {exitCode}");
        }

        Information("Running `docker push`...");
        exitCode = StartProcess("docker", 
            new ProcessSettings 
            { 
                WorkingDirectory = ".",
                Arguments = $"push {awsAccountId}.dkr.ecr.{awsRegion}.amazonaws.com/{imageRepoName}:{version.FullVersion}"
            });
        if (exitCode != 0)
        {
            throw new Exception($"Exit code: {exitCode}");
        }
    });

RunTarget(target);