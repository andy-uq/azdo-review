using System.Diagnostics;
using System.Text.RegularExpressions;

namespace AzdoPr;

public sealed record AzdoConfig(string Pat, string Org, string Project, string Repo);

public static partial class ConfigResolver
{
    public static AzdoConfig Resolve(string? org, string? project, string? repo)
    {
        var pat = Environment.GetEnvironmentVariable("AZDO_PAT")
            ?? throw new InvalidOperationException("AZDO_PAT environment variable is required");

        org ??= Environment.GetEnvironmentVariable("AZDO_ORG");
        project ??= Environment.GetEnvironmentVariable("AZDO_PROJECT");

        if (org is null || project is null || repo is null)
        {
            var (detectedOrg, detectedProject, detectedRepo) = DetectFromGitRemote();
            org ??= detectedOrg;
            project ??= detectedProject;
            repo ??= detectedRepo;
        }

        if (org is null) throw new InvalidOperationException("Could not determine org. Use --org or AZDO_ORG env var.");
        if (project is null) throw new InvalidOperationException("Could not determine project. Use --project or AZDO_PROJECT env var.");
        if (repo is null) throw new InvalidOperationException("Could not determine repo. Use --repo flag.");

        return new AzdoConfig(pat, org, project, repo);
    }

    private static (string? Org, string? Project, string? Repo) DetectFromGitRemote()
    {
        try
        {
            var psi = new ProcessStartInfo("git", "remote get-url origin")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            if (process is null) return (null, null, null);

            var url = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();

            // Match: https://dev.azure.com/{org}/{project}/_git/{repo}
            // or:    https://{org}@dev.azure.com/{org}/{project}/_git/{repo}
            var match = AzDoRemotePattern().Match(url);
            if (match.Success)
            {
                return (match.Groups["org"].Value, match.Groups["project"].Value, match.Groups["repo"].Value);
            }

            // Match SSH: git@ssh.dev.azure.com:v3/{org}/{project}/{repo}
            match = AzDoSshPattern().Match(url);
            if (match.Success)
            {
                return (match.Groups["org"].Value, match.Groups["project"].Value, match.Groups["repo"].Value);
            }

            // Match old format: https://{org}.visualstudio.com/{project}/_git/{repo}
            // or: https://{org}@{org}.visualstudio.com/{project}/_git/{repo}
            match = VisualStudioPattern().Match(url);
            if (match.Success)
            {
                return (match.Groups["org"].Value, match.Groups["project"].Value, match.Groups["repo"].Value);
            }
        }
        catch
        {
            // git not available or not a git repo
        }

        return (null, null, null);
    }

    [GeneratedRegex(@"dev\.azure\.com/(?<org>[^/]+)/(?<project>[^/]+)/_git/(?<repo>[^/\s]+)")]
    private static partial Regex AzDoRemotePattern();

    [GeneratedRegex(@"ssh\.dev\.azure\.com:v3/(?<org>[^/]+)/(?<project>[^/]+)/(?<repo>[^/\s]+)")]
    private static partial Regex AzDoSshPattern();

    [GeneratedRegex(@"(?<org>[^/@]+)\.visualstudio\.com/(?<project>[^/]+)/_git/(?<repo>[^/\s]+)")]
    private static partial Regex VisualStudioPattern();
}
