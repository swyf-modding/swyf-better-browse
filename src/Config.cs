using BepInEx.Configuration;
using BepInEx.Logging;
using ScamWYF.Modding.Core;

namespace ScamWYF.BetterBrowse
{
    /// <summary>
    /// The settings, and the derived plan the Harmony patches read on every navigation.
    /// </summary>
    /// <remarks>
    /// The plan is rebuilt whenever the .cfg is re-read. The patches then only read one immutable
    /// object instead of re-parsing settings on the address bar's path, and a hand edit takes effect
    /// on the next search without reinstalling anything.
    /// </remarks>
    internal sealed class Config
    {
        public readonly ConfigEntry<bool> Enabled;
        public readonly ConfigEntry<bool> Debug;
        public readonly ConfigEntry<SearchProvider> Provider;
        public readonly ConfigEntry<string> CustomSearchUrl;
        public readonly ConfigEntry<string> SearxInstance;
        public readonly ConfigEntry<HomePageMode> HomePage;
        public readonly ConfigEntry<string> CustomHomePageUrl;

        private readonly ModSettings _settings;
        private volatile SearchEnginePlan _plan;

        public Config(ModSettings settings)
        {
            _settings = settings;

            Enabled = settings.Bind("3 - General", "Enabled", true,
                "Set false to leave the in-game browser exactly as the game ships it.\n" +
                "Takes effect on the next search; no restart needed.");

            Debug = settings.Bind("3 - General", "Debug", false,
                "Log every rewritten search to BepInEx/LogOutput.log.");

            Provider = settings.BindChoice("1 - Search", "Provider", SearchProvider.Brave,
                new[]
                {
                    SearchProvider.Brave, SearchProvider.DuckDuckGo, SearchProvider.Ecosia,
                    SearchProvider.Bing, SearchProvider.Startpage, SearchProvider.Yahoo,
                    SearchProvider.Mojeek, SearchProvider.Qwant, SearchProvider.Kagi,
                    SearchProvider.SearXNG, SearchProvider.Yandex, SearchProvider.Baidu,
                    SearchProvider.Google, SearchProvider.Custom
                },
                "Which search engine the browser sends plain-text searches to.\n" +
                "Brave, DuckDuckGo, Ecosia, Bing, Startpage, Yahoo, Mojeek, Qwant, Kagi,\n" +
                "SearXNG, Yandex, Baidu, Google, or Custom.\n" +
                "Text in the address bar that is not a domain or URL becomes a search on this\n" +
                "engine. A real URL is still opened unchanged.");

            CustomSearchUrl = settings.Bind("1 - Search", "CustomSearchUrl", "",
                "Used when Provider = Custom. Put {query} where the search terms go.\n" +
                "Example: https://search.marginalia.nu/search?query={query}\n" +
                "If {query} is missing, ?q={query} is appended. A value that is not an http(s)\n" +
                "URL falls back to Brave.");

            SearxInstance = settings.Bind("1 - Search", "SearxInstance", "https://searx.be",
                "Base URL of the SearXNG instance used when Provider = SearXNG.\n" +
                "Example: https://searx.be or https://search.example.org\n" +
                "Without a scheme, https:// is assumed. The instance must be reachable from\n" +
                "this machine and must accept a /search?q= query.");

            HomePage = settings.BindChoice("2 - Home", "HomePage", HomePageMode.SearchEngine,
                new[] { HomePageMode.SearchEngine, HomePageMode.Custom, HomePageMode.GameDefault },
                "What the browser opens as its home page, and where the toolbar's magnifier\n" +
                "button goes.\n" +
                "SearchEngine = the home page of the selected provider.\n" +
                "Custom       = CustomHomePageUrl.\n" +
                "GameDefault  = leave the game's own Google home page alone. Searches are still\n" +
                "               redirected.");

            CustomHomePageUrl = settings.Bind("2 - Home", "CustomHomePageUrl", "",
                "Used when HomePage = Custom. Must be an http(s) URL.\n" +
                "Blank or invalid falls back to the selected provider's home page.");

            Rebuild();
        }

        /// <summary>The immutable plan the patches read. Never null after construction.</summary>
        public SearchEnginePlan Plan
        {
            get { return _plan; }
        }

        /// <summary>Where this mod's .cfg lives, for the menu's help text.</summary>
        public string ConfigFilePath
        {
            get { return _settings.FilePath; }
        }

        /// <summary>Re-read every setting and derive the URL shapes from them.</summary>
        public void Rebuild()
        {
            var enabled = _settings.Bool(Enabled, true);
            var debug = _settings.Bool(Debug, false);
            var provider = _settings.EnumValue(Provider, SearchProvider.Brave);
            var homeMode = _settings.EnumValue(HomePage, HomePageMode.SearchEngine);
            var customSearch = _settings.Text(CustomSearchUrl, "");
            var searx = _settings.Text(SearxInstance, "https://searx.be");
            var customHome = _settings.Text(CustomHomePageUrl, "");

            _plan = SearchEngines.Build(enabled, debug, provider, homeMode, customSearch, searx, customHome);
        }

        /// <summary>
        /// Log what is in effect after a load or reload, so "it did nothing" has an answer in the log.
        /// </summary>
        public void Describe(ManualLogSource log)
        {
            var plan = _plan;
            if (log == null || plan == null) return;

            if (!plan.Enabled)
            {
                log.LogInfo("Disabled; the browser is using Google exactly as the game ships it.");
                return;
            }

            log.LogInfo("Search engine is " + plan.ProviderName + " (" + plan.SearchTemplate + ").");
            log.LogInfo(plan.ReplacesHome
                ? "Browser home page is " + plan.HomeUrl + "."
                : "Browser home page is left as the game's own.");
            if (!string.IsNullOrEmpty(plan.Warning)) log.LogWarning(plan.Warning);
        }
    }
}
