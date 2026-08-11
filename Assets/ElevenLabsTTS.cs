using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ShinyMinds.Config;

public class ElevenLabsTTS : MonoBehaviour
{
    [Header("Audio")]
    public AudioSource audioSource;
    public bool IsSpeaking { get; private set; }
    public delegate void SpeechFinished();
    public event SpeechFinished OnSpeechFinished;

    // The ElevenLabs key is deliberately NOT a serialized field. Keeping it in the
    // Inspector wrote it into SampleScene.unity and made every pull conflict.
    // See GameConfig and .env.example in the repository root.

    [Header("Voice IDs")]
    [Tooltip("Leave blank to use ELEVENLABS_NPC_VOICE_ID from .env.")]
    public string npcVoiceId;

    [Tooltip("Leave blank to use ELEVENLABS_GIRL_VOICE_ID from .env.")]
    public string girlVoiceId;

    // NPC voice
    public void SpeakNPC(string text)
    {
        StartCoroutine(
            GenerateSpeech(
                text,
                ResolveVoiceId(npcVoiceId, GameConfig.NpcVoiceId)
            )
        );
    }

    // Girl voice
    public void SpeakGirl(string text)
    {
        StartCoroutine(
            GenerateSpeech(
                text,
                ResolveVoiceId(girlVoiceId, GameConfig.GirlVoiceId)
            )
        );
    }

    // A voice ID set on this component wins, so a single NPC can be given a distinct
    // voice without touching everyone else's .env.
    static string ResolveVoiceId(string inspectorValue, string configValue)
    {
        return string.IsNullOrWhiteSpace(inspectorValue)
            ? configValue
            : inspectorValue;
    }

    IEnumerator GenerateSpeech(
        string text,
        string voiceId
    )
    {
        // Claimed before the request starts, not when playback begins. GroqDialogue gates
        // the "E = Next" key on IsSpeaking, so leaving it false during generation let the
        // player skip past a line while its audio was still being fetched.
        IsSpeaking = true;

        string elevenLabsApiKey = GameConfig.ElevenLabsApiKey;

        if (string.IsNullOrWhiteSpace(elevenLabsApiKey))
        {
            GameConfig.Require(
                GameConfig.ElevenLabsApiKeyName,
                "ElevenLabsTTS"
            );

            FinishWithoutSpeaking();

            yield break;
        }

        if (string.IsNullOrWhiteSpace(voiceId))
        {
            Debug.LogError(
                "[ElevenLabsTTS] No voice ID. Set one on this component, or set " +
                GameConfig.NpcVoiceIdName + " / " +
                GameConfig.GirlVoiceIdName + " in .env."
            );

            FinishWithoutSpeaking();

            yield break;
        }

        string url =
            "https://api.elevenlabs.io/v1/text-to-speech/"
            + voiceId;

        // Built with a JSON writer rather than string concatenation: generated dialogue
        // regularly contains apostrophes, quotes and newlines that would break hand-built JSON.
        string json = new JObject(
            new JProperty("text", text),
            new JProperty("model_id", "eleven_multilingual_v2")
        ).ToString(Formatting.None);

        UnityWebRequest request =
            new UnityWebRequest(
                url,
                "POST"
            );

        byte[] bodyRaw =
            Encoding.UTF8.GetBytes(json);

        request.uploadHandler =
            new UploadHandlerRaw(bodyRaw);

        request.downloadHandler =
            new DownloadHandlerBuffer();

        request.SetRequestHeader(
            "Content-Type",
            "application/json"
        );

        request.SetRequestHeader(
            "xi-api-key",
            elevenLabsApiKey
        );

        yield return request.SendWebRequest();

        if (request.result ==
            UnityWebRequest.Result.Success)
        {
            byte[] audioBytes =
                request.downloadHandler.data;

            string path =
                Application.persistentDataPath +
                "/voice.mp3";

            System.IO.File.WriteAllBytes(
                path,
                audioBytes
            );

            Debug.Log(
                "Saved audio to: " + path
            );

            yield return StartCoroutine(
                PlayAudio(path)
            );

            Debug.Log(
                "Generated voice for: "
                + text
            );
        }
        else
        {
            Debug.LogError(
                "[ElevenLabsTTS] " + request.error + "\n" +
                request.downloadHandler.text
            );

            FinishWithoutSpeaking();
        }
    }

    // Speech could not be produced. Callers wait on IsSpeaking and OnSpeechFinished to
    // re-enable the "E = Next" prompt, so both must still settle or the dialogue locks up.
    void FinishWithoutSpeaking()
    {
        IsSpeaking = false;
        OnSpeechFinished?.Invoke();
    }

    IEnumerator PlayAudio(string path)
    {
        string uri =
            "file://" + path;

        UnityWebRequest audioRequest =
            UnityWebRequestMultimedia.GetAudioClip(
                uri,
                AudioType.MPEG
            );

        yield return audioRequest.SendWebRequest();

        if (audioRequest.result ==
            UnityWebRequest.Result.Success)
        {
            AudioClip clip =
                DownloadHandlerAudioClip
                .GetContent(audioRequest);

            audioSource.clip = clip;

            IsSpeaking = true;

            audioSource.Play();

            yield return new WaitWhile(
                () => audioSource.isPlaying
            );

            IsSpeaking = false;
            OnSpeechFinished?.Invoke();
        }
        else
        {
            Debug.LogError(
                "[ElevenLabsTTS] " + audioRequest.error
            );

            FinishWithoutSpeaking();
        }
    }
}