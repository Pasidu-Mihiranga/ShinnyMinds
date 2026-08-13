using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ShinyMinds.Api
{
    /// <summary>
    /// Wire models for the ShinyMinds backend. Field names match the JSON the API
    /// returns; see backend/src/routes for the endpoints these belong to.
    /// </summary>

    [Serializable]
    public class TokenPair
    {
        [JsonProperty("accessToken")] public string AccessToken;
        [JsonProperty("refreshToken")] public string RefreshToken;
        [JsonProperty("expiresIn")] public string ExpiresIn;
    }

    [Serializable]
    public class ChildAccount
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("username")] public string Username;
        [JsonProperty("displayName")] public string DisplayName;
        [JsonProperty("age")] public int? Age;
        [JsonProperty("isLinkedToParent")] public bool IsLinkedToParent;
    }

    [Serializable]
    public class AuthResponse
    {
        [JsonProperty("child")] public ChildAccount Child;
        [JsonProperty("tokens")] public TokenPair Tokens;
    }

    /// <summary>
    /// Linking returns the updated child only. It deliberately issues no new tokens:
    /// the player is already signed in and their session is unchanged.
    /// </summary>
    [Serializable]
    public class LinkParentResponse
    {
        [JsonProperty("child")] public ChildAccount Child;
    }

    [Serializable]
    public class RefreshResponse
    {
        [JsonProperty("tokens")] public TokenPair Tokens;
        [JsonProperty("role")] public string Role;
    }

    [Serializable]
    public class ContinueMission
    {
        [JsonProperty("code")] public string Code;
        [JsonProperty("title")] public string Title;
        [JsonProperty("topic")] public string Topic;
        [JsonProperty("resuming")] public bool Resuming;
    }

    [Serializable]
    public class PlayerProfile
    {
        [JsonProperty("child")] public ChildAccount Child;
        [JsonProperty("playtimeSeconds")] public int PlaytimeSeconds;
        [JsonProperty("skills")] public Dictionary<string, int> Skills;
        [JsonProperty("missionsCompleted")] public int MissionsCompleted;
        [JsonProperty("missionsTotal")] public int MissionsTotal;
        [JsonProperty("hasSavedProgress")] public bool HasSavedProgress;
        [JsonProperty("continueMission")] public ContinueMission ContinueMission;
    }

    [Serializable]
    public class MissionSummary
    {
        [JsonProperty("code")] public string Code;
        [JsonProperty("title")] public string Title;
        [JsonProperty("description")] public string Description;
        [JsonProperty("topic")] public string Topic;
        [JsonProperty("skill")] public string Skill;
        [JsonProperty("skillLabel")] public string SkillLabel;
        [JsonProperty("maxScore")] public int MaxScore;
        [JsonProperty("orderIndex")] public int OrderIndex;
        [JsonProperty("bestScore")] public int? BestScore;
        [JsonProperty("isCompleted")] public bool IsCompleted;
        [JsonProperty("isUnlocked")] public bool IsUnlocked;
        [JsonProperty("isInProgress")] public bool IsInProgress;
    }

    [Serializable]
    public class MissionListResponse
    {
        [JsonProperty("missions")] public List<MissionSummary> Missions;
    }

    [Serializable]
    public class SessionResponse
    {
        [JsonProperty("sessionId")] public string SessionId;
    }

    [Serializable]
    public class AttemptResponse
    {
        [JsonProperty("attemptId")] public string AttemptId;
        [JsonProperty("missionCode")] public string MissionCode;
        [JsonProperty("resumed")] public bool Resumed;
    }

    [Serializable]
    public class MissionResultResponse
    {
        [JsonProperty("attemptId")] public string AttemptId;
        [JsonProperty("missionCode")] public string MissionCode;
        [JsonProperty("status")] public string Status;
        [JsonProperty("score")] public int Score;
        [JsonProperty("maxScore")] public int MaxScore;
        [JsonProperty("skills")] public Dictionary<string, int> Skills;
    }

    /// <summary>The shape of every error the API returns.</summary>
    [Serializable]
    public class ApiErrorEnvelope
    {
        [JsonProperty("error")] public ApiErrorBody Error;
    }

    [Serializable]
    public class ApiErrorBody
    {
        [JsonProperty("code")] public string Code;
        [JsonProperty("message")] public string Message;
        [JsonProperty("details")] public List<ApiErrorDetail> Details;
    }

    [Serializable]
    public class ApiErrorDetail
    {
        [JsonProperty("field")] public string Field;
        [JsonProperty("message")] public string Message;
    }

    /// <summary>
    /// Result of one API call. Callers check <see cref="Success"/> and show
    /// <see cref="ErrorMessage"/>, which is always safe to display to a child.
    /// </summary>
    public class ApiResult<T>
    {
        public bool Success;
        public T Data;
        public string ErrorMessage;
        public long StatusCode;

        public static ApiResult<T> Ok(T data, long status)
        {
            return new ApiResult<T> { Success = true, Data = data, StatusCode = status };
        }

        public static ApiResult<T> Fail(string message, long status)
        {
            return new ApiResult<T> { Success = false, ErrorMessage = message, StatusCode = status };
        }
    }
}
