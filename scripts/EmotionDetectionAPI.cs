// ===============================================
// Influences:
// - HTTP communication via UnityWebRequest and handlers
// - Asynchronous task management using Coroutines (IEnumerator + StartCoroutine)
// - Pattern for Model inference calls inspired by Hugging Face Inference API
// - JSON serialization/deserialization with UnityEngine.JsonUtility
// - Use of System.Action<T> delegates for callback handling
// - C# coding conventions and Unity style best practices
// ===============================================
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

// This MonoBehaviour handles communication with the Hugging Face emotion detection model.
// It sends text payloads and parses the returned emotion scores.
public class EmotionDetectionAPI : MonoBehaviour
{
    [Header("Hugging Face")]
    [Tooltip("Your HF Inference API token")]
    [SerializeField]
    private string apiKey; // Store your inference API token here via the Inspector or SetApiKey()

    [Tooltip("The model to call, e.g. j-hartmann/emotion-english-distilroberta-base")]
    [SerializeField]
    private string modelId = "j-hartmann/emotion-english-distilroberta-base"; // Default model endpoint

    // Construct the full API URL based on the chosen model
    private string ApiUrl => $"https://api-inference.huggingface.co/models/{modelId}";

    /// <summary>
    /// Allows other scripts (like SpeechRecognitionTest) to set the API key at runtime.
    /// </summary>
    public void SetApiKey(string key)
    {
        apiKey = key; // Store the provided token
    }

    /// <summary>
    /// Public entry point: submit text to detect emotion.
    /// onSuccess will receive the top emotion label,
    /// onError will receive an error message if something goes wrong.
    /// </summary>
    public void DetectEmotion(string text, Action<string> onSuccess, Action<string> onError)
    {
        // Ensure we have a token before attempting the request
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            onError?.Invoke("No API key set: please paste your HF token in the Inspector or use SetApiKey().");
            return;
        }

        // Start the coroutine to send the HTTP request
        StartCoroutine(SendRequest(text, onSuccess, onError));
    }

    // Coroutine to perform the HTTP POST to the HF inference endpoint
    private IEnumerator SendRequest(string text, Action<string> onSuccess, Action<string> onError, int attempt = 1)
    {
        // Prepare the JSON payload (simple { "text": "..." })
        var requestData = new TextRequest { text = text };
        string payload = JsonUtility.ToJson(requestData);
        Debug.Log($"[EmotionAPI] Payload: {payload}");

        // Create the UnityWebRequest for a JSON POST
        using var req = new UnityWebRequest(ApiUrl, "POST")
        {
            uploadHandler   = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(payload)),
            downloadHandler = new DownloadHandlerBuffer()
        };
        req.SetRequestHeader("Content-Type",  "application/json");
        req.SetRequestHeader("Authorization", "Bearer " + apiKey);
        req.SetRequestHeader("x-wait-for-model", "true"); // Wait if model is still loading

        yield return req.SendWebRequest(); // Actually send it and wait

        string responseBody = req.downloadHandler?.text;
        Debug.Log($"[EmotionAPI] HTTP {req.responseCode}: {responseBody}");

        // If the service is loading, retry once after a short wait
        if (req.responseCode == 503 && attempt < 2)
        {
            Debug.LogWarning("[EmotionAPI] Service unavailable (503), retrying in 1s...");
            yield return new WaitForSeconds(1f);
            StartCoroutine(SendRequest(text, onSuccess, onError, attempt + 1));
            yield break;
        }

        // Handle HTTP errors: bad request, auth issues, forbidden, etc.
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

        // Hugging Face returns an array of {label,score}, so wrap for JsonUtility
        var wrapped = "{\"data\":" + responseBody + "}";
        var wrapper = JsonUtility.FromJson<EmotionArray>(wrapped);

        // If no data returned, forward an error
        if (wrapper?.data == null || wrapper.data.Length == 0)
        {
            onError?.Invoke("No emotion data returned.");
            yield break;
        }

        // Log all detected emotions for debugging
        Debug.Log($"[EmotionAPI] Parsed {wrapper.data.Length} emotions:");
        foreach (var emo in wrapper.data)
            Debug.Log($"[EmotionAPI]   {emo.label}: {emo.score}");

        // Find the highest-scoring emotion
        var best = wrapper.data[0];
        for (int i = 1; i < wrapper.data.Length; i++)
            if (wrapper.data[i].score > best.score)
                best = wrapper.data[i];

        Debug.Log($"[EmotionAPI] Best emotion: {best.label} ({best.score})");
        onSuccess?.Invoke(best.label); // Return the top label
    }

    // Helper class for JsonUtility to serialize the request
    [Serializable]
    private class TextRequest { public string text; }

    // Represents one emotion entry from the API response
    [Serializable]
    private class Emotion
    {
        public string label; // e.g. "joy", "sadness"
        public float score;  // confidence from 0.0 to 1.0
    }

    // Wrapper for the array of Emotion objects
    [Serializable]
    private class EmotionArray
    {
        public Emotion[] data;
    }
}
