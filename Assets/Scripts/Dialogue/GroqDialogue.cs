using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using System.Collections;
using System.Text;
using Newtonsoft.Json.Linq;
using ShinyMinds.Core;

// Runs before NPCInteraction (-50) and DoorController (0) so an open dialogue
// always wins the E press.
[DefaultExecutionOrder(-100)]
public class GroqDialogue : MonoBehaviour
{
    [Header("NPC Topic")]
    public string safetyTopic =
        "road crossing safety";

    [Header("Animators")]
    public Animator npcAnimator;
    public Animator girlAnimator;

    public ElevenLabsTTS tts;

    private string[] dialogueLines;
    private int currentLine = 0;

    [Header("UI")]
    public TMP_Text dialogueText;
    public TMP_Text speakerText;
    public TMP_Text continueText;
    public GameObject dialoguePanel;

    [Header("Player")]
    public MonoBehaviour playerController;

    [Header("Groq")]
    [TextArea]
    public string groqApiKey;

    private bool dialogueOpen = false;

    public bool IsDialogueOpen
    {
        get { return dialogueOpen; }
    }

    public void GenerateConversation()
    {
        StartCoroutine(GetConversation());
    }

    IEnumerator GetConversation()
    {
        dialoguePanel.SetActive(true);

        if (continueText != null)
        {
            continueText.text = "Generating...";
        }

        dialogueOpen = true;

        // Freezes PlayerController, CameraController, MapToggle and footstepaudio
        // together, and zeroes the locomotion animator params. See PlayerLockBinder.
        PlayerInputLock.Acquire(this);

        Debug.Log("TOPIC = " + safetyTopic);

        string topicRules = "";

        if (safetyTopic == "road crossing safety")
        {
            topicRules =
                "- Teach looking left and right.\n" +
                "- Teach using zebra crossings.\n" +
                "- Teach crossing safely.\n";
        }
        else if (safetyTopic == "school zone safety")
        {
            topicRules =
                "- Teach walking on sidewalks.\n" +
                "- Teach obeying crossing guards.\n" +
                "- Teach school bus safety.\n" +
                "- Do NOT discuss zebra crossings.\n";
        }
        else if (safetyTopic == "traffic light safety")
        {
            topicRules =
                "- Teach traffic lights.\n" +
                "- Teach pedestrian signals.\n" +
                "- Do NOT discuss school buses.\n";
        }
        else if (safetyTopic == "bus stop safety")
        {
            topicRules =
                "- Teach waiting for the bus.\n" +
                "- Teach boarding safely.\n" +
                "- Do NOT discuss traffic lights.\n";
        }

        string prompt =
            "Create a conversation between a little girl and a wise mentor.\n" +
            "Exactly 4 lines.\n" +
            "Format:\n" +
            "Girl:\n" +
            "NPC:\n" +
            "Girl:\n" +
            "NPC:\n\n" +

            "TOPIC: " + safetyTopic + "\n\n" +

            "IMPORTANT RULES:\n" +
            topicRules +
            "- Keep answers short.\n" +
            "- Suitable for a 9-year-old child.\n";

        JObject requestBody = new JObject(
            new JProperty("model", "llama-3.3-70b-versatile"),
            new JProperty("messages",
                new JArray(
                    new JObject(
                        new JProperty("role", "user"),
                        new JProperty("content", prompt)
                    )
                )
            )
        );

        UnityWebRequest request =
            new UnityWebRequest(
                "https://api.groq.com/openai/v1/chat/completions",
                "POST"
            );

        byte[] bodyRaw =
            Encoding.UTF8.GetBytes(requestBody.ToString());

        request.uploadHandler =
            new UploadHandlerRaw(bodyRaw);

        request.downloadHandler =
            new DownloadHandlerBuffer();

        request.SetRequestHeader(
            "Content-Type",
            "application/json"
        );

        request.SetRequestHeader(
            "Authorization",
            "Bearer " + groqApiKey
        );

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            try
            {
                JObject response =
                    JObject.Parse(
                        request.downloadHandler.text
                    );

                string content =
                    response["choices"][0]
                    ["message"]
                    ["content"]
                    .ToString();

                dialogueLines =
                    content.Split('\n');

                currentLine = 0;

                ShowCurrentLine();
            }
            catch (System.Exception ex)
            {
                Debug.LogError(ex.ToString());

                dialogueText.text =
                    "Failed to parse AI response.";
            }
        }
        else
        {
            dialogueText.text =
                "Groq Error:\n" +
                request.error;

            Debug.LogError(
                request.downloadHandler.text
            );
        }
    }

    void SetNPCTalking()
    {
        Debug.Log("NPC Animator = " +
         npcAnimator.gameObject.name);
        if (npcAnimator != null)
            npcAnimator.SetBool("IsTalking", true);

        if (girlAnimator != null)
            girlAnimator.SetBool("IsTalking", false);
    }

    void SetGirlTalking()
    {
        Debug.Log("Girl Animator = " +
                girlAnimator.gameObject.name);
        if (girlAnimator != null)
            girlAnimator.SetBool("IsTalking", true);

        if (npcAnimator != null)
            npcAnimator.SetBool("IsTalking", false);
    }

    void StopTalking()
    {
        if (npcAnimator != null)
            npcAnimator.SetBool("IsTalking", false);

        if (girlAnimator != null)
            girlAnimator.SetBool("IsTalking", false);
    }

    void ShowCurrentLine()
    {
        if (dialogueLines == null ||
            currentLine >= dialogueLines.Length)
        {
            return;
        }

        string line =
            dialogueLines[currentLine].Trim();

        if (line.StartsWith("Girl:"))
        {
            if (speakerText != null)
                speakerText.text = "Girl";

            dialogueText.text =
                line.Replace("Girl:", "").Trim();

            SetGirlTalking();

            if (continueText != null)
            {
                continueText.text =
                    "Speaking...";
            }

            if (tts != null)
            {
                tts.SpeakGirl(
                    dialogueText.text
                );
            }
        }
        else if (line.StartsWith("NPC:"))
        {
            if (speakerText != null)
                speakerText.text = "NPC";

            dialogueText.text =
                line.Replace("NPC:", "").Trim();

            SetNPCTalking();

            if (continueText != null)
            {
                continueText.text =
                    "Speaking...";
            }

            if (tts != null)
            {
                tts.SpeakNPC(
                    dialogueText.text
                );
            }
        }
        else
        {
            if (speakerText != null)
                speakerText.text = "";

            dialogueText.text = line;

            StopTalking();

            if (continueText != null)
            {
                continueText.text =
                    "E = Next | F = Leave";
            }
        }
    }

    public void CloseDialogue()
    {
        dialoguePanel.SetActive(false);
        dialogueOpen = false;

        StopTalking();

        PlayerInputLock.Release(this);
    }

    void Update()
    {
        if (!dialoguePanel.activeSelf)
            return;

        if (
            (tts == null || !tts.IsSpeaking)
            &&
            InteractKey.TryConsumeUI()
        )
        {
            currentLine++;

            if (currentLine < dialogueLines.Length)
            {
                ShowCurrentLine();
            }
            else
            {
                CloseDialogue();
            }
        }

        if (Input.GetKeyDown(KeyCode.F))
        {
            CloseDialogue();
        }
    }

    IEnumerator WaitForSpeechToFinish()
    {
        if (continueText != null)
        {
            continueText.text =
                "Speaking...";
        }

        while (
            tts != null &&
            tts.IsSpeaking
        )
        {
            yield return null;
        }

        StopTalking();

        if (continueText != null)
        {
            continueText.text =
                "E = Next    |    F = Leave";
        }
    }

    void Start()
    {
        if (tts != null)
        {
            tts.OnSpeechFinished += HandleSpeechFinished;
        }
    }

    void HandleSpeechFinished()
    {
        StopTalking();

        if (continueText != null)
        {
            continueText.text =
                "E = Next | F = Leave";
        }
    }
}