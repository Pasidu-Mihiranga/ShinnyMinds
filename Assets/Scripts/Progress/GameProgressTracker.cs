using System;
using System.Collections;
using ShinyMinds.Api;
using UnityEngine;

namespace ShinyMinds.Progress
{
    /// <summary>
    /// Records what the player does so the parent dashboard has something real to show:
    /// how long they played, which missions they attempted, and every choice they made.
    ///
    /// Survives scene loads, because a session begins on the main menu and continues
    /// through gameplay.
    /// </summary>
    public class GameProgressTracker : MonoBehaviour
    {
        /// <summary>How often elapsed playtime is pushed to the server, in seconds.</summary>
        private const float HeartbeatInterval = 30f;

        private static GameProgressTracker _instance;

        public static GameProgressTracker Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject host = new GameObject("ShinyMinds.ProgressTracker");

                    _instance = host.AddComponent<GameProgressTracker>();

                    DontDestroyOnLoad(host);
                }

                return _instance;
            }
        }

        public string SessionId { get; private set; }
        public string AttemptId { get; private set; }
        public string MissionCode { get; private set; }

        public bool HasActiveSession => !string.IsNullOrEmpty(SessionId);
        public bool HasActiveMission => !string.IsNullOrEmpty(AttemptId);

        private float _sessionSeconds;
        private float _missionSeconds;
        private Coroutine _heartbeat;

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

        private void Update()
        {
            if (HasActiveSession)
            {
                _sessionSeconds += Time.unscaledDeltaTime;
            }

            if (HasActiveMission)
            {
                _missionSeconds += Time.unscaledDeltaTime;
            }
        }

        // --- session ----------------------------------------------------------

        public void BeginSession(Action<bool> onDone = null)
        {
            if (HasActiveSession)
            {
                onDone?.Invoke(true);

                return;
            }

            _sessionSeconds = 0f;

            ApiClient.Instance.StartSession(Application.platform.ToString(), result =>
            {
                if (result.Success && result.Data != null)
                {
                    SessionId = result.Data.SessionId;

                    _heartbeat = StartCoroutine(HeartbeatLoop());
                }
                else
                {
                    // Playtime tracking failing must never block play, so this is a
                    // warning and the game carries on unrecorded.
                    Debug.LogWarning($"[Progress] Could not start a session: {result.ErrorMessage}");
                }

                onDone?.Invoke(result.Success);
            });
        }

        public void EndSession(Action onDone = null)
        {
            if (!HasActiveSession)
            {
                onDone?.Invoke();

                return;
            }

            if (_heartbeat != null)
            {
                StopCoroutine(_heartbeat);

                _heartbeat = null;
            }

            string sessionId = SessionId;
            int seconds = Mathf.RoundToInt(_sessionSeconds);

            SessionId = null;

            ApiClient.Instance.EndSession(sessionId, seconds, _ => onDone?.Invoke());
        }

        private IEnumerator HeartbeatLoop()
        {
            WaitForSecondsRealtime wait = new WaitForSecondsRealtime(HeartbeatInterval);

            while (HasActiveSession)
            {
                yield return wait;

                if (!HasActiveSession)
                {
                    yield break;
                }

                // The total is sent, not a delta, so a dropped heartbeat self-corrects
                // on the next one instead of permanently losing that time.
                ApiClient.Instance.SessionHeartbeat(SessionId, Mathf.RoundToInt(_sessionSeconds), null);
            }
        }

        // --- missions ---------------------------------------------------------

        public void BeginMission(string missionCode, Action<bool, string> onDone = null)
        {
            _missionSeconds = 0f;

            ApiClient.Instance.StartMission(missionCode, SessionId, result =>
            {
                if (result.Success && result.Data != null)
                {
                    AttemptId = result.Data.AttemptId;
                    MissionCode = result.Data.MissionCode;
                }
                else
                {
                    Debug.LogWarning($"[Progress] Could not start mission '{missionCode}': {result.ErrorMessage}");
                }

                onDone?.Invoke(result.Success, result.ErrorMessage);
            });
        }

        /// <summary>
        /// Records one choice. <paramref name="skill"/> must be SAFETY, COMMUNICATION,
        /// EMPATHY or CONFIDENCE - these feed the four scores on the parent dashboard.
        /// </summary>
        public void RecordDecision(
            string promptCode,
            string promptText,
            string choiceText,
            string skill,
            bool isCorrect,
            int scoreDelta = 10)
        {
            if (!HasActiveMission)
            {
                Debug.LogWarning(
                    $"[Progress] Ignoring decision '{promptCode}': no mission is in progress. " +
                    "Call BeginMission first.");

                return;
            }

            ApiClient.Instance.RecordDecision(
                AttemptId,
                promptCode,
                promptText,
                choiceText,
                skill,
                isCorrect,
                isCorrect ? scoreDelta : 0,
                result =>
                {
                    if (!result.Success)
                    {
                        Debug.LogWarning($"[Progress] Decision not saved: {result.ErrorMessage}");
                    }
                });
        }

        public void CompleteMission(bool abandoned = false, Action<MissionResultResponse> onDone = null)
        {
            if (!HasActiveMission)
            {
                onDone?.Invoke(null);

                return;
            }

            string attemptId = AttemptId;
            int seconds = Mathf.RoundToInt(_missionSeconds);

            AttemptId = null;
            MissionCode = null;

            ApiClient.Instance.CompleteMission(attemptId, seconds, abandoned, result =>
            {
                if (!result.Success)
                {
                    Debug.LogWarning($"[Progress] Could not save the mission result: {result.ErrorMessage}");
                }

                onDone?.Invoke(result.Data);
            });
        }

        // Quitting is the usual way a play session ends, so playtime is flushed here
        // rather than only on an explicit "return to menu".
        private void OnApplicationQuit()
        {
            if (HasActiveMission)
            {
                CompleteMission(true);
            }

            EndSession();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && HasActiveSession)
            {
                ApiClient.Instance.SessionHeartbeat(SessionId, Mathf.RoundToInt(_sessionSeconds), null);
            }
        }
    }
}
