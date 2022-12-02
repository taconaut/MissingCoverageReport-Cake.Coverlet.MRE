//////////////////////////////////////////////////////////////////////
// ADD-INS
//////////////////////////////////////////////////////////////////////
#addin nuget:?package=Cake.Git&version=2.0.0
#addin nuget:?package=Cake.Coverlet&version=3.0.2

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var _target = Argument("target", "Default");
var _configuration = Argument("configuration", "Release");
var _applicationVersion = Argument("applicationVersion", "0.0.0.1");
var _nugetServer = Argument("nugetServer", string.Empty);

//////////////////////////////////////////////////////////////////////
// PROPERTIES
//////////////////////////////////////////////////////////////////////

var _applicationBaseName = "MRE";

var _gitRepoDir = Directory("../");

var _artifactsDir = MakeAbsolute(Directory("artifacts"));
var _packagesDir = _artifactsDir.Combine(Directory("packages"));

var _testOutputDir = MakeAbsolute(Directory("./test-output"));
var _testCoverageResultsDir = MakeAbsolute(_testOutputDir.Combine(Directory("coverage-results")));
var _testResultsDir = MakeAbsolute(_testOutputDir.Combine(Directory ("test-results")));

var _solutionPath = $"{_applicationBaseName}.sln";

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("Clean")
    .Does(() =>
    {
        CleanDirectory (_artifactsDir);
        Information ($"Deleted all content in '{_artifactsDir}'");
        CleanDirectory (_testCoverageResultsDir);
        Information ($"Deleted all content in '{_testCoverageResultsDir}'");
        CleanDirectory (_testResultsDir);
        Information ($"Deleted all content in '{_testResultsDir}'");
        CleanDirectories($"**/obj/{_configuration}");
        Information($"Deleted all content in '**/obj/{_configuration}'.");
        CleanDirectories($"**/bin/{_configuration}");
        Information($"Deleted all content in '**/bin/{_configuration}'.");
    });

Task("Restore-NuGet-Packages")
    .IsDependentOn("Clean")
    .Does(() => {
        DotNetRestore(_solutionPath);
        Information($"Restored nuget packages for solution '{_solutionPath}'.");
    });

Task("Build")
    .IsDependentOn("Clean")
    .IsDependentOn("Application-Build");

Task("Application-Build")
    .IsDependentOn("Clean")
    .IsDependentOn("Restore-NuGet-Packages")
    .Does(() => {
        DotNetBuild(
            _solutionPath,
            new DotNetBuildSettings{
                NoRestore = true,
                Configuration = _configuration
            }
        );
        Information($"Finished building solution '{_solutionPath}'.");
    });

Task("Release-Prepare")
    .Does(() => {
        // Compute the package version
        var gitCommitHash = GitLogTip(_gitRepoDir)?.Sha;
        var gitCommitHashShort = gitCommitHash?.Substring(0, 8) ?? "Unknown";
        var currentDateTime = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var packageVersion = $"{_applicationVersion}-{gitCommitHashShort}-{currentDateTime}";

        // Versions having to be modified in *.csproj
        var versionXPath = "/Project/PropertyGroup/Version";
        var assemblyVersionXPath = "/Project/PropertyGroup/AssemblyVersion";
        var fileVersionXPath = "/Project/PropertyGroup/FileVersion";

        // Set versions for all projects (except testing)
        var projectFilePaths = GetFiles("./**/*.csproj");
        foreach(var projectFilePath in projectFilePaths) {
            if (projectFilePath.FullPath.Contains("Tests")) {
                // Do not version test projects
                continue;
            }

            XmlPoke(projectFilePath, versionXPath, packageVersion);
            XmlPoke(projectFilePath, assemblyVersionXPath, _applicationVersion);
            XmlPoke(projectFilePath, fileVersionXPath, _applicationVersion);
        }

        Information ($"Updated projects with AssemblyVersion/FileVersion='{_applicationVersion}' and Version='{packageVersion}'.");
    });

//////////////////////////////////////////////////////////////////////
// Tests
//////////////////////////////////////////////////////////////////////
Task("Unit-Test")
    .IsDependentOn("Application-Build")
    .Does (() => {
        RunTests(GetFiles($"./**/{_applicationBaseName}*.Tests.Unit.csproj"));
    });

Task("Integration-Test")
    .IsDependentOn("Application-Build")
    .Does (() => {
        RunTests(GetFiles($"./**/{_applicationBaseName}*.Tests.Integration.csproj"));
    });

Task("FullStack-Test")
    .IsDependentOn("Application-Build")
    .Does (() => {
        RunTests(GetFiles($"./**/{_applicationBaseName}*.Tests.FullStack.csproj"));
    });	

Task("Test")
    .IsDependentOn("Build")
    .IsDependentOn("Unit-Test")
    .IsDependentOn("Integration-Test")
    .IsDependentOn("FullStack-Test");

var _testSettings = new DotNetTestSettings {
    NoBuild = true,
    NoRestore = true,
    Configuration = _configuration,
    ResultsDirectory = _testResultsDir
};

var _coverletSettings = new CoverletSettings {
    CollectCoverage = true,
    CoverletOutputFormat = CoverletOutputFormat.cobertura,
    CoverletOutputDirectory = _testCoverageResultsDir,
    Exclude = new List<string> { "[xunit*]*", $"[*{_applicationBaseName}.Tests.*]*" },
    Include = new List<string> { $"[*{_applicationBaseName}*]*" }
};

private void RunTests(FilePathCollection projectFiles){
    foreach(var projectFile in projectFiles)
    {
        var projectFilePath = projectFile.Segments.Last();

        // Test result customization
        var testResultFileName = $"test-results-{projectFilePath}.xml";
        _testSettings.ArgumentCustomization = args => args.Append($"--logger:trx;LogFileName={testResultFileName}");

        // Coverage result customization
        var coverageResultFileName = $"coverage-results-{projectFilePath}.cobertura.xml";
        _coverletSettings.CoverletOutputName = coverageResultFileName;

        DotNetTest(projectFile.FullPath, _testSettings, _coverletSettings);
    }
}

//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Default")
    .IsDependentOn("Build");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(_target);