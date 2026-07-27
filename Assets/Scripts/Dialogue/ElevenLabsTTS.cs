using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text;
using System.IO;

public class ElevenLabsTTS : MonoBehaviour
{
    [Header("Audio")]
    public AudioSource audioSource;
    public bool IsSpeaking { get; private set; }
    public delegate void SpeechFinished();
    public event SpeechFinished OnSpeechFinished;

    [Header("API")]
    [TextArea]
    public string elevenLabsApiKey;

    [Header("Voice IDs")]
    public string npcVoiceId;
    public string girlVoiceId;

    // NPC voice
    public void SpeakNPC(string text)
    {
        StartCoroutine(
            GenerateSpeech(
                text,
                npcVoiceId
            )
        );
    }

    // Girl voice
    public void SpeakGirl(string text)
    {
        StartCoroutine(
            GenerateSpeech(
                text,
                girlVoiceId
            )
        );
    }

    IEnumerator GenerateSpeech(
        string text,
        string voiceId
    )
    {
        string url =
            "https://api.elevenlabs.io/v1/text-to-speech/"
            + voiceId;

        string json =
        "{"
        + "\"text\":\"" + text + "\","
        + "\"model_id\":\"eleven_multilingual_v2\""
        + "}";

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
                request.downloadHandler.text
            );
        }
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
                audioRequest.error
            );
        }
    }
}