// ===============================================
// Utilised by:
// This script was developed by referencing and combining multiple publicly available Unity and API integration examples:
// 
// - The structure for sending JSON via POST using UnityWebRequest in a coroutine was adapted from this Unity-focused blog:
//   https://blog.csdn.net/weixin_38484443/article/details/117434855
//
// - The coroutine pattern (IEnumerator + StartCoroutine) for asynchronous workflows follows Unity’s official coroutine documentation:
//   https://docs.unity3d.com/Manual/Coroutines.html
//https://huggingface.co/docs/transformers/v4.17.0/preprocessing
// - JSON (de)serialization using UnityEngine.JsonUtility, and the technique of wrapping array responses for successful parsing, is informed by this Unity tutorial:
//   https://gamedevbeginner.com/json-and-unity-how-to-savetoload-your-game-data/
//
// - The use of C# Action<T> delegates for success and error callbacks is based on Microsoft’s documentation and Unity event handling practices:
//   https://learn.microsoft.com/en-us/dotnet/api/system.action-1
//
// - Retry-on-503 logic (with WaitForSeconds and recursive coroutine calls) is custom logic inspired by patterns discussed on Unity forums and general resilient API request techniques:
//
//   (e.g. retry discussion: https://forum.unity.com/threads/simple-retry-logic-for-unitywebrequest.1025994/)
//
// - API integration details like sending an "Authorization: Bearer" header and using "x-wait-for-model" are derived from Hugging Face’s Unity API usage and documentation:
//   https://huggingface.co/docs/api-inference/index
//   https://github.com/huggingface/unity-api
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



    public void SetApiKey(string key)
    {
        apiKey = key.Trim();
    }


    public void DetectEmotion(string text, Action<string> onSuccess, Action<string> onError)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            onError?.Invoke("API key is missing. Please set it in the Inspector or call SetApiKey().");
            return;
        }

        StartCoroutine(SendRequest(text, onSuccess, onError));
    }

   
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

