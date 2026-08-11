using UnityEngine;

namespace ShinyMinds.Api
{
    /// <summary>
    /// Remembers who is signed in between runs, so the menu can offer Continue
    /// without asking for a password every time.
    ///
    /// Tokens live in PlayerPrefs. That is readable by anyone with access to the
    /// machine, which is why the access token is short-lived and the refresh token
    /// can be revoked server-side - a copied token stops working once the child
    /// signs out. It is deliberately not treated as secure storage.
    /// </summary>
    public static class PlayerSession
    {
        private const string AccessTokenKey = "shinyminds.accessToken";
        private const string RefreshTokenKey = "shinyminds.refreshToken";
        private const string ChildIdKey = "shinyminds.childId";
        private const string DisplayNameKey = "shinyminds.displayName";

        public static string AccessToken => PlayerPrefs.GetString(AccessTokenKey, string.Empty);
        public static string RefreshToken => PlayerPrefs.GetString(RefreshTokenKey, string.Empty);
        public static string ChildId => PlayerPrefs.GetString(ChildIdKey, string.Empty);
        public static string DisplayName => PlayerPrefs.GetString(DisplayNameKey, string.Empty);

        public static bool IsSignedIn => !string.IsNullOrEmpty(RefreshToken);

        public static void Save(ChildAccount child, TokenPair tokens)
        {
            if (tokens != null)
            {
                PlayerPrefs.SetString(AccessTokenKey, tokens.AccessToken ?? string.Empty);
                PlayerPrefs.SetString(RefreshTokenKey, tokens.RefreshToken ?? string.Empty);
            }

            if (child != null)
            {
                PlayerPrefs.SetString(ChildIdKey, child.Id ?? string.Empty);
                PlayerPrefs.SetString(DisplayNameKey, child.DisplayName ?? string.Empty);
            }

            PlayerPrefs.Save();
        }

        public static void UpdateTokens(TokenPair tokens)
        {
            Save(null, tokens);
        }

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(AccessTokenKey);
            PlayerPrefs.DeleteKey(RefreshTokenKey);
            PlayerPrefs.DeleteKey(ChildIdKey);
            PlayerPrefs.DeleteKey(DisplayNameKey);
            PlayerPrefs.Save();
        }
    }
}
