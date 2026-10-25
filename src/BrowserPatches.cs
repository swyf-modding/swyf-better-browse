using System;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using ScamWYF.Modding.Core;
using UnityEngine;

namespace ScamWYF.BetterBrowse
{
    /// <summary>
    /// The four small hooks that stand between Google and the address bar.
    /// </summary>
    /// <remarks>
    /// The game's browser normalises every navigation in one public static method,
    /// KolkataOsBrowserHost.NormalizeBrowserUrl. Plain text that is not a URL becomes
    /// https://www.google.com/search?q=..., and the home page comes from the browser app's JSON,
    /// which is Google too. That is the whole of the game's search engine.
    ///
    /// The type itself is internal, so it is found by name at load rather than referenced at build
    /// time. If a future game build renames it or changes a signature, the mod logs which method it
    /// wanted and leaves the browser alone instead of failing in the middle of a session.
    /// </remarks>
    internal static class BrowserPatches
    {
        private const string HostTypeName = "Kolkata.UI.KolkataOsBrowserHost";

        private static FieldInfo _homeUrlField;
        private static FieldInfo _currentUrlField;
        private static MethodInfo _refreshAddressField;
        private static MethodInfo _navigate;
        private static string _gameHomeUrl;
        private static string _lastAppliedHome;
        private static bool _loggedReflectionFailure;

        /// <summary>Install the hooks. Returns false when this build has no browser to hook.</summary>
        public static bool Install(ScamMod owner, ManualLogSource log, out Type hostType)
        {
            hostType = null;

            Type resolved;
            if (!GameBuild.TryResolveType(owner, HostTypeName, out resolved)) return false;
            hostType = resolved;

            var normalize = AccessTools.Method(hostType, "NormalizeBrowserUrl",
                new[] { typeof(string), typeof(string) });
            if (normalize == null)
            {
                log.LogWarning(HostTypeName + " has no NormalizeBrowserUrl(string, string) in this build. " +
                               "The browser is being left alone; the game was most likely updated.");
                return false;
            }

            var installed = PatchCoordinator.TryPatch(
                owner,
                normalize,
                "send address-bar searches to the configured engine",
                postfix: new HarmonyMethod(AccessTools.Method(typeof(BrowserPatches), nameof(NormalizePostfix))));

            // Home is cosmetic relative to search redirection, so a missing field or method must not
            // stop the search hook from working.
            _homeUrlField = AccessTools.Field(hostType, "homeUrl");
            _currentUrlField = AccessTools.Field(hostType, "currentUrl");
            _refreshAddressField = AccessTools.Method(hostType, "RefreshAddressField");
            _navigate = AccessTools.Method(hostType, "Navigate", new[] { typeof(string) });

            var bind = AccessTools.Method(hostType, "BindBrowserView");
            if (bind != null && _homeUrlField != null && _currentUrlField != null)
            {
                installed |= PatchCoordinator.TryPatch(
                    owner,
                    bind,
                    "start the browser on the configured home page",
                    postfix: new HarmonyMethod(AccessTools.Method(typeof(BrowserPatches), nameof(BindPostfix))));
            }
            else
            {
                log.LogWarning("Could not hook the browser's home page; searches are still redirected.");
            }

            var navigateHome = AccessTools.Method(hostType, "NavigateHome", Type.EmptyTypes);
            if (navigateHome != null && _navigate != null)
            {
                installed |= PatchCoordinator.TryPatch(
                    owner,
                    navigateHome,
                    "send the magnifier button to the configured home page",
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(BrowserPatches), nameof(NavigateHomePrefix))));
            }

            // The game keeps search-provider hosts out of the player's browser history. Teach it the
            // configured engine as well, so a SearXNG instance or a custom engine is not logged as a
            // visited website in the review.
            var ignored = AccessTools.Method(hostType, "IsIgnoredReviewSearchProvider",
                new[] { typeof(string) });
            if (ignored != null)
            {
                installed |= PatchCoordinator.TryPatch(
                    owner,
                    ignored,
                    "keep searches out of the player's browser history",
                    postfix: new HarmonyMethod(AccessTools.Method(typeof(BrowserPatches), nameof(IgnoredProviderPostfix))));
            }

