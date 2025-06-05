// ===============================================
// Inspired by:
// - UnityWebRequest for sending HTTP requests
// - Coroutines (IEnumerator + StartCoroutine) for handling async work
// - Hugging Face Inference API patterns for model calls
// - UnityEngine.JsonUtility for JSON (de)serialization
// - C# Action<T> callbacks for passing results/errors
// ===============================================
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Sends text to a Hugging Face emotion-detection model and returns the predicted label.

/// </summary>
public class EmotionDetectionAPI : MonoBehaviour
{
    [Header("Hugging Face Settings")]
    [Tooltip("Your API token for Hugging Face inference.")]
    [SerializeField]
    private string apiKey;

    [Tooltip("Model endpoint, e.g. 'j-hartmann/emotion-english-distilroberta-base'.")]
    [SerializeField]
    private string modelId = "j-hartmann/emotion-english-distilroberta-base";

    // Builds the full URL to the inference endpoint based on modelId
    private string ApiUrl => $"https://api-inference.huggingface.co/models/{modelId}";


    /// <summary>
    /// Call this from other scripts if you want to set the API key at runtime.
    /// </summary>
    public void SetApiKey(string key)
    {
        apiKey = key.Trim();
    }

    /// <summary>
    /// Starts the emotion detection process. 
    /// onSuccess is invoked with the top emotion label, onError with any error message.
    /// </summary>
    public void DetectEmotion(string text, Action<string> onSuccess, Action<string> onError)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            onError?.Invoke("API key is missing. Please set it in the Inspector or call SetApiKey().");
            return;
        }

        StartCoroutine(SendRequest(text, onSuccess, onError));
    }

    /// <summary>
    /// Sends the text to the Hugging Face model. Retries once if the model is still loading (503).
    /// </summary>
    private IEnumerator SendRequest(string text, Action<string> onSuccess, Action<string> onError, int attempt = 1)
    {
        // Prepare JSON: {"text": "your input"}
        var requestBody = new TextPayload { text = text };
        string json = JsonUtility.ToJson(requestBody);
        Debug.Log($"[EmotionAPI] Sending payload: {json}");

        using var request = new UnityWebRequest(ApiUrl, "POST")
        {
            uploadHandler   = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json)),
            downloadHandler = new DownloadHandlerBuffer()
        };
        request.SetRequestHeader("Content-Type",  "application/json");
        request.SetRequestHeader("Authorization", "Bearer " + apiKey);
        request.SetRequestHeader("x-wait-for-model", "true"); // Wait if the model is still warming up

        yield return request.SendWebRequest();

        string responseText = request.downloadHandler?.text;
        Debug.Log($"[EmotionAPI] HTTP {(int)request.responseCode}: {responseText}");

        // If the model is not ready yet, try one more time after 1 second
        if (request.responseCode == 503 && attempt < 2)
        {
            Debug.LogWarning("[EmotionAPI] Model still loading (503). Retrying in 1 second...");
            yield return new WaitForSeconds(1f);
            StartCoroutine(SendRequest(text, onSuccess, onError, attempt + 1));
            yield break;
        }

        // Handle HTTP errors
        if (request.result != UnityWebRequest.Result.Success)
        {
            switch (request.responseCode)
            {
                case 400:
                    onError?.Invoke($"Bad request (400): {responseText}");
                    break;
                case 401:
                    onError?.Invoke("Authentication failed (401). Check your API key.");
                    break;
                case 403:
                    onError?.Invoke($"Access forbidden (403). Your API key may not have permission for '{modelId}'.");
                    break;
                default:
                    onError?.Invoke($"Error {(int)request.responseCode}: {request.error}");
                    break;
            }
            yield break;
        }

        // Hugging Face returns an array of { "label": "...", "score": ... }
    
        var wrappedJson = "{\"data\":" + responseText + "}";
        var parsed = JsonUtility.FromJson<EmotionArray>(wrappedJson);

        if (parsed?.data == null || parsed.data.Length == 0)
        {
            onError?.Invoke("No emotion data received from the model.");
            yield break;
        }

        // Log the received emotions
        Debug.Log($"[EmotionAPI] Received {parsed.data.Length} labels:");
        foreach (var e in parsed.data)
        {
            Debug.Log($"  {e.label}: {e.score}");
        }

        // Find the emotion with the highest score
        var bestEmotion = parsed.data[0];
        foreach (var e in parsed.data)
        {
            if (e.score > bestEmotion.score)
                bestEmotion = e;
        }

        Debug.Log($"[EmotionAPI] Top emotion: {bestEmotion.label} (score {bestEmotion.score})");
        onSuccess?.Invoke(bestEmotion.label);
    }

    // Simple classes to match the HF JSON structure
    [Serializable]
    private class TextPayload
    {
        public string text;
    }

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

