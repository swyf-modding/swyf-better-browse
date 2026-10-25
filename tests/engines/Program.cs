using System;
using ScamWYF.BetterBrowse;

namespace ScamWYF.BetterBrowse.Tests
{
    /// <summary>
    /// The URL arithmetic, with no game and no Unity: every provider, the custom template, the home
    /// modes and the Google-search recogniser. Run with:
    ///
    ///     dotnet run --project tests/engines/Engines.csproj -c Release
    /// </summary>
    internal static class Program
    {
        private static int _failed;

        private static void Check(string name, object actual, object expected)
        {
            var ok = Equals(actual, expected);
            if (!ok) _failed++;
            Console.WriteLine((ok ? "PASS " : "FAIL ") + name);
            if (!ok)
            {
                Console.WriteLine("  expected: " + expected);
                Console.WriteLine("  actual:   " + actual);
            }
        }

        private static void Main()
        {
            BuiltInProviders();
            CustomProvider();
            HomeModes();
            GoogleUrls();
            Disabled();

            Console.WriteLine(_failed == 0
                ? "\nAll checks passed."
                : "\n" + _failed + " check(s) failed.");
            Environment.Exit(_failed == 0 ? 0 : 1);
        }

        private static void BuiltInProviders()
        {
            var brave = SearchEngines.Build(true, false, SearchProvider.Brave,
                HomePageMode.SearchEngine, "", "", "");
            Check("brave home", brave.HomeUrl, "https://search.brave.com/");
            Check("brave search", brave.BuildSearchUrl("hello+world"),
                "https://search.brave.com/search?q=hello+world");

            var ddg = SearchEngines.Build(true, false, SearchProvider.DuckDuckGo,
                HomePageMode.SearchEngine, "", "", "");
            Check("ddg search", ddg.BuildSearchUrl("hello+world"),
                "https://duckduckgo.com/?q=hello+world");
            Check("ddg host", ddg.SearchHost, "duckduckgo.com");
            Check("ddg owns host", ddg.OwnsHost("duckduckgo.com"), true);
            Check("ddg owns subdomain", ddg.OwnsHost("html.duckduckgo.com"), true);
            Check("ddg rejects suffix trick", ddg.OwnsHost("duckduckgo.com.evil.test"), false);

            var searx = SearchEngines.Build(true, false, SearchProvider.SearXNG,
                HomePageMode.SearchEngine, "", "searx.example.org", "");
            Check("searx home", searx.HomeUrl, "https://searx.example.org/");
            Check("searx search", searx.BuildSearchUrl("cats"),
                "https://searx.example.org/search?q=cats");
            Check("searx host", searx.SearchHost, "searx.example.org");

            var bing = SearchEngines.Build(true, false, SearchProvider.Bing,
                HomePageMode.SearchEngine, "", "", "");
            Check("bing search", bing.BuildSearchUrl("cats"),
                "https://www.bing.com/search?q=cats");

            var startpage = SearchEngines.Build(true, false, SearchProvider.Startpage,
                HomePageMode.SearchEngine, "", "", "");
            Check("startpage search", startpage.BuildSearchUrl("cats"),
                "https://www.startpage.com/sp/search?query=cats");

            var yandex = SearchEngines.Build(true, false, SearchProvider.Yandex,
                HomePageMode.SearchEngine, "", "", "");
            Check("yandex search", yandex.BuildSearchUrl("cats"),
                "https://yandex.com/search/?text=cats");
        }

