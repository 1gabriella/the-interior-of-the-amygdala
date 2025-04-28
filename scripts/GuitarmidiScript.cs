
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using System.Linq;
using System;

public class SimpleMidiPlayer : MonoBehaviour
{
    [Tooltip("Relative path to the MIDI file (e.g., Assets/Midi/song.mid)")]
    public string midiFilePath = "Assets/Midi/song.mid";

    [Tooltip("The AudioSource component that will play the audio sample. Its AudioClip should be your instrument sound recorded at a base pitch.")]
    public AudioSource audioSource;

    [Tooltip("The base MIDI note number corresponding to the AudioClip’s pitch (e.g., 60 for Middle C)")]
    public int baseMidiNote = 60;

    // Optional multiplier if you want to adjust the recorded pitch further.
    [Tooltip("Additional pitch multiplier (default 1 means no change)")]
    public float basePitch = 1.0f;

    private IEnumerable<Note> midiNotes;
    private TempoMap tempoMap;

    void Start()
    {
        try
        {
            // Load the MIDI file using DryWetMIDI.
            MidiFile midiFile = MidiFile.Read(midiFilePath);
            tempoMap = midiFile.GetTempoMap();
            midiNotes = midiFile.GetNotes();
            Debug.Log("Loaded MIDI file with " + midiNotes.Count() + " note events.");
            StartCoroutine(PlayNotes());
        }
        catch (Exception e)
        {
            Debug.LogError("Error reading MIDI file: " + e.Message);
        }
    }

    IEnumerator PlayNotes()
    {
        float startTime = Time.time;

        foreach (var note in midiNotes)
        {
            // Convert MIDI note time (ticks) to seconds.
            double noteStartSec = TimeConverter.ConvertTo<MetricTimeSpan>(note.Time, tempoMap).TotalSeconds;
            float delay = (float)(noteStartSec - (Time.time - startTime));
            if (delay > 0)
                yield return new WaitForSeconds(delay);

            int midiNoteNumber = note.NoteNumber;

            // Calculate pitch shift:
            // When the MIDI note equals baseMidiNote, pitchShift should be 1 (no change).
            // For each semitone, the pitch multiplier is the 12th root of 2 (approximately 1.0595).
            float pitchShift = Mathf.Pow(2f, (midiNoteNumber - baseMidiNote) / 12f);

            // Play the sample with the computed pitch shift.
            if (audioSource != null && audioSource.clip != null)
            {
                audioSource.pitch = pitchShift * basePitch;
                audioSource.PlayOneShot(audioSource.clip);
                Debug.Log("Playing MIDI note " + midiNoteNumber + " with pitch shift " + pitchShift);
            }
            else
            {
                Debug.LogWarning("AudioSource or AudioClip not assigned.");
            }
        }
    }
}



