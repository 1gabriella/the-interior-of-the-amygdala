// ===============================================
// Influences & Inspirations:
// - UnityEngine.Microphone API for real-time audio capture
// - Audio spectrum analysis via FFT (Fast Fourier Transform)
// - Valence/Arousal mapping inspired by music emotion research
// - XR Interaction Toolkit for hover-based input events
// - UnityEngine.Mathf.Lerp for smoothing signals over time
// - TextMeshPro for in-game text feedback
// - Coroutine patterns for asynchronous audio initialization
// - C# event-driven architecture using UnityEvent listeners
// ===============================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using TMPro;

[RequireComponent(typeof(AudioSource))]
public class SimpleMusicVA : MonoBehaviour
{
    [Header("XR String Triggers")]
    [Tooltip("Drag all 6 guitar-string XRBaseInteractable components here.")]
    [SerializeField] private List<XRBaseInteractable> strings = new();

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI feedbackText;  // Displays valence and arousal values

    [Header("FFT & Smoothing")]
    [SerializeField] private int fftSize = 1024;            // Number of samples for FFT
    [SerializeField] private float smoothTime = 0.5f;       // Seconds over which to smooth values

    [Header("Recording Settings")]
    [Tooltip("Max seconds to record into the string's AudioSource.clip")]
    [SerializeField] private int recordingDuration = 10;
    [Tooltip("Sample rate for microphone capture")]
    [SerializeField] private int sampleRate = 44100;

    [Header("RMS Calibration")]
    [Tooltip("Divide raw RMS by this value (tune after inspecting Debug.Log).")]
    [SerializeField] private float rmsDivisor = 0.005f;

    // Internal state
    private AudioSource currentSrc;    // The AudioSource tied to the currently active string
    private float[] spectrum;          // Buffer for FFT data
    private float smoothedArousal;     // Smoothed arousal value
    private float smoothedValence;     // Smoothed valence value
    private bool isRecording;          // Whether microphone is capturing

    void OnEnable()
    {
        // Register hover events on each interactable string
        foreach (var s in strings)
        {
            if (s != null)
            {
                s.hoverEntered.AddListener(OnPlayStarted);
                s.hoverExited .AddListener(OnPlayEnded);
            }
        }
    }

    void OnDisable()
    {
        // Unregister hover events to avoid memory leaks
        foreach (var s in strings)
        {
            if (s != null)
            {
                s.hoverEntered.RemoveListener(OnPlayStarted);
                s.hoverExited .RemoveListener(OnPlayEnded);
            }
        }
    }

    void Start()
    {
        // Initialize the spectrum buffer for FFT calculations
        spectrum = new float[fftSize];
    }

    private void OnPlayStarted(HoverEnterEventArgs args)
    {
        // Called when user hovers over a string interactable
        Debug.Log("[SimpleMusicVA] Mic devices: " + string.Join(", ", Microphone.devices));
        Debug.Log("[SimpleMusicVA] Mic start position: " + Microphone.GetPosition(null));

        // Get or add an AudioSource on the string object
        currentSrc = args.interactableObject.transform.GetComponent<AudioSource>();
        if (currentSrc == null || isRecording) return;

        isRecording = true;
        // Start recording into the AudioSource.clip
        currentSrc.clip = Microphone.Start(null, true, recordingDuration, sampleRate);
        currentSrc.loop = true;
        StartCoroutine(WaitThenPlay(currentSrc));
    }

    private IEnumerator WaitThenPlay(AudioSource src)
    {
        // Wait until the microphone has begun capturing audio
        while (Microphone.GetPosition(null) <= 0)
            yield return null;

        Debug.Log("[SimpleMusicVA] Mic kicked in at position: " + Microphone.GetPosition(null));
        src.Play();  // Begin playback of the recorded audio
    }

    private void OnPlayEnded(HoverExitEventArgs args)
    {
        // Stop recording and playback when hover ends
        if (!isRecording || currentSrc == null) return;

        Microphone.End(null);
        currentSrc.Stop();
        isRecording = false;
        Debug.Log("[SimpleMusicVA] Recording stopped.");
    }

    void Update()
    {
        // Only run analysis when recording is active
        if (!isRecording || currentSrc == null) return;

        // 1) Perform FFT to compute spectral centroid (valence proxy)
        currentSrc.GetSpectrumData(spectrum, 0, FFTWindow.BlackmanHarris);
        float num = 0f, den = 0f;
        for (int i = 0; i < fftSize; i++)
        {
            float m = spectrum[i];
            num += i * m;
            den += m;
        }
        float centroid = (den > 0f) ? num / den : fftSize / 2f;
        float centroidNorm = Mathf.Clamp01(centroid / (fftSize / 2f));
        float valence = (centroidNorm - 0.5f) * 2f;  // Range [-1, 1]

        // 2) Compute RMS over a sliding window for arousal
        int windowSize = Mathf.Min(16384, currentSrc.clip.samples);
        float[] buffer = new float[windowSize];
        int start = (currentSrc.timeSamples - windowSize + currentSrc.clip.samples) % currentSrc.clip.samples;
        currentSrc.clip.GetData(buffer, start);

        float sumSq = 0f;
        foreach (var s in buffer) sumSq += s * s;
        float rms = Mathf.Sqrt(sumSq / buffer.Length);

        Debug.Log($"[SimpleMusicVA] Raw RMS = {rms:F6}, divisor = {rmsDivisor:F6}");
        float rmsNorm = rms / rmsDivisor;              // Normalize using calibration divisor
        float arousal = Mathf.Clamp01(rmsNorm);       // Range [0, 1]

        // 3) Smooth both valence and arousal over time
        smoothedValence = Mathf.Lerp(smoothedValence, valence, Time.deltaTime / smoothTime);
        smoothedArousal = Mathf.Lerp(smoothedArousal, arousal, Time.deltaTime / smoothTime);

        // 4) Update the on-screen feedback text
        feedbackText.text =
            $"Valence:   {smoothedValence:F2}\n" +
            $"Arousal: {smoothedArousal:F2}";
    }
}


