using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class EmotionDetectionAPI : MonoBehaviour
{
    [Header("Hugging Face")]
    [Tooltip("Your HF Inference API token")]
    [SerializeField] private string apiKey;

    [Tooltip("The model to call, e.g. j-hartmann/emotion-english-distilroberta-base")]
    [SerializeField] private string modelId = "j-hartmann/emotion-english-distilroberta-base";

    private string ApiUrl => $"https://api-inference.huggingface.co/models/{modelId}";

    /// <summary>
    /// Allows other scripts (e.g. SpeechRecognitionTest) to supply the HF token at runtime.
    /// </summary>
    public void SetApiKey(string key)
    {
        apiKey = key;
    }

    /// <summary>
    /// Always invoke the emotion detection endpoint, forwarding whatever ASR returned.
    /// </summary>
    public void DetectEmotion(string text, Action<string> onSuccess, Action<string> onError)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            onError?.Invoke("No API key set: please paste your HF token in the Inspector or use SetApiKey().");
            return;
        }

        StartCoroutine(SendRequest(text, onSuccess, onError));
    }

    private IEnumerator SendRequest(string text, Action<string> onSuccess, Action<string> onError, int attempt = 1)
    {
        // Prepare JSON payload with 'text' field as required by HF
        var requestData = new TextRequest { text = text };
        string payload = JsonUtility.ToJson(requestData);
        Debug.Log($"[EmotionAPI] Payload: {payload}");

        using var req = new UnityWebRequest(ApiUrl, "POST")
        {
            uploadHandler   = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(payload)),
            downloadHandler = new DownloadHandlerBuffer()
        };
        req.SetRequestHeader("Content-Type",  "application/json");
        req.SetRequestHeader("Authorization", "Bearer " + apiKey);
        req.SetRequestHeader("x-wait-for-model", "true");

        yield return req.SendWebRequest();

        string responseBody = req.downloadHandler?.text;
        Debug.Log($"[EmotionAPI] HTTP {req.responseCode}: {responseBody}");

        // Retry once on 503 (model is loading)
        if (req.responseCode == 503 && attempt < 2)
        {
            Debug.LogWarning("[EmotionAPI] Service unavailable (503), retrying in 1s...");
            yield return new WaitForSeconds(1f);
            StartCoroutine(SendRequest(text, onSuccess, onError, attempt + 1));
            yield break;
        }

        if (req.result != UnityWebRequest.Result.Success)
        {
            switch (req.responseCode)
            {
                case 400:
                    onError?.Invoke($"Bad request (400): {responseBody}");
                    break;
                case 401:
                    onError?.Invoke("Auth failed: check your API key in the Inspector or via SetApiKey().");
                    break;
                case 403:
                    onError?.Invoke($"Access forbidden: your token may lack permission for model '{modelId}'.");
                    break;
                default:
                    onError?.Invoke($"API Error ({req.responseCode}): {req.error}");
                    break;
            }
            yield break;
        }

        // Wrap the raw response array in {"data": ...} for deserialization
        var wrapped = "{\"data\":" + responseBody + "}";
        var wrapper = JsonUtility.FromJson<EmotionArray>(wrapped);

        if (wrapper?.data == null || wrapper.data.Length == 0)
        {
            onError?.Invoke("No emotion data returned.");
            yield break;
        }

        // Log all returned emotions
        Debug.Log($"[EmotionAPI] Parsed {wrapper.data.Length} emotions:");
        foreach (var emo in wrapper.data)
            Debug.Log($"[EmotionAPI]   {emo.label}: {emo.score}");

        // Pick highest-scoring emotion
        var best = wrapper.data[0];
        for (int i = 1; i < wrapper.data.Length; i++)
            if (wrapper.data[i].score > best.score)
                best = wrapper.data[i];

        Debug.Log($"[EmotionAPI] Best emotion: {best.label} ({best.score})");
        onSuccess?.Invoke(best.label);
    }

    [Serializable]
    private class TextRequest { public string text; }

    [Serializable]
    private class Emotion
    {
        public string label;
        public float score;
    }

    [Serializable]
    private class EmotionArray
    {
        public Emotion[] data;
    }
}




