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

    /// <summary>
    /// Speaks with an explicit voice. Missions cast their own characters, so they pass
    /// the voice in rather than picking one of the two named above.
    /// </summary>
    public void Speak(string text, string voiceId)
    {
        StartCoroutine(GenerateSpeech(text, voiceId));
    }

    /// <summary>
    /// Cuts off the current line. Used when a mission is aborted mid-sentence, where the
    /// alternative is a disembodied voice talking over the main menu.
    /// </summary>
    public void StopSpeaking()
    {
        StopAllCoroutines();

        if (audioSource != null)
            audioSource.Stop();

        if (IsSpeaking)
            FinishWithoutSpeaking();
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

            // One file per clip. Every line used to be written to the same voice.mp3,
            // so two overlapping requests fought over it and the loader could read a
            // half-written file — the race that kept MissionNode.speakAloud switched off.
            string path = NextClipPath();

            System.IO.File.WriteAllBytes(
                path,
                audioBytes
            );

            yield return StartCoroutine(
                PlayAudio(path)
            );

            // Cleaned up once played: these accumulate in persistentDataPath otherwise,
            // one file per spoken line, for the life of the install.
            TryDelete(path);
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

    // Unique per clip and per run: the counter alone would collide with files left behind
    // by a previous session if one was killed before its cleanup ran.
    static int clipCounter;

    static string NextClipPath()
    {
        clipCounter++;

        return Path.Combine(
            Application.persistentDataPath,
            $"voice_{System.DateTime.UtcNow.Ticks}_{clipCounter}.mp3"
        );
    }

    static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (System.Exception e)
        {
            // A clip still held open by the audio system is not worth failing a line over.
            Debug.LogWarning("[ElevenLabsTTS] Could not delete " + path + ": " + e.Message);
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