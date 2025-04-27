
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using TMPro;

[RequireComponent(typeof(AudioSource))]
public class SimpleMusicVA : MonoBehaviour
{
    [Header("XR String Triggers")]
    [Tooltip("Drag all 6 guitar‐string XRBaseInteractable components here.")]
    [SerializeField] private List<XRBaseInteractable> strings = new();

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI feedbackText;

    [Header("FFT & Smoothing")]
    [SerializeField] private int fftSize = 1024;
    [SerializeField] private float smoothTime = 0.5f;

    [Header("Recording Settings")]
    [Tooltip("Max seconds to record into the string's AudioSource.clip")]
    [SerializeField] private int recordingDuration = 10;
    [Tooltip("Sample rate for microphone capture")]
    [SerializeField] private int sampleRate = 44100;

    [Header("RMS Calibration")]
    [Tooltip("Divide raw RMS by this value (tune after inspecting Debug.Log).")]
    [SerializeField] private float rmsDivisor = 0.005f;

    // Internal state
    private AudioSource currentSrc;
    private float[]     spectrum;
    private float       smoothedArousal;
    private float       smoothedValence;
    private bool        isRecording;

    void OnEnable()
    {
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
        spectrum = new float[fftSize];
    }

    private void OnPlayStarted(HoverEnterEventArgs args)
    {
        // Log available devices and start position
        Debug.Log("[SimpleMusicVA] Mic devices: " + string.Join(", ", Microphone.devices));
        Debug.Log("[SimpleMusicVA] Mic start position: " + Microphone.GetPosition(null));

        currentSrc = args.interactableObject.transform.GetComponent<AudioSource>();
        if (currentSrc == null || isRecording) return;

        isRecording = true;
        currentSrc.clip = Microphone.Start(
            deviceName: null,
            loop:      true,
            lengthSec: recordingDuration,
            frequency: sampleRate
        );
        currentSrc.loop = true;
        StartCoroutine(WaitThenPlay(currentSrc));
    }

    private IEnumerator WaitThenPlay(AudioSource src)
    {
        // Wait until mic starts filling the buffer
        while (Microphone.GetPosition(null) <= 0)
            yield return null;

        Debug.Log("[SimpleMusicVA] Mic kicked in at position: " + Microphone.GetPosition(null));
        src.Play();
    }

    private void OnPlayEnded(HoverExitEventArgs args)
    {
        if (!isRecording || currentSrc == null) return;

        Microphone.End(null);
        currentSrc.Stop();
        isRecording = false;
        Debug.Log("[SimpleMusicVA] Recording stopped.");
    }

    void Update()
    {
        if (!isRecording || currentSrc == null) return;

        // 1) FFT ⇒ spectral centroid ⇒ valence
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
        float valence = (centroidNorm - 0.5f) * 2f;

        // 2) Larger RMS window ⇒ arousal
        int windowSize = Mathf.Min(16384, currentSrc.clip.samples);
        float[] buffer = new float[windowSize];
        int start = (currentSrc.timeSamples - windowSize + currentSrc.clip.samples) % currentSrc.clip.samples;
        currentSrc.clip.GetData(buffer, start);

        float sumSq = 0f;
        foreach (var s in buffer) sumSq += s * s;
        float rms = Mathf.Sqrt(sumSq / buffer.Length);

        // Debug raw RMS and divisor
        Debug.Log($"[SimpleMusicVA] Raw RMS over {windowSize} samples = {rms:F6}");
        Debug.Log($"[SimpleMusicVA] rmsDivisor = {rmsDivisor:F6}");

        float rmsNorm = rms / rmsDivisor;
        Debug.Log($"[SimpleMusicVA] rmsNorm = {rmsNorm:F6}");

        float arousal = Mathf.Clamp01(rmsNorm);

        // 3) Smooth both values
        smoothedValence = Mathf.Lerp(smoothedValence, valence, Time.deltaTime / smoothTime);
        smoothedArousal = Mathf.Lerp(smoothedArousal, arousal, Time.deltaTime / smoothTime);

        // 4) Update UI
        feedbackText.text =
            $"Valence:   {smoothedValence:F2}\n" +
            $"Arousal: {smoothedArousal:F2}";
    }
}

