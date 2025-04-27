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
    [SerializeField] private Button startButton;

    [Tooltip("Click to end recording and process audio")]
    [SerializeField] private Button stopButton;

    [Header("UI Feedback")]
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("Hugging Face Settings")]
    [Tooltip("Your HF Inference API token")]  
    [SerializeField] private string apiKey = "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";

    [Tooltip("Use facebook/wav2vec2-base-960h for ASR")]
    [SerializeField] private string asrModelId = "facebook/wav2vec2-base-960h";

    // Internal recording state
    private AudioClip clip;
    private byte[] wavData;
    private bool recording;

    // Emotion detection helper
    private EmotionDetectionAPI emotionAPI;

    private void Start()
    {
       emotionAPI = GetComponent<EmotionDetectionAPI>() ?? gameObject.AddComponent<EmotionDetectionAPI>();
emotionAPI.SetApiKey(apiKey);

          emotionAPI.SetApiKey(apiKey);

    startButton.onClick.AddListener(OnStartClicked);
    stopButton.onClick.AddListener(OnStopClicked);
      
        startButton.interactable = true;
        stopButton.interactable  = false;
        UpdateStatus("Ready to record.", Color.white);
    }

    private void OnStartClicked()
    {
        if (recording) return;
        clip = Microphone.Start(null, false, 10, 44100);
        recording = true;

        startButton.interactable = false;
        stopButton.interactable  = true;
        UpdateStatus("Recording speech…", Color.white);
    }

    private void OnStopClicked()
    {
        if (!recording) return;
        int pos = Microphone.GetPosition(null);
        Microphone.End(null);
        recording = false;

        var samples = new float[pos * clip.channels];
        clip.GetData(samples, 0);
        wavData = EncodeAsWAV(samples, clip.frequency, clip.channels);

        startButton.interactable = true;
        stopButton.interactable  = false;

        StartCoroutine(ProcessRecording());
    }

    private IEnumerator ProcessRecording()
    {
        UpdateStatus("Processing speech…", Color.yellow);

        bool done = false;
        // Call custom ASR coroutine
        StartCoroutine(SendForASR(wavData,
            transcription =>
            {
                UpdateStatus("Transcription: " + transcription, Color.white);

                // Now emotion detection
                emotionAPI.DetectEmotion(
                    transcription,
                    emo => UpdateStatus("Emotion: " + emo, Color.green),
                    err => UpdateStatus("Emotion error: " + err, Color.red)
                );
                done = true;
            },
            err =>
            {
                UpdateStatus(err, Color.red);
                done = true;
            }
        ));

        while (!done)
            yield return null;
    }

   private IEnumerator SendForASR(byte[] wavData, Action<string> onSuccess, Action<string> onError, int attempt = 1)
{
    string url = $"https://api-inference.huggingface.co/models/{asrModelId}";

    // ——— LOGGING ———
    Debug.Log($"[ASR] POST to {url}, payload size = {wavData.Length} bytes");
    // check the first four bytes are “RIFF”
    string riff = System.Text.Encoding.ASCII.GetString(wavData, 0, 4);
    Debug.Log($"[ASR] WAV header: “{riff}”");

    using var www = new UnityWebRequest(url, "POST")
    {
        uploadHandler   = new UploadHandlerRaw(wavData),
        downloadHandler = new DownloadHandlerBuffer()
    };
    www.SetRequestHeader("Authorization", $"Bearer {apiKey}");
    www.SetRequestHeader("Content-Type", "audio/wav");
    www.SetRequestHeader("x-wait-for-model", "true");

    yield return www.SendWebRequest();

    // grab the full response body for inspection
    string responseBody = www.downloadHandler?.text;
    Debug.LogError($"[ASR] HTTP {(int)www.responseCode}\n{responseBody}");

    // Retry once on 503
    if (www.responseCode == 503 && attempt < 2)
    {
        Debug.LogWarning("[ASR] Service unavailable, retrying...");
        yield return new WaitForSeconds(1f);
        StartCoroutine(SendForASR(wavData, onSuccess, onError, attempt + 1));
        yield break;
    }

    if (www.result != UnityWebRequest.Result.Success)
    {
        onError?.Invoke($"ASR Error {(int)www.responseCode}: {responseBody}");
        yield break;
    }

    // parse JSON…
    var json = responseBody;
    try
    {
        var wrap = JsonUtility.FromJson<TranscriptionWrapper>(json);
        onSuccess?.Invoke(wrap.text);
    }
    catch (Exception ex)
    {
        Debug.LogError("[ASR] JSON parse error: " + ex);
        onError?.Invoke("ASR parse error");
    }
}


    private void UpdateStatus(string message, Color color)
    {
        if (statusText != null)
        {
            statusText.text  = message;
            statusText.color = color;
        }
    }

    private byte[] EncodeAsWAV(float[] samples, int frequency, int channels)
    {
        using var ms = new MemoryStream(44 + samples.Length * 2);
        using var writer = new BinaryWriter(ms);
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
        writer.Write("data".ToCharArray());
        writer.Write(samples.Length * 2);
        foreach (var s in samples)
            writer.Write((short)(s * short.MaxValue));
        return ms.ToArray();
    }

    [Serializable]
    private class TranscriptionWrapper { public string text; }
}


