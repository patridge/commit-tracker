using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Mono.Options;

//#addin nuget:?package=Cake.ArgumentHelpers
//#addin nuget:?package=Mono.Options
//#addin nuget:?package=Octokit
//// #addin nuget:?package=Cake.Git

//// using LibGit2Sharp;

namespace CommitTracker
{
    class Program
    {
        const string environmentVariablePrefix = "CommitTracker_";
        const string gitHubAccessTokenVariableKey = "GitHubAccessToken";

        static void Information(string output)
        {
            Console.Out.WriteLine(output);
        }
        static void Error(string output)
        {
            Console.Error.WriteLine(output);
        }
        static string ArgumentOrEnvironmentVariable(string key, string environmentPrefix, string fallback)
        {
            var environmentValue = Environment.GetEnvironmentVariable(key);
            if (string.IsNullOrEmpty(environmentValue))
            {
                environmentValue = Environment.GetEnvironmentVariable(environmentPrefix + key);
            }

            return environmentValue;
        }

        public static async Task Main(string[] args)
        {
            var gitHubAccessToken = ArgumentOrEnvironmentVariable("GitHubAccessToken", environmentVariablePrefix, null);
            var gitHubRepoToWatch = ArgumentOrEnvironmentVariable("GitHubWatchRepo", environmentVariablePrefix, null);
            Octokit.GitHubClient gitHubClient;
            Octokit.RepositoriesClient gitHubReposClient;
            string gitHubRepoName = null;
            string gitHubRepoOwner = null;
            string gitHubRepoBranchName = "master";
            string[] gitRepoPaths = new string[0];
            string gitRepoLastCommitSha = null;

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
                { "repoLastKnownCommitSha=", "Last commit within repo since checking for chnages", lastCommit => gitRepoLastCommitSha = lastCommit },
            };

            //// NOTE: Skip two args because they are Cake script items (e.g., `/…/Cake.exe` and `build.cake`), which screws up Mono.Options because a console app's args do not include it.
            //var args = System.Environment.GetCommandLineArgs().Skip(2).ToArray();
            List<string> extraArgs;
            try
            {
                // parse the command line
                extraArgs = options.Parse(args);
                // NOTE: extraArgs will usually contain various Cake items, so probably not worth using for too much error logic.
                // e.g., macOS run: "/Users/someuser/Project/xamu-scripts/tools/Cake/Cake.exe", "build.cake" }
            }
            catch (OptionException e)
            {
                Error("Error parsing inputs: " + e.Message);
                return;
            }

            gitHubClient = new Octokit.GitHubClient(new Octokit.ProductHeaderValue("submodule-helper"))
            {
                Credentials = new Octokit.Credentials(gitHubAccessToken),
            };
            var apiConnection = new Octokit.ApiConnection(gitHubClient.Connection);
            var repoClient = new Octokit.RepositoriesClient(apiConnection);
            // var forksClient = new Octokit.RepositoryForksClient(apiConnection);
            var referencesClient = new Octokit.ReferencesClient(apiConnection);
            // var pullRequestsClient = new Octokit.PullRequestsClient(apiConnection);
            var commitsClient = new Octokit.CommitsClient(apiConnection);

            if (gitHubRepoOwner == null)
            {
                var currentUser = await gitHubClient.User.Current();
                gitHubRepoOwner = currentUser.Login;
            }

        //var target = Argument("target", "Default");

            //Task("Prerequisite-GitHubIntegration")
        //.Description("Validates any required GitHub settings and establishes the various Octokit clients.")
        //.Does(() => {
            // Validate GitHub inputs (env or command line) required for GitHub access.
            if (string.IsNullOrEmpty(gitHubAccessToken))
            {
                Error("To access GitHub, you must provide a GitHub access token using either environment variables or command-line options.");
                throw new Exception("GitHub token not present.");
            }

