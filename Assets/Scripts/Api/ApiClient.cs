using System;
using System.Collections;
using System.Text;
using Newtonsoft.Json;
using ShinyMinds.Config;
using UnityEngine;
using UnityEngine.Networking;

namespace ShinyMinds.Api
{
    /// <summary>
    /// The only place the game talks to the ShinyMinds backend.
    ///
    /// A persistent singleton, so a request started on the menu survives the load into
    /// the gameplay scene. Every call is a coroutine taking a callback: Unity's web
    /// requests are frame-based, and this keeps calling code free of async plumbing.
    /// </summary>
    public class ApiClient : MonoBehaviour
    {
        private const int TimeoutSeconds = 20;

        private static ApiClient _instance;

        public static ApiClient Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject host = new GameObject("ShinyMinds.ApiClient");

                    _instance = host.AddComponent<ApiClient>();

                    DontDestroyOnLoad(host);
                }

                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);

                return;
            }

            _instance = this;

            DontDestroyOnLoad(gameObject);
        }

        public string BaseUrl => GameConfig.ApiBaseUrl;

        // --- authentication ---------------------------------------------------

        public Coroutine RegisterPlayer(
            string username,
            string password,
            string displayName,
            int? age,
            string parentLinkCode,
            Action<ApiResult<AuthResponse>> onDone)
        {
            object body = new
            {
                username,
                password,
                displayName,
                age,
                parentLinkCode = string.IsNullOrWhiteSpace(parentLinkCode) ? null : parentLinkCode,
            };

            return StartCoroutine(
                Send<AuthResponse>("POST", "/api/auth/player/register", body, false, result =>
                {
                    if (result.Success)
                    {
                        PlayerSession.Save(result.Data.Child, result.Data.Tokens);
                    }

                    onDone?.Invoke(result);
                }));
        }

        public Coroutine LoginPlayer(
            string username,
            string password,
            Action<ApiResult<AuthResponse>> onDone)
        {
            return StartCoroutine(
                Send<AuthResponse>("POST", "/api/auth/player/login", new { username, password }, false, result =>
                {
                    if (result.Success)
                    {
                        PlayerSession.Save(result.Data.Child, result.Data.Tokens);
                    }

                    onDone?.Invoke(result);
                }));
        }

        public Coroutine LinkParent(string parentLinkCode, Action<ApiResult<LinkParentResponse>> onDone)
        {
            return StartCoroutine(
                Send("POST", "/api/auth/player/link-parent", new { parentLinkCode }, true, onDone));
        }

        public Coroutine SignOut(Action onDone)
        {
            string refreshToken = PlayerSession.RefreshToken;

            PlayerSession.Clear();

            if (string.IsNullOrEmpty(refreshToken))
            {
                onDone?.Invoke();

                return null;
            }

            // Revoking server-side matters: clearing PlayerPrefs alone would leave a
            // usable refresh token behind on any machine that had already copied it.
            return StartCoroutine(
                Send<object>("POST", "/api/auth/logout", new { refreshToken }, false, _ => onDone?.Invoke()));
        }

        // --- profile and missions --------------------------------------------

        public Coroutine GetProfile(Action<ApiResult<PlayerProfile>> onDone)
        {
            return StartCoroutine(Send("GET", "/api/game/profile", null, true, onDone));
        }

        public Coroutine GetMissions(Action<ApiResult<MissionListResponse>> onDone)
        {
            return StartCoroutine(Send("GET", "/api/game/missions", null, true, onDone));
        }

        public Coroutine StartNewGame(Action<ApiResult<object>> onDone)
        {
            return StartCoroutine(Send("POST", "/api/game/new-game", new { }, true, onDone));
        }

        // --- gameplay recording -----------------------------------------------

        public Coroutine StartSession(string platform, Action<ApiResult<SessionResponse>> onDone)
        {
            return StartCoroutine(Send("POST", "/api/game/sessions", new { platform }, true, onDone));
        }

        public Coroutine SessionHeartbeat(string sessionId, int durationSeconds, Action<ApiResult<object>> onDone)
        {
            return StartCoroutine(
                Send("POST", $"/api/game/sessions/{sessionId}/heartbeat", new { durationSeconds }, true, onDone));
        }

        public Coroutine EndSession(string sessionId, int durationSeconds, Action<ApiResult<object>> onDone)
        {
            return StartCoroutine(
                Send("POST", $"/api/game/sessions/{sessionId}/end", new { durationSeconds }, true, onDone));
        }

        public Coroutine StartMission(string missionCode, string sessionId, Action<ApiResult<AttemptResponse>> onDone)
        {
            object body = string.IsNullOrEmpty(sessionId)
                ? new { missionCode }
                : (object)new { missionCode, sessionId };

            return StartCoroutine(Send("POST", "/api/game/missions/start", body, true, onDone));
        }

        public Coroutine RecordDecision(
            string attemptId,
            string promptCode,
            string promptText,
            string choiceText,
            string skill,
            bool isCorrect,
            int scoreDelta,
            Action<ApiResult<object>> onDone)
        {
            object body = new { promptCode, promptText, choiceText, skill, isCorrect, scoreDelta };

            return StartCoroutine(
                Send("POST", $"/api/game/attempts/{attemptId}/decisions", body, true, onDone));
        }

        /// <summary>
        /// Records how far the player has got, so Continue can resume there. Best-effort:
        /// a checkpoint that fails to save costs a little replayed dialogue and nothing more.
        /// </summary>
        public Coroutine SaveCheckpoint(
            string attemptId,
            string nodeId,
            Action<ApiResult<object>> onDone = null)
        {
            return StartCoroutine(
                Send(
                    "POST",
                    $"/api/game/attempts/{attemptId}/checkpoint",
                    new { nodeId },
                    true,
                    onDone));
        }

        public Coroutine CompleteMission(
            string attemptId,
            int durationSeconds,
            bool abandoned,
            Action<ApiResult<MissionResultResponse>> onDone)
        {
            return StartCoroutine(
                Send(
                    "POST",
                    $"/api/game/attempts/{attemptId}/complete",
                    new { durationSeconds, abandoned },
                    true,
                    onDone));
        }

        // --- transport --------------------------------------------------------

        private IEnumerator Send<T>(
            string method,
            string path,
            object body,
            bool authenticated,
            Action<ApiResult<T>> onDone,
            bool allowRetryAfterRefresh = true)
        {
            using (UnityWebRequest request = Build(method, path, body, authenticated))
            {
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.ConnectionError)
                {
                    onDone?.Invoke(ApiResult<T>.Fail(
                        "Cannot reach the ShinyMinds server. Check that the backend is running " +
                        $"at {BaseUrl}.",
                        0));

                    yield break;
                }

                long status = request.responseCode;
                string payload = request.downloadHandler?.text ?? string.Empty;

                // An expired access token is the normal case, not an error: refresh once
                // and replay the request before troubling the player with a login screen.
                if (status == 401 && authenticated && allowRetryAfterRefresh && PlayerSession.IsSignedIn)
                {
                    bool refreshed = false;

                    yield return StartCoroutine(TryRefresh(success => refreshed = success));

                    if (refreshed)
                    {
                        yield return StartCoroutine(
                            Send(method, path, body, true, onDone, false));

                        yield break;
                    }

                    PlayerSession.Clear();

                    onDone?.Invoke(ApiResult<T>.Fail("Your session has expired. Please sign in again.", status));

                    yield break;
                }

                if (status >= 200 && status < 300)
                {
                    onDone?.Invoke(ApiResult<T>.Ok(Deserialize<T>(payload), status));

                    yield break;
                }

                onDone?.Invoke(ApiResult<T>.Fail(ExtractErrorMessage(payload, request.error), status));
            }
        }

        private UnityWebRequest Build(string method, string path, object body, bool authenticated)
        {
            UnityWebRequest request = new UnityWebRequest(BaseUrl + path, method)
            {
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = TimeoutSeconds,
            };

            if (body != null)
            {
                // NullValueHandling.Ignore keeps optional fields such as age and
                // parentLinkCode out of the payload entirely when they are unset,
                // rather than sending nulls the server would have to special-case.
                string json = JsonConvert.SerializeObject(
                    body,
                    new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                request.SetRequestHeader("Content-Type", "application/json");
            }

            if (authenticated && !string.IsNullOrEmpty(PlayerSession.AccessToken))
            {
                request.SetRequestHeader("Authorization", "Bearer " + PlayerSession.AccessToken);
            }

            return request;
        }

        private IEnumerator TryRefresh(Action<bool> onDone)
        {
            string refreshToken = PlayerSession.RefreshToken;

            if (string.IsNullOrEmpty(refreshToken))
            {
                onDone(false);

                yield break;
            }

            using (UnityWebRequest request = Build("POST", "/api/auth/refresh", new { refreshToken }, false))
            {
                yield return request.SendWebRequest();

                if (request.responseCode < 200 || request.responseCode >= 300)
                {
                    onDone(false);

                    yield break;
                }

                RefreshResponse response = Deserialize<RefreshResponse>(request.downloadHandler.text);

                if (response?.Tokens == null)
                {
                    onDone(false);

                    yield break;
                }

                PlayerSession.UpdateTokens(response.Tokens);

                onDone(true);
            }
        }

        private static T Deserialize<T>(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                return default;
            }

            try
            {
                return JsonConvert.DeserializeObject<T>(payload);
            }
            catch (JsonException exception)
            {
                Debug.LogError($"[ApiClient] Could not parse response: {exception.Message}\n{payload}");

                return default;
            }
        }

        /// <summary>
        /// Pulls the human-readable message out of the API's error envelope, including
        /// per-field validation messages, so the UI can show what the player must fix.
        /// </summary>
        private static string ExtractErrorMessage(string payload, string fallback)
        {
            try
            {
                ApiErrorEnvelope envelope = JsonConvert.DeserializeObject<ApiErrorEnvelope>(payload);

                if (envelope?.Error != null)
                {
                    if (envelope.Error.Details != null && envelope.Error.Details.Count > 0)
                    {
                        StringBuilder builder = new StringBuilder();

                        foreach (ApiErrorDetail detail in envelope.Error.Details)
                        {
                            if (builder.Length > 0)
                            {
                                builder.Append('\n');
                            }

                            builder.Append(detail.Message);
                        }

                        return builder.ToString();
                    }

                    if (!string.IsNullOrWhiteSpace(envelope.Error.Message))
                    {
                        return envelope.Error.Message;
                    }
                }
            }
            catch (JsonException)
            {
                // Not our envelope - fall through to the transport error below.
            }

            return string.IsNullOrWhiteSpace(fallback)
                ? "Something went wrong. Please try again."
                : fallback;
        }
    }
}
