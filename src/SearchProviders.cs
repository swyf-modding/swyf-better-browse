namespace ScamWYF.BetterBrowse
{
    /// <summary>Search engines with a built-in URL shape.</summary>
    public enum SearchProvider
    {
        Brave,
        DuckDuckGo,
        Ecosia,
        Bing,
        Startpage,
        Yahoo,
        Mojeek,
        Qwant,
        Kagi,
        SearXNG,
        Yandex,
        Baidu,
        Google,

        /// <summary>Use the custom search URL setting, which must contain {query}.</summary>
        Custom
    }

    /// <summary>What the browser opens on startup and with its magnifier button.</summary>
    public enum HomePageMode
    {
        /// <summary>The home page of the selected search engine.</summary>
        SearchEngine,

        /// <summary>Use the custom home page URL setting.</summary>
        Custom,

        /// <summary>Leave the game's own Google home page alone.</summary>
        GameDefault
    }
}
