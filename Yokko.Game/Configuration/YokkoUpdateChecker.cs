using System;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Yokko.Game.Configuration;

internal readonly record struct YokkoUpdateCheckResult(
    bool Success,
    bool UpdateAvailable,
    string LatestVersion,
    string ReleaseUrl,
    string Message);

/// <summary>
/// Checks GitHub releases for a newer Yokko build.
/// </summary>
internal static class YokkoUpdateChecker
{
    private const string releases_api =
        "https://api.github.com/repos/Moekotori/yokko/releases/latest";

    internal static async Task<YokkoUpdateCheckResult> CheckAsync(
        CancellationToken cancellationToken = default)
    {
        string current = Assembly.GetEntryAssembly()?
                                 .GetName().Version?.ToString()
                         ?? "0.0.0";

        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("YokkoSettings/1.0");
            using HttpResponseMessage response = await client
                .GetAsync(releases_api, cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            string json = await response.Content
                .ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            string tag = root.GetProperty("tag_name").GetString()?
                             .TrimStart('v', 'V')
                         ?? string.Empty;
            string url = root.GetProperty("html_url").GetString()
                         ?? "https://github.com/Moekotori/yokko/releases";

            bool updateAvailable = compareVersions(tag, current) > 0;
            return new YokkoUpdateCheckResult(
                true,
                updateAvailable,
                tag,
                url,
                updateAvailable
                    ? $"Update available: v{tag} (current v{current})"
                    : $"You are up to date (v{current}).");
        }
        catch (Exception exception)
        {
            return new YokkoUpdateCheckResult(
                false,
                false,
                current,
                "https://github.com/Moekotori/yokko/releases",
                exception.Message);
        }
    }

    private static int compareVersions(string left, string right)
    {
        static int[] parts(string value) =>
            (value ?? "0").Split('.', '-', '+')
                          .Select(part => int.TryParse(part, out int number) ? number : 0)
                          .ToArray();

        int[] a = parts(left);
        int[] b = parts(right);
        int length = Math.Max(a.Length, b.Length);
        for (int i = 0; i < length; i++)
        {
            int av = i < a.Length ? a[i] : 0;
            int bv = i < b.Length ? b[i] : 0;
            if (av != bv)
                return av.CompareTo(bv);
        }

        return 0;
    }
}