        private static void CustomProvider()
        {
            var custom = SearchEngines.Build(true, false, SearchProvider.Custom,
                HomePageMode.SearchEngine,
                "https://search.marginalia.nu/search?query={query}", "", "");
            Check("custom search", custom.BuildSearchUrl("cats"),
                "https://search.marginalia.nu/search?query=cats");
            Check("custom home", custom.HomeUrl, "https://search.marginalia.nu/");
            Check("custom no warning", custom.Warning, null);

            var missingQuery = SearchEngines.Build(true, false, SearchProvider.Custom,
                HomePageMode.SearchEngine, "https://example.org/find", "", "");
            Check("custom missing query appended", missingQuery.BuildSearchUrl("cats"),
                "https://example.org/find?q=cats");
            Check("custom missing query warns", missingQuery.Warning != null, true);

            var invalid = SearchEngines.Build(true, false, SearchProvider.Custom,
                HomePageMode.SearchEngine, "not a url", "", "");
            Check("custom invalid falls back to brave", invalid.BuildSearchUrl("cats"),
                "https://search.brave.com/search?q=cats");
            Check("custom invalid warns", invalid.Warning != null, true);

            var empty = SearchEngines.Build(true, false, SearchProvider.Custom,
                HomePageMode.SearchEngine, "", "", "");
            Check("custom empty falls back to brave", empty.BuildSearchUrl("cats"),
                "https://search.brave.com/search?q=cats");
        }

        private static void HomeModes()
        {
            var engineHome = SearchEngines.Build(true, false, SearchProvider.Brave,
                HomePageMode.SearchEngine, "", "", "");
            Check("engine home replaces", engineHome.ReplacesHome, true);

            var customHome = SearchEngines.Build(true, false, SearchProvider.Brave,
                HomePageMode.Custom, "", "", "example.com/start");
            Check("custom home normalized", customHome.HomeUrl, "https://example.com/start");

            var badCustomHome = SearchEngines.Build(true, false, SearchProvider.Brave,
                HomePageMode.Custom, "", "", "not a url");
            Check("invalid custom home falls back", badCustomHome.HomeUrl,
                "https://search.brave.com/");
            Check("invalid custom home warns", badCustomHome.Warning != null, true);

            var gameHome = SearchEngines.Build(true, false, SearchProvider.DuckDuckGo,
                HomePageMode.GameDefault, "", "", "");
            Check("game home stays google", gameHome.HomeUrl, "https://www.google.com/");
            Check("game home not replaced", gameHome.ReplacesHome, false);
            Check("game home still searches", gameHome.BuildSearchUrl("cats"),
                "https://duckduckgo.com/?q=cats");
        }

        private static void GoogleUrls()
        {
            string query;
            Check("parse google query",
                SearchEngines.TryGetGoogleQuery("https://www.google.com/search?q=hello+world&oq=hello",
                    out query) && query == "hello+world",
                true);
            Check("parse google query without extra params",
                SearchEngines.TryGetGoogleQuery("https://www.google.com/search?q=cats", out query) &&
                query == "cats",
                true);
            Check("parse google query rejects other host",
                SearchEngines.TryGetGoogleQuery("https://example.com/search?q=hello", out query), false);
            Check("parse google query rejects host trick",
                SearchEngines.TryGetGoogleQuery("https://www.google.com.evil.test/search?q=hello",
                    out query), false);
            Check("parse google query rejects empty",
                SearchEngines.TryGetGoogleQuery("https://www.google.com/search?q=", out query), false);

            Check("google home", SearchEngines.IsGoogleHome("https://www.google.com/"), true);
            Check("google home without slash", SearchEngines.IsGoogleHome("https://www.google.com"), true);
            Check("google home is not a result page",
                SearchEngines.IsGoogleHome("https://www.google.com/search?q=x"), false);
            Check("google home rejects lookalike",
                SearchEngines.IsGoogleHome("https://google.com.evil.test/"), false);

            string home;
            Check("derive home keeps the port",
                SearchEngines.TryDeriveHome("https://search.example.org:8443/path?q={query}", out home) &&
                home == "https://search.example.org:8443/",
                true);
        }

        private static void Disabled()
        {
            var disabled = SearchEngines.Build(false, false, SearchProvider.Brave,
                HomePageMode.SearchEngine, "", "", "");
            Check("disabled is off", disabled.Enabled, false);
            Check("disabled does not replace home", disabled.ReplacesHome, false);
        }
    }
}
