// https://github.com/huggingface/unity-api
//https://developers.meta.com/horizon/documentation/unity/voice-sdk-tutorials-3/
// https://git.gti.ssr.upm.es/mgm/ModelosEscalera/-/tree/TerapiaEscalerasOculusSDK/Library/PackageCache/com.unity.xr.interaction.toolkit@2.0.1/Documentation~?ref_type=heads
//https://git.gti.ssr.upm.es/mgm/ModelosEscalera/-/tree/TerapiaEscalerasOculusSDK/Library/PackageCache/com.unity.xr.interaction.toolkit@2.0.1/Documentation~?ref_type=heads
//https://github.com/Cysharp/ZLinq
//https://yang-su2000.github.io/Voice2Action/
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit.Samples.SpatialKeyboard;

public class FlipPhoneChatXR : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI chatLog;
    [SerializeField] private XRKeyboardDisplay keyboardDisplay;
    [SerializeField] private TMP_InputField inputField;

    [Header("Hugging Face Settings")]
    [Tooltip("Try DialoGPT-medium for better consistency")]
    [SerializeField] private string modelId = "microsoft/DialoGPT-medium";
    [Tooltip("Your HF Inference API token")]
    [SerializeField] private string apiKey  = "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";

    private List<string> history = new List<string>();

    private void Awake()
    {
        // Wire XRI keyboard display to the TMP input field
        if (keyboardDisplay != null && inputField != null)
        {
            keyboardDisplay.inputField = inputField;
            keyboardDisplay.onTextSubmitted.AddListener(OnTextSubmitted);
        }

 
        AppendLog("<b>Friend:</b> do u miss early justin beiber");
        history.Add("Friend: do u miss early justin beiber");

   
        inputField.ActivateInputField();
    }

    private void OnDestroy()
    {
        if (keyboardDisplay != null)
            keyboardDisplay.onTextSubmitted.RemoveListener(OnTextSubmitted);
    }

    private void OnTextSubmitted(string rawText)
    {
        string trimmed = rawText?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return;

        AppendLog($"<b>You:</b> {trimmed}");
        history.Add($"You: {trimmed}");

        inputField.text = string.Empty;
        StartCoroutine(SendToHuggingFace());
    }

    private IEnumerator SendToHuggingFace()
    {
   
        if (history.Count > 6)
            history = history.GetRange(history.Count - 6, 6);

    
        var sb = new StringBuilder();
        sb.AppendLine("You are a 2000s girl texting on a flip-phone to a friend . Reply in a nostalgic, fun style talk about music, clubbing, Jeresey Shore  etc.");
        sb.AppendLine("Ensure your reply is between 3 and 6 words, using 2000s slang.");
        sb.AppendLine();
        foreach (var line in history)
            sb.AppendLine(line);
        sb.Append("Friend:");
        string prompt = sb.ToString();

      
        string esc = prompt
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n");

       
        string json = "{"
            + "\"inputs\":\"" + esc + "\"," 
            + "\"parameters\":{"
            +     "\"max_new_tokens\":64,"
            +     "\"truncation\":\"only_last\","
            +     "\"stop\":[\"You:\"]"
            + "}"
            + "}";

        using var www = new UnityWebRequest($"https://api-inference.huggingface.co/models/{modelId}", "POST");
        www.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        www.downloadHandler = new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type",  "application/json");
        www.SetRequestHeader("Authorization", "Bearer " + apiKey);

        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            AppendLog($"<color=red>System: Error {www.responseCode}</color>");
        }
        else
        {
            var raw = www.downloadHandler.text;
            HFArray wrapper = JsonUtility.FromJson<HFArray>("{\"arr\":" + raw + "}");

            if (wrapper?.arr == null || wrapper.arr.Length == 0)
            {
                AppendLog("<color=red>System: No reply</color>");
            }
            else
            {
             
                string full = wrapper.arr[0].generated_text;
                int idx = full.LastIndexOf("Friend:") + "Friend:".Length;
                var body = (idx > 0 && idx < full.Length)
                    ? full.Substring(idx).Trim()
                    : full.Trim();

                var words = body
                    .Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries)
                    .ToList();

                // Truncate to max 6 words
                if (words.Count > 6)
                    words = words.GetRange(0, 6);

             
                var fillers = new[] {"omg", "lol", "<3", "haha", "yeet", "tbh", "rad", "epic"};
                // Randomly insert or append fillers until >= 3 words
                while (words.Count < 3)
                {
                    int pos = UnityEngine.Random.Range(0, words.Count + 1);
                    words.Insert(pos, fillers[UnityEngine.Random.Range(0, fillers.Length)]);
                }


                if (words.Count < 6 && UnityEngine.Random.value < 0.5f)
                    words.Insert(UnityEngine.Random.Range(0, words.Count + 1), fillers[UnityEngine.Random.Range(0, fillers.Length)]);

                var reply = string.Join(" ", words);

                AppendLog($"<b>Friend:</b> {reply}");
                history.Add($"Friend: {reply}");
            }
        }

        inputField.ActivateInputField();
    }

    private void AppendLog(string line)
    {
        chatLog.text += "\n" + line;
    }

    [Serializable]
    private class HFArray { public HFItem[] arr; }
    [Serializable]
    private class HFItem  { public string generated_text; }
}



