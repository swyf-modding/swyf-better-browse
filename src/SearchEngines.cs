using System;

namespace ScamWYF.BetterBrowse
{
    /// <summary>
    /// One settled set of URLs: where searches go, where home goes, and how to recognise a Google
    /// search the game has already built.
    /// </summary>
    internal sealed class SearchEnginePlan
    {
        public readonly bool Enabled;
        public readonly bool Debug;
        public readonly SearchProvider Provider;
        public readonly HomePageMode HomeMode;
        public readonly string ProviderName;
        public readonly string SearchTemplate;
        public readonly string HomeUrl;
        public readonly string SearchHost;
        public readonly string Warning;

        public SearchEnginePlan(bool enabled, bool debug, SearchProvider provider,
            HomePageMode homeMode, string providerName, string searchTemplate, string homeUrl,
            string warning)
        {
            Enabled = enabled;
            Debug = debug;
            Provider = provider;
            HomeMode = homeMode;
            ProviderName = providerName;
            SearchTemplate = searchTemplate;
            HomeUrl = homeUrl;
            Warning = warning;
            SearchHost = SearchEngines.HostOf(searchTemplate);
        }

        /// <summary>
        /// Whether the home page should be replaced. Search redirection does not depend on this:
        /// HomePage = GameDefault keeps Google as the home page but still searches elsewhere.
        /// </summary>
        public bool ReplacesHome
        {
            get
            {
                return Enabled && HomeMode != HomePageMode.GameDefault &&
                       !string.IsNullOrEmpty(HomeUrl);
            }
        }

        /// <summary>Build the engine's URL from an already escaped query.</summary>
        public string BuildSearchUrl(string escapedQuery)
        {
            return SearchTemplate.Replace("{query}", escapedQuery ?? "");
        }

