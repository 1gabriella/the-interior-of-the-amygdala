// ===============================================
// Influences & Inspirations:
// - UnityEngine.Microphone API for capturing real-time audio input
// - Hugging Face Inference API patterns for long-poll ASR requests
// - UnityWebRequest with raw audio payloads for HTTP multipart
// - Coroutines (IEnumerator + StartCoroutine) for asynchronous workflows
// - JsonUtility for parsing JSON transcription responses
// - MemoryStream & BinaryWriter for WAV encoding of PCM samples
// - UI feedback patterns using TextMeshPro and UnityEngine.UI.Button
// - Event-driven input handling via Button.onClick listeners
// - Error-handling and retry logic inspired by resilient network programming
// ===============================================

using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;

public class SpeechRecognitionTest : MonoBehaviour
{
    [Header("UI Buttons")]
    [Tooltip("Click to begin recording from the mic")]
    [SerializeField] private Button startButton;  // Triggers audio record start

    [Tooltip("Click to end recording and process audio")]
    [SerializeField] private Button stopButton;   // Triggers audio record stop

    [Header("UI Feedback")]
    [SerializeField] private TextMeshProUGUI statusText; // Displays status and results

    [Header("Hugging Face Settings")]
    [Tooltip("Your HF Inference API token")]
    [SerializeField] private string apiKey = ""; // Fill via Inspector or SetApiKey

    [Tooltip("Use facebook/wav2vec2-base-960h for ASR")]
    [SerializeField] private string asrModelId = "facebook/wav2vec2-base-960h"; // HF ASR model

    // Internal recording state
    private AudioClip clip;        // Captured audio clip
    private byte[] wavData;        // Encoded WAV bytes
    private bool recording;        // Recording flag

    // Emotion detection helper
    private EmotionDetectionAPI emotionAPI; // Reference to EmotionDetectionAPI component

    private void Start()
    {
        // Ensure EmotionDetectionAPI is available and set its API key
        emotionAPI = GetComponent<EmotionDetectionAPI>() ?? gameObject.AddComponent<EmotionDetectionAPI>();
        emotionAPI.SetApiKey(apiKey);

        // Register button callbacks
        startButton.onClick.AddListener(OnStartClicked);
        stopButton.onClick.AddListener(OnStopClicked);

        // Initialize button states and status text
        startButton.interactable = true;
        stopButton.interactable  = false;
        UpdateStatus("Ready to record.", Color.white);
    }

    private void OnStartClicked()
    {
        if (recording) return; // Prevent double-start

        // Begin recording from default microphone
        clip = Microphone.Start(null, false, 10, 44100);
        recording = true;

        // Update UI interactivity and feedback
        startButton.interactable = false;
        stopButton.interactable  = true;
        UpdateStatus("Recording speech…", Color.white);
    }

    private void OnStopClicked()
    {
        if (!recording) return; // Prevent stopping when not recording

        // Capture current position and stop microphone
        int pos = Microphone.GetPosition(null);
        Microphone.End(null);
        recording = false;

        // Extract float samples and encode to WAV bytes
        var samples = new float[pos * clip.channels];
        clip.GetData(samples, 0);
        wavData = EncodeAsWAV(samples, clip.frequency, clip.channels);

        // Restore UI interactivity and start processing
        startButton.interactable = true;
        stopButton.interactable  = false;
        StartCoroutine(ProcessRecording());
    }

    private IEnumerator ProcessRecording()
    {
        // Show processing feedback
        UpdateStatus("Processing speech…", Color.yellow);

        bool done = false;
        // Send WAV to ASR endpoint asynchronously
        StartCoroutine(SendForASR(wavData,
            transcription => {
                // OnSuccess: update transcription, then run emotion detection
                UpdateStatus("Transcription: " + transcription, Color.white);
                emotionAPI.DetectEmotion(
                    transcription,
                    emo => UpdateStatus("Emotion: " + emo, Color.green),
                    err => UpdateStatus("Emotion error: " + err, Color.red)
                );
                done = true;
            },
            err => {
                // OnError: show error
                UpdateStatus(err, Color.red);
                done = true;
            }
        ));

        // Wait until ASR + emotion finish
        while (!done)
            yield return null;
    }

    // Coroutine to post WAV bytes to Hugging Face ASR API
    private IEnumerator SendForASR(byte[] wavData, Action<string> onSuccess, Action<string> onError, int attempt = 1)
    {
        string url = $"https://api-inference.huggingface.co/models/{asrModelId}";

        // Logging request details
        Debug.Log($"[ASR] POST to {url}, size = {wavData.Length} bytes");
        string riff = System.Text.Encoding.ASCII.GetString(wavData, 0, 4);
        Debug.Log($"[ASR] WAV header: '{riff}'");

        using var www = new UnityWebRequest(url, "POST") {
            uploadHandler   = new UploadHandlerRaw(wavData),
            downloadHandler = new DownloadHandlerBuffer()
        };
        www.SetRequestHeader("Authorization", $"Bearer {apiKey}");
        www.SetRequestHeader("Content-Type", "audio/wav");
        www.SetRequestHeader("x-wait-for-model", "true"); // Block until model loads

        yield return www.SendWebRequest();

        string responseBody = www.downloadHandler?.text;
        Debug.LogError($"[ASR] HTTP {(int)www.responseCode}: {responseBody}");

        // Retry on service unavailable
        if (www.responseCode == 503 && attempt < 2)
        {
            Debug.LogWarning("[ASR] Service unavailable, retrying...");
            yield return new WaitForSeconds(1f);
            StartCoroutine(SendForASR(wavData, onSuccess, onError, attempt + 1));
            yield break;
        }

        // Error handling
        if (www.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke($"ASR Error {(int)www.responseCode}: {responseBody}");
            yield break;
        }

        // Parse transcription JSON
        try
        {
            var wrap = JsonUtility.FromJson<TranscriptionWrapper>(responseBody);
            onSuccess?.Invoke(wrap.text);
        }
        catch (Exception ex)
        {
            Debug.LogError("[ASR] JSON parse error: " + ex);
            onError?.Invoke("ASR parse error");
        }
    }

    // Update the status text and color on the UI
    private void UpdateStatus(string message, Color color)
    {
        if (statusText != null)
        {
            statusText.text  = message;
            statusText.color = color;
        }
    }

    // Convert float PCM samples to WAV byte array (RIFF header + PCM data)
    private byte[] EncodeAsWAV(float[] samples, int frequency, int channels)
    {
        using var ms = new MemoryStream(44 + samples.Length * 2);
        using var writer = new BinaryWriter(ms);
        // RIFF header
        writer.Write("RIFF".ToCharArray());
        writer.Write(36 + samples.Length * 2);
        writer.Write("WAVE".ToCharArray());
        writer.Write("fmt ".ToCharArray());
        writer.Write(16);
        writer.Write((ushort)1);
        writer.Write((ushort)channels);
        writer.Write(frequency);
        writer.Write(frequency * channels * 2);
        writer.Write((ushort)(channels * 2));
        writer.Write((ushort)16);
        // Data chunk
        writer.Write("data".ToCharArray());
        writer.Write(samples.Length * 2);
        foreach (var s in samples)
            writer.Write((short)(s * short.MaxValue));
        return ms.ToArray();
    }

    [Serializable]
    private class TranscriptionWrapper { public string text; }
}
