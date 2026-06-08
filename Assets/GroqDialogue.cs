using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using System.Collections;
using System.Text;

public class GroqDialogue : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text dialogueText;
    public GameObject dialoguePanel;

    [Header("Player")]
    public MonoBehaviour PlayerController;

    [Header("Groq")]
    [TextArea]
    public string groqApiKey;

    public void GenerateAdvice()
    {
        StartCoroutine(GetAdvice());
    }

    IEnumerator GetAdvice()
    {
        dialoguePanel.SetActive(true);

        if (PlayerController != null)
        {
            PlayerController.enabled = false;
        }

        string prompt =
            "You are a wise old mentor. "
            + "Give one short road safety advice "
            + "to a little girl in 2 sentences only.";

        string json =
        "{"
        + "\"model\":\"llama-3.3-70b-versatile\","
        + "\"messages\":["
        + "{"
        + "\"role\":\"user\","
        + "\"content\":\"" + prompt + "\""
        + "}"
        + "]"
        + "}";

        UnityWebRequest request =
            new UnityWebRequest(
                "https://api.groq.com/openai/v1/chat/completions",
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
            "Authorization",
            "Bearer " + groqApiKey
        );

        yield return request.SendWebRequest();

        if (request.result ==
            UnityWebRequest.Result.Success)
        {
            string response =
                request.downloadHandler.text;

            Debug.Log(response);

            int start =
                response.IndexOf("\"content\":\"");

            if (start != -1)
            {
                start += 11;

                int end =
                    response.IndexOf("\"", start);

                string advice =
                    response.Substring(
                        start,
                        end - start
                    );

                advice = advice.Replace("\\n", "\n");
                advice = advice.Replace("\\\"", "\"");

                dialogueText.text = advice;
            }
            else
            {
                dialogueText.text =
                    "Could not read AI response.";
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

    public void CloseDialogue()
    {
        dialoguePanel.SetActive(false);

        if (PlayerController != null)
        {
            PlayerController.enabled = true;
        }
    }

    void Update()
    {
        if (dialoguePanel != null &&
            dialoguePanel.activeSelf &&
            Input.GetKeyDown(KeyCode.Space))
        {
            CloseDialogue();
        }
    }
}