        /// <summary>
        /// True when <paramref name="host"/> is this engine's own host, so the game's review
        /// bookkeeping can treat a search as a search rather than as a visit to a website.
        /// </summary>
        public bool OwnsHost(string host)
        {
            if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(SearchHost)) return false;
            var normalized = host.Trim().TrimEnd('.').ToLowerInvariant();
            return normalized == SearchHost || normalized.EndsWith("." + SearchHost);
        }
    }

    /// <summary>
    /// The URL catalogue. Everything here is deliberately pure so the URL arithmetic can be read
    /// and tested without the game running; see tests/engines.
    /// </summary>
    internal static class SearchEngines
    {
        private const string BraveSearch = "https://search.brave.com/search?q={query}";
        private const string BraveHome = "https://search.brave.com/";
        private const string GoogleSearch = "https://www.google.com/search?q={query}";
        private const string GoogleHome = "https://www.google.com/";

        public static SearchEnginePlan Build(bool enabled, bool debug, SearchProvider provider,
            HomePageMode homeMode, string customSearchUrl, string searxInstance, string customHomeUrl)
        {
            string providerHome;
            string warning;
            var search = TemplateFor(provider, customSearchUrl, searxInstance, out providerHome, out warning);

            var home = providerHome;
            if (homeMode == HomePageMode.GameDefault)
            {
                home = GoogleHome;
            }
            else if (homeMode == HomePageMode.Custom)
            {
                string parsed;
                if (TryNormalizeHttpUrl(customHomeUrl, out parsed))
                {
                    home = parsed;
                }
                else
                {
                    if (!string.IsNullOrEmpty((customHomeUrl ?? "").Trim()))
                    {
                        warning = Append(warning, "CustomHomePageUrl is not a usable http(s) URL; " +
                                                   "using " + providerHome + ".");
                    }
                    home = providerHome;
                }
            }

            if (!IsUsableHttpUrl(home))
            {
                warning = Append(warning, "The chosen home page is not a usable http(s) URL; " +
                                           "using the game's own.");
                home = GoogleHome;
            }

            return new SearchEnginePlan(enabled, debug, provider, homeMode, provider.ToString(),
                search, home, warning);
        }

        // ------------------------------------------------------------------ search URLs

        private static string TemplateFor(SearchProvider provider, string customSearchUrl,
            string searxInstance, out string homeUrl, out string warning)
        {
            warning = null;

            switch (provider)
            {
                case SearchProvider.DuckDuckGo:
                    homeUrl = "https://duckduckgo.com/";
                    return "https://duckduckgo.com/?q={query}";

                case SearchProvider.Ecosia:
                    homeUrl = "https://www.ecosia.org/";
                    return "https://www.ecosia.org/search?q={query}";

                case SearchProvider.Bing:
                    homeUrl = "https://www.bing.com/";
                    return "https://www.bing.com/search?q={query}";

                case SearchProvider.Startpage:
                    homeUrl = "https://www.startpage.com/";
                    return "https://www.startpage.com/sp/search?query={query}";

                case SearchProvider.Yahoo:
                    homeUrl = "https://search.yahoo.com/";
                    return "https://search.yahoo.com/search?p={query}";

                case SearchProvider.Mojeek:
                    homeUrl = "https://www.mojeek.com/";
                    return "https://www.mojeek.com/search?q={query}";

                case SearchProvider.Qwant:
                    homeUrl = "https://www.qwant.com/";
                    return "https://www.qwant.com/?q={query}";

                case SearchProvider.Kagi:
                    homeUrl = "https://kagi.com/";
                    return "https://kagi.com/search?q={query}";

                case SearchProvider.SearXNG:
                {
                    var instance = NormalizeInstance(searxInstance, out warning);
                    homeUrl = instance + "/";
                    return instance + "/search?q={query}";
                }

                case SearchProvider.Yandex:
                    homeUrl = "https://yandex.com/";
                    return "https://yandex.com/search/?text={query}";

                case SearchProvider.Baidu:
                    homeUrl = "https://www.baidu.com/";
                    return "https://www.baidu.com/s?wd={query}";

                case SearchProvider.Google:
                    homeUrl = GoogleHome;
                    return GoogleSearch;

                case SearchProvider.Custom:
                {
                    var custom = (customSearchUrl ?? "").Trim();
                    if (custom.Length == 0)
                    {
                        warning = "Provider is Custom but CustomSearchUrl is empty; using Brave.";
                        homeUrl = BraveHome;
                        return BraveSearch;
                    }

                    if (custom.IndexOf("{query}", StringComparison.Ordinal) < 0)
                    {
                        custom = custom + (custom.IndexOf('?') >= 0 ? "&q={query}" : "?q={query}");
                        warning = "CustomSearchUrl had no {query}; appended q={query}.";
                    }

                    if (!IsUsableHttpUrl(custom.Replace("{query}", "test")))
                    {
                        warning = Append(warning, "CustomSearchUrl is not a usable http(s) URL; " +
                                                   "using Brave.");
                        homeUrl = BraveHome;
                        return BraveSearch;
                    }

                    string derived;
                    homeUrl = TryDeriveHome(custom, out derived) ? derived : BraveHome;
                    return custom;
                }

                default:
                    homeUrl = BraveHome;
                    return BraveSearch;
            }
        }

        private static string NormalizeInstance(string instance, out string warning)
        {
            warning = null;
            var value = (instance ?? "").Trim();
            if (value.Length == 0) return "https://searx.be";

            string parsed;
            if (TryNormalizeHttpUrl(value, out parsed))
            {
                return parsed.TrimEnd('/');
            }

            warning = "SearxInstance is not a usable http(s) URL; using https://searx.be.";
            return "https://searx.be";
        }

        // ------------------------------------------------------------------ URL helpers

        /// <summary>
        /// The home page implied by a search URL: scheme, host and port, with nothing after them.
        /// </summary>
        public static bool TryDeriveHome(string searchTemplate, out string home)
        {
            home = null;
            if (string.IsNullOrEmpty(searchTemplate)) return false;

            Uri uri;
            if (!Uri.TryCreate(searchTemplate, UriKind.Absolute, out uri)) return false;
            if (!IsHttp(uri)) return false;

            home = uri.Scheme + "://" + uri.Authority + "/";
            return true;
        }

        /// <summary>The host of a URL template, for comparing against a host the game reports.</summary>
        public static string HostOf(string urlTemplate)
        {
            if (string.IsNullOrEmpty(urlTemplate)) return null;

            Uri uri;
            if (!Uri.TryCreate(urlTemplate.Replace("{query}", "test"), UriKind.Absolute, out uri))
                return null;

            return uri.Host.TrimEnd('.').ToLowerInvariant();
        }

        /// <summary>
        /// Whether this is a Google search URL the game built for plain text. Handles the game's
        /// exact shape and any other google.com/search?q= URL, and returns the query already escaped.
        /// </summary>
        public static bool TryGetGoogleQuery(string url, out string query)
        {
            query = null;
            if (string.IsNullOrEmpty(url)) return false;

            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri)) return false;
            if (!IsHttp(uri)) return false;

            var host = uri.Host.TrimEnd('.').ToLowerInvariant();
            if (host != "google.com" && !host.EndsWith(".google.com", StringComparison.Ordinal))
                return false;

            if (uri.AbsolutePath.IndexOf("/search", StringComparison.OrdinalIgnoreCase) < 0)
                return false;

            var raw = uri.Query;
            if (raw.Length > 1) raw = raw.Substring(1);

            var parts = raw.Split('&');
            for (var i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                var equals = part.IndexOf('=');
                var key = equals >= 0 ? part.Substring(0, equals) : part;
                if (!string.Equals(key, "q", StringComparison.OrdinalIgnoreCase)) continue;

                var value = equals >= 0 ? part.Substring(equals + 1) : "";
                if (value.Length == 0) return false;
                query = value;
                return true;
            }

            return false;
        }

        public static bool IsGoogleHome(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;

            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri)) return false;
            if (!IsHttp(uri)) return false;

            var host = uri.Host.TrimEnd('.').ToLowerInvariant();
            if (host != "google.com" && host != "www.google.com") return false;

            return uri.AbsolutePath == "/" || uri.AbsolutePath.Length == 0;
        }

        private static bool IsUsableHttpUrl(string url)
        {
            string normalized;
            return TryNormalizeHttpUrl(url, out normalized);
        }

        private static bool TryNormalizeHttpUrl(string value, out string normalized)
        {
            normalized = null;
            var text = (value ?? "").Trim();
            if (text.Length == 0) return false;

            if (text.IndexOf("://", StringComparison.Ordinal) < 0) text = "https://" + text;

            Uri uri;
            if (!Uri.TryCreate(text, UriKind.Absolute, out uri)) return false;
            if (!IsHttp(uri)) return false;
            if (string.IsNullOrEmpty(uri.Host)) return false;

            normalized = uri.AbsoluteUri;
            return true;
        }

        private static bool IsHttp(Uri uri)
        {
            return string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        }

        private static string Append(string first, string second)
        {
            if (string.IsNullOrEmpty(first)) return second;
            if (string.IsNullOrEmpty(second)) return first;
            return first + " " + second;
        }
    }
}
