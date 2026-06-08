using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using System.Collections;
using System.Text;
using Newtonsoft.Json.Linq;

public class GroqDialogue : MonoBehaviour
{
    [Header("Animators")]
    public Animator npcAnimator;
    public Animator girlAnimator;

    private string[] dialogueLines;
    private int currentLine = 0;

    [Header("UI")]
    public TMP_Text dialogueText;
    public TMP_Text speakerText;
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
        dialogueOpen = true;

        if (playerController != null)
        {
            playerController.enabled = false;
        }

        string prompt =
            "Create a short conversation between a little girl and a wise road safety mentor. " +
            "Exactly 4 lines. " +
            "Format: Girl:, NPC:, Girl:, NPC:. " +
            "Teach one road safety lesson.";

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
        if (npcAnimator != null)
            npcAnimator.SetBool("IsTalking", true);

        if (girlAnimator != null)
            girlAnimator.SetBool("IsTalking", false);
    }

    void SetGirlTalking()
    {
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
            speakerText.text = "Girl";

            dialogueText.text =
                line.Replace("Girl:", "").Trim();

            SetGirlTalking();
        }
        else if (line.StartsWith("NPC:"))
        {
            speakerText.text = "NPC";

            dialogueText.text =
                line.Replace("NPC:", "").Trim();

            SetNPCTalking();
        }
        else
        {
            speakerText.text = "";

            dialogueText.text = line;

            StopTalking();
        }
    }

    public void CloseDialogue()
    {
        dialoguePanel.SetActive(false);
        dialogueOpen = false;

        StopTalking();

        if (playerController != null)
        {
            playerController.enabled = true;
        }
    }

    void Update()
    {
        if (!dialoguePanel.activeSelf)
            return;

        if (Input.GetKeyDown(KeyCode.E))
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
}