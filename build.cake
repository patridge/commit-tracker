#addin nuget:?package=Cake.ArgumentHelpers
#addin nuget:?package=Mono.Options
#addin nuget:?package=Octokit
// #addin nuget:?package=Cake.Git

using Mono.Options;
// using LibGit2Sharp;

const string environmentVariablePrefix = "CommitTracker_";
const string gitHubAccessTokenVariableKey = "GitHubAccessToken";

var gitHubAccessToken = ArgumentOrEnvironmentVariable("GitHubAccessToken", environmentVariablePrefix, null);
var gitHubRepoToWatch = ArgumentOrEnvironmentVariable("GitHubWatchRepo", environmentVariablePrefix, null);
Octokit.GitHubClient gitHubClient;
Octokit.RepositoriesClient gitHubReposClient;
string gitHubRepoName = null;
string gitHubRepoOwner = null;
string gitHubRepoBranchName = "master";
string[] gitRepoPaths = new string[0];

var options = new OptionSet {
    {
        "token|gitHubAccessTokenVariableKey=",
        "What is your GitHub personal access token (also environment variable `" + (environmentVariablePrefix + gitHubAccessTokenVariableKey) + "`)? (Need a token? https://github.com/settings/tokens/new)",
        t => gitHubAccessToken = t
    },
    { "repoOwner=", "Owner name of repo", owner => gitHubRepoOwner = owner },
    { "repoName=", "Repo name to analyze", name => gitHubRepoName = name },
    { "repoBranch=", "Branch name within repo", branch =>  gitHubRepoBranchName = branch },
    { "repoPaths=", "Path within repo to observe for changes", paths => gitRepoPaths = paths.Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries) },
};

// NOTE: Skip two args because they are Cake script items (e.g., `/…/Cake.exe` and `build.cake`), which screws up Mono.Options because a console app's args do not include it.
var args = System.Environment.GetCommandLineArgs().Skip(2).ToArray();
List<string> extraArgs;
try {
    // parse the command line
    extraArgs = options.Parse(args);
    // NOTE: extraArgs will usually contain various Cake items, so probably not worth using for too much error logic.
    // e.g., macOS run: "/Users/someuser/Project/xamu-scripts/tools/Cake/Cake.exe", "build.cake" }
} catch (OptionException e) {
    Console.WriteLine("Error parsing inputs: " + e.Message);
    return;
}

gitHubClient = new Octokit.GitHubClient(new Octokit.ProductHeaderValue("submodule-helper")) {
    Credentials = new Octokit.Credentials(gitHubAccessToken),
};
var apiConnection = new Octokit.ApiConnection(gitHubClient.Connection);
var repoClient = new Octokit.RepositoriesClient(apiConnection);
// var forksClient = new Octokit.RepositoryForksClient(apiConnection);
var referencesClient = new Octokit.ReferencesClient(apiConnection);
// var pullRequestsClient = new Octokit.PullRequestsClient(apiConnection);
var commitsClient = new Octokit.CommitsClient(apiConnection);

if (gitHubRepoOwner == null) {
    var currentUser = await gitHubClient.User.Current();
    gitHubRepoOwner = currentUser.Name;
}

var target = Argument("target", "Default");

// Display resulting argument values for debugging.
Task("Debug-InfoDump")
.Description("Outputs the values found in the various supplied command-line and environment variables. Also outputs the command line help system.")
.Does(() => {
    Information($"{nameof(gitHubAccessToken)}: {gitHubAccessToken}");
    Information($"{nameof(gitHubRepoName)}/{nameof(gitHubRepoOwner)}: {gitHubRepoName}/{gitHubRepoOwner}");
    Information($"{nameof(gitHubRepoBranchName)}: {gitHubRepoBranchName}");
    Information($"{nameof(gitRepoPaths)}: {string.Join(", ", gitRepoPaths)}");

    // output the options
    Console.WriteLine();
    Console.WriteLine("Command-line options:");
    options.WriteOptionDescriptions(Console.Out);

    Information("Available tasks");
    foreach (var task in Tasks) {
        Information(" * " + task.Name + ":");
        Information("     " + task.Description);
    }
});

Task("CheckForChanges")
.Description("Determines if the most recent commit affected the target path with the repo.")
.IsDependentOn("Prerequisite-GitHubIntegration")
.Does(async () => {
    // Get target repo commits from GitHub.
    var repoId = (await gitHubClient.Repository.Get(gitHubRepoOwner, gitHubRepoName)).Id;
    // var latestCommitHash = (await gitHubClient.Git.Tree.Get(repoId, gitHubRepoBranchName)).Sha;
    // TODO: Grab from last known commit X to latest so we aren't limited to previous commit.
    var commits = await gitHubReposClient.Commit.GetAll(gitHubRepoOwner, gitHubRepoName, new Octokit.ApiOptions { PageSize = 1, PageCount = 2, });
    // Grab latest commit to repo.
    var lastCommits = commits.Reverse().Take(2);
    var priorKnownCommit = lastCommits.Last();
    var mostRecentCommit = lastCommits.First();
    // Determine list of changes in latest commit(s).
    var changes = await gitHubReposClient.Commit.Compare(repoId, priorKnownCommit.Sha, mostRecentCommit.Sha);
    // Determine if watched path(s) are in the list of changes.
    var changesAffectingWatchedRepoPaths = changes.Files.Where(f => gitRepoPaths.Any(path => f.Filename.StartsWith(path)));

    foreach (var path in changesAffectingWatchedRepoPaths) {
        Information(path);
    }
    // TODO: Set up something for VSTS to see to indicate a change was found
});

Task("Prerequisite-GitHubIntegration")
.Description("Validates any required GitHub settings and establishes the various Octokit clients.")
.Does(() => {
    // Validate GitHub inputs (env or command line) required for GitHub access.
    if (string.IsNullOrEmpty(gitHubAccessToken)) {
        Error("To access GitHub, you must provide a GitHub access token using either environment variables or command-line options.");
        throw new Exception("GitHub token not present.");
    }

    gitHubClient = new Octokit.GitHubClient(new Octokit.ProductHeaderValue("commit-tracker")) {
        Credentials = new Octokit.Credentials(gitHubAccessToken),
    };
    gitHubReposClient = new Octokit.RepositoriesClient(new Octokit.ApiConnection(gitHubClient.Connection));
});

Task("Default")
.Description("The default task run, if a target is not specified.")
.IsDependentOn("Debug-InfoDump");

RunTarget(target);
