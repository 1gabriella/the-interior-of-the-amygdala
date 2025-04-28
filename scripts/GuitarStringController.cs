
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using System.Linq;
using System;

public class GuitarStringController : MonoBehaviour
{
    [Header("MIDI Settings")]
    [Tooltip("Path to the MIDI file for this string (e.g., \"Assets/midi/string6.mid\")")]
    public string midiFilePath;

    [Header("Audio Settings")]
    [Tooltip("Audio clip that represents this string's pluck sound (recorded at a base pitch).")]
    public AudioClip pluckClip;
    [Tooltip("AudioSource component that plays the pluck sound.")]
    public AudioSource audioSource;
    [Tooltip("The base MIDI note for which the pluckClip is recorded (e.g., 40 for string6 if recorded at MIDI note 40).")]
    public int baseMidiNote;

    private IEnumerable<Note> midiNotes;
    private TempoMap tempoMap;

    void Start()
    {
        try
        {
            MidiFile midiFile = MidiFile.Read(midiFilePath);
            tempoMap = midiFile.GetTempoMap();
            midiNotes = midiFile.GetNotes();
            Debug.Log($"{gameObject.name}: Loaded MIDI file with {midiNotes.Count()} note events.");
            StartCoroutine(PlayMidiNotes());
        }
        catch (Exception ex)
        {
            Debug.LogError($"{gameObject.name}: Error loading MIDI file: {ex.Message}");
        }
    }

    IEnumerator PlayMidiNotes()
    {
        float startTime = Time.time;

        foreach (var note in midiNotes)
        {
            // Convert MIDI note time (ticks) to seconds using MetricTimeSpan
            double noteStartSec = TimeConverter.ConvertTo<MetricTimeSpan>(note.Time, tempoMap).TotalSeconds;
            float delay = (float)(noteStartSec - (Time.time - startTime));
            if (delay > 0)
                yield return new WaitForSeconds(delay);

            int midiNoteNumber = note.NoteNumber;

            // Calculate pitch shift: when the note is equal to baseMidiNote, pitch factor is 1.
            float pitchShift = Mathf.Pow(2f, (midiNoteNumber - baseMidiNote) / 12f);
            PlayNote(pitchShift);
        }
    }

    public void PlayNote(float pitchShift)
    {
        if (audioSource != null && pluckClip != null)
        {
            audioSource.pitch = pitchShift;
            audioSource.PlayOneShot(pluckClip);
            Debug.Log($"{gameObject.name}: Playing note with pitch shift {pitchShift}");
        }
        else
        {
            Debug.LogWarning($"{gameObject.name}: Missing AudioSource or pluckClip");
        }
    }
}

