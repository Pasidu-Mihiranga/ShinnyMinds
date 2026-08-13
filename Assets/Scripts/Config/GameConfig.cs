using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ShinyMinds.Config
{
    /// <summary>
    /// Central access point for every secret and environment-specific value.
    ///
    /// Keys used to live on public fields of GroqDialogue / ElevenLabsTTS, which meant
    /// Unity serialised them into SampleScene.unity. Every developer who pasted their own
    /// key dirtied that 17k-line YAML file, so every pull produced a merge conflict.
    /// Nothing secret is serialised any more: values are read from a .env file that is
    /// git-ignored, or from real OS environment variables.
    ///
    /// Lookup order (first hit wins):
    ///   1. OS environment variable   - how CI and production should supply values
    ///   2. .env next to the built player / in StreamingAssets - for QA builds
    ///   3. .env at the repository root - how everyone works day to day in the Editor
    /// </summary>
    public static class GameConfig
    {
        public const string GroqApiKeyName = "GROQ_API_KEY";
        public const string ElevenLabsApiKeyName = "ELEVENLABS_API_KEY";
        public const string NpcVoiceIdName = "ELEVENLABS_NPC_VOICE_ID";
        public const string GirlVoiceIdName = "ELEVENLABS_GIRL_VOICE_ID";
        public const string ApiBaseUrlName = "SHINYMINDS_API_URL";

        /// <summary>
        /// Environment variable holding the voice for a mission speaker key, e.g.
        /// "stranger" becomes ELEVENLABS_STRANGER_VOICE_ID. Missions cast their
        /// characters by key so a new speaker needs no code change, only a .env line.
        /// </summary>
        public static string VoiceIdNameFor(string speakerKey)
        {
            return string.IsNullOrWhiteSpace(speakerKey)
                ? NpcVoiceIdName
                : "ELEVENLABS_" + speakerKey.Trim().ToUpperInvariant() + "_VOICE_ID";
        }

        private static Dictionary<string, string> _values;

        public static string GroqApiKey => Get(GroqApiKeyName);
        public static string ElevenLabsApiKey => Get(ElevenLabsApiKeyName);
        public static string NpcVoiceId => Get(NpcVoiceIdName);
        public static string GirlVoiceId => Get(GirlVoiceIdName);

        /// <summary>
        /// Voice for a mission speaker, falling back to the NPC voice when that speaker
        /// has no line of their own in .env. The fallback is what lets a mission be cast
        /// one character at a time instead of all at once.
        /// </summary>
        public static string VoiceIdForSpeaker(string speakerKey)
        {
            string specific = Get(VoiceIdNameFor(speakerKey));

            return string.IsNullOrWhiteSpace(specific)
                ? NpcVoiceId
                : specific;
        }

        /// <summary>Base URL of the ShinyMinds backend, without a trailing slash.</summary>
        public static string ApiBaseUrl
        {
            get
            {
                string url = Get(ApiBaseUrlName);

                if (string.IsNullOrWhiteSpace(url))
                {
                    url = "http://localhost:4000";
                }

                return url.TrimEnd('/');
            }
        }

        /// <summary>
        /// Reads a configuration value. Returns an empty string when the key is absent so
        /// callers can test with string.IsNullOrWhiteSpace rather than null-checking.
        /// </summary>
        public static string Get(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            string fromEnvironment = SafeGetEnvironmentVariable(key);

            if (!string.IsNullOrWhiteSpace(fromEnvironment))
            {
                return fromEnvironment.Trim();
            }

            EnsureLoaded();

            return _values.TryGetValue(key, out string value)
                ? value
                : string.Empty;
        }

        /// <summary>
        /// Logs a clear, actionable error when a required key is missing, so a blank key
        /// surfaces as one readable message instead of a 401 from a third-party API.
        /// </summary>
        public static bool Require(string key, string usedBy)
        {
            if (!string.IsNullOrWhiteSpace(Get(key)))
            {
                return true;
            }

            Debug.LogError(
                $"[GameConfig] Missing '{key}', required by {usedBy}.\n" +
                $"Copy .env.example to .env in the project root and fill in {key}. " +
                "The Editor reads it on the next play; there is nothing to set in the Inspector.");

            return false;
        }

        /// <summary>Forces the next read to re-parse the .env file. Useful after editing it.</summary>
        public static void Reload()
        {
            _values = null;
        }

        private static void EnsureLoaded()
        {
            if (_values != null)
            {
                return;
            }

            _values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (string path in CandidatePaths())
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    continue;
                }

                Parse(File.ReadAllLines(path));

                Debug.Log($"[GameConfig] Loaded {_values.Count} value(s) from {path}");

                return;
            }

            Debug.LogWarning(
                "[GameConfig] No .env file found. Copy .env.example to .env in the project root " +
                "and add your keys, or export them as environment variables.");
        }

        private static IEnumerable<string> CandidatePaths()
        {
            // Repository root in the Editor; alongside the executable in a build.
            yield return Path.Combine(Application.dataPath, "..", ".env");

            yield return Path.Combine(Application.streamingAssetsPath, ".env");
            yield return Path.Combine(Application.persistentDataPath, ".env");
        }

        private static void Parse(IEnumerable<string> lines)
        {
            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();

                if (line.Length == 0 || line.StartsWith("#"))
                {
                    continue;
                }

                // Tolerate "export KEY=value", which is common when the same file is sourced by a shell.
                if (line.StartsWith("export "))
                {
                    line = line.Substring("export ".Length).Trim();
                }

                int separator = line.IndexOf('=');

                if (separator <= 0)
                {
                    continue;
                }

                string key = line.Substring(0, separator).Trim();
                string value = line.Substring(separator + 1).Trim();

                value = Unquote(value);

                if (key.Length > 0)
                {
                    _values[key] = value;
                }
            }
        }

        private static string Unquote(string value)
        {
            if (value.Length >= 2 &&
                ((value[0] == '"' && value[value.Length - 1] == '"') ||
                 (value[0] == '\'' && value[value.Length - 1] == '\'')))
            {
                return value.Substring(1, value.Length - 2);
            }

            return value;
        }

        private static string SafeGetEnvironmentVariable(string key)
        {
            try
            {
                return Environment.GetEnvironmentVariable(key);
            }
            catch (Exception)
            {
                // Some platforms (WebGL, restricted consoles) throw rather than returning null.
                return null;
            }
        }
    }
}