            return installed;
        }

        /// <summary>
        /// The one method every navigation goes through. It turns text into a Google search, and
        /// blank input into the home page; both are replaced here.
        /// </summary>
        private static void NormalizePostfix(string url, ref string __result)
        {
            var plan = Plugin.Plan;
            if (plan == null || !plan.Enabled) return;

            try
            {
                if (string.IsNullOrEmpty(url) || url.Trim().Length == 0)
                {
                    if (plan.ReplacesHome) __result = plan.HomeUrl;
                    else if (!string.IsNullOrEmpty(_gameHomeUrl)) __result = _gameHomeUrl;
                    return;
                }

                string query;
                if (!SearchEngines.TryGetGoogleQuery(__result, out query)) return;

                var rewritten = plan.BuildSearchUrl(query);
                __result = rewritten;
                Plugin.RecordRewrite(query, rewritten);
            }
            catch (Exception ex)
            {
                LogFailureOnce("NormalizeBrowserUrl", ex);
            }
        }

        /// <summary>
        /// BindBrowserView copies the browser app's Google home URL into the host. Replace it, so the
        /// address bar and the history start on the configured page rather than flashing Google.
        /// </summary>
        private static void BindPostfix(MonoBehaviour __instance)
        {
            if (__instance == null) return;

            var plan = Plugin.Plan;
            if (plan == null || !plan.Enabled) return;

            try
            {
                // Keep the game's own home URL, so switching HomePage back to GameDefault works
                // without reopening the browser app.
                var stored = _homeUrlField.GetValue(__instance) as string;
                if (string.IsNullOrEmpty(_gameHomeUrl) && !string.IsNullOrEmpty(stored))
                    _gameHomeUrl = stored;

                var chosen = plan.ReplacesHome ? plan.HomeUrl : _gameHomeUrl;
                if (string.IsNullOrEmpty(chosen)) return;

                _homeUrlField.SetValue(__instance, chosen);

                var current = _currentUrlField.GetValue(__instance) as string;
                if (!string.IsNullOrEmpty(current) && !SearchEngines.IsGoogleHome(current) &&
                    !IsLastAppliedHome(current))
                {
                    _lastAppliedHome = chosen;
                    return;
                }

                _currentUrlField.SetValue(__instance, chosen);
                if (_refreshAddressField != null) _refreshAddressField.Invoke(__instance, null);
                _lastAppliedHome = chosen;
            }
            catch (Exception ex)
            {
                LogFailureOnce("BindBrowserView", ex);
            }
        }

        private static bool IsLastAppliedHome(string url)
        {
            return !string.IsNullOrEmpty(_lastAppliedHome) &&
                   string.Equals(url, _lastAppliedHome, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// NavigateHome navigates to the host's stored home URL. Use the plan instead, so a config
        /// change takes effect on the magnifier button without reopening the app.
        /// </summary>
        private static bool NavigateHomePrefix(MonoBehaviour __instance)
        {
            var plan = Plugin.Plan;
            if (__instance == null || plan == null || !plan.Enabled || _navigate == null) return true;

            var home = plan.ReplacesHome ? plan.HomeUrl : _gameHomeUrl;
            if (string.IsNullOrEmpty(home)) return true;

            try
            {
                _navigate.Invoke(__instance, new object[] { home });
                return false;
            }
            catch (Exception ex)
            {
                LogFailureOnce("NavigateHome", ex);
                return true;
            }
        }

        private static void IgnoredProviderPostfix(string host, ref bool __result)
        {
            if (__result) return;

            var plan = Plugin.Plan;
            if (plan == null || !plan.Enabled) return;
            if (plan.OwnsHost(host)) __result = true;
        }

        /// <summary>
        /// Feed the patched method a sample search and a blank home navigation, and check the results.
        /// This is what makes a game update that keeps the method but changes its URL shape visible in
        /// the log rather than as a browser that mysteriously still uses Google.
        /// </summary>
        public static bool Verify(Type hostType, ManualLogSource log)
        {
            var plan = Plugin.Plan;
            if (hostType == null || plan == null || !plan.Enabled) return true;

            var normalize = AccessTools.Method(hostType, "NormalizeBrowserUrl",
                new[] { typeof(string), typeof(string) });
            if (normalize == null) return false;

            Plugin.Verifying = true;
            try
            {
                var expected = plan.BuildSearchUrl("probe+query");
                var search = (string)normalize.Invoke(null,
                    new object[] { "probe query", "https://www.google.com" });

                if (!string.Equals(search, expected, StringComparison.Ordinal))
                {
                    log.LogWarning("Hook check failed: a test search came back as '" + search +
                                   "' instead of '" + expected + "'. Searches are not being redirected; " +
                                   "the game's URL shape may have changed.");
                    return false;
                }

                if (plan.ReplacesHome)
                {
                    var home = (string)normalize.Invoke(null,
                        new object[] { "", "https://www.google.com" });
                    if (!string.Equals(home, plan.HomeUrl, StringComparison.Ordinal))
                    {
                        log.LogWarning("Hook check failed: a blank navigation came back as '" + home +
                                       "' instead of '" + plan.HomeUrl + "'. The home page is not being " +
                                       "replaced, but searches are.");
                        return false;
                    }
                }

                log.LogInfo("Hook check passed: a test search goes to " + search + ".");
                return true;
            }
            catch (Exception ex)
            {
                log.LogWarning("Hook check could not run: " + ex.Message);
                return false;
            }
            finally
            {
                Plugin.Verifying = false;
            }
        }

        private static void LogFailureOnce(string where, Exception ex)
        {
            if (_loggedReflectionFailure) return;
            _loggedReflectionFailure = true;
            Plugin.Log.LogWarning("Browser hook " + where + " failed once and is being skipped: " + ex);
        }
    }
}