            gitHubClient = new Octokit.GitHubClient(new Octokit.ProductHeaderValue("commit-tracker"))
            {
                Credentials = new Octokit.Credentials(gitHubAccessToken),
            };
            gitHubReposClient = new Octokit.RepositoriesClient(new Octokit.ApiConnection(gitHubClient.Connection));
        //});

        //// Display resulting argument values for debugging.
        //Task("Debug-InfoDump")
        //.Description("Outputs the values found in the various supplied command-line and environment variables. Also outputs the command line help system.")
        //.Does(() => {
            if (string.IsNullOrWhiteSpace(gitHubRepoOwner) || string.IsNullOrWhiteSpace(gitHubRepoName) || string.IsNullOrWhiteSpace(gitHubRepoBranchName))
            {
                Information($"{nameof(gitHubAccessToken)}: {gitHubAccessToken}");
                Information($"{nameof(gitHubRepoOwner)}/{nameof(gitHubRepoName)}: {gitHubRepoOwner}/{gitHubRepoName}");
                Information($"{nameof(gitHubRepoBranchName)}: {gitHubRepoBranchName}");
                Information($"{nameof(gitRepoPaths)}: {string.Join(", ", gitRepoPaths)}");
                Information($"{nameof(gitRepoLastCommitSha)}: {gitRepoLastCommitSha}");

                // output the options
                Information("");
                Information("Command-line options:");
                options.WriteOptionDescriptions(Console.Out);

                //Information("Available tasks");
                //foreach (var task in Tasks)
                //{
                //    Information(" * " + task.Name + ":");
                //    Information("     " + task.Description);
                //}
            }
        //});

        //Task("CheckForChanges")
        //.Description("Determines if the most recent commit affected the target path with the repo.")
        //.IsDependentOn("Prerequisite-GitHubIntegration")
        //.Does(async () => {
            // Get target repo commits from GitHub.
            var repoId = (await gitHubClient.Repository.Get(gitHubRepoOwner, gitHubRepoName)).Id;
            // var latestCommitHash = (await gitHubClient.Git.Tree.Get(repoId, gitHubRepoBranchName)).Sha;
            // TODO: Grab from last known commit X to latest so we aren't limited to previous commit.
            var commits = await gitHubReposClient.Commit.GetAll(gitHubRepoOwner, gitHubRepoName, new Octokit.ApiOptions { PageSize = 1, PageCount = 2, });
            // Grab latest commit to repo.
            var lastCommits = commits.Take(2);
            var priorKnownCommit = gitRepoLastCommitSha ?? lastCommits.Last().Sha;
            var mostRecentCommit = lastCommits.First().Sha;
            // Determine list of changes in latest commit(s).
            var changes = await gitHubReposClient.Commit.Compare(repoId, priorKnownCommit, mostRecentCommit);
            // Determine if watched path(s) are in the list of changes.
            var pathsAndFilesChanged = gitRepoPaths.Select(path =>
            {
                List<string> filesChanged = changes.Files.Where(f => f.Filename.StartsWith(path)).Select(file => file.Filename).ToList();
                return (Path: path, FilesChanged: filesChanged);
            }).Where(pathAndFilesChanged => pathAndFilesChanged.FilesChanged.Any());

            if (pathsAndFilesChanged.Any())
            {
                Information($"Relevant changes found: {priorKnownCommit.Substring(0, 5)}..{mostRecentCommit.Substring(0, 5)}");
                foreach (var pathAndFilesChanged in pathsAndFilesChanged)
                {
                    Information($"{pathAndFilesChanged.Path}");
                    foreach (var fileChanged in pathAndFilesChanged.FilesChanged)
                    {
                        Information($"* {fileChanged}");
                    }
                }
            }
            // TODO: Set up something for VSTS to see to indicate a change was found
        //});

        //Task("Default")
        //.Description("The default task run, if a target is not specified.")
        //.IsDependentOn("Debug-InfoDump");

        //RunTarget(target);

            Console.Read();
        }
    }
}
