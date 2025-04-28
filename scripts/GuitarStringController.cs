/*
 * INSPECTOR SETUP:
 * - Midi File Path: String path to  MIDI file (example: "Assets/midi/string6.mid") - there are multiple midi files taken from three beatles songs 
 * - Pluck Clip: AudioClip of the pluck sound 
 * - Audio Source: The AudioSource component that will play the pluck sound

 */
INSPIRATIONS & REFERENCES:
 * - DryWetMIDI Library by Melanchall: https://github.com/melanchall/drywetmidi
 *   .NET library for MIDI file processing, used here for reading MIDI notes and tempo mapping.
 * - Unity Integration Guide for DryWetMIDI: https://melanchall.github.io/drywetmidi/articles/dev/Using-in-Unity.html.
 * - Unity MIDI Piano Project by catdevpete: https://github.com/catdevpete/Unity-MIDI-Piano
 *   Served as a reference for handling MIDI note playback within Unity.
 * - Reddit Discussion on MIDI and C# in Unity: https://www.reddit.com/r/unity/comments/p71xz4/anyone_used_midi_and_c_in_unity_before/
 * 
 * 
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using System.Linq;
using System;

// This script controls the playback of notes 
public class GuitarStringController : MonoBehaviour
{
    // Path to the MIDI file used for this string
    [Tooltip("Path to the MIDI file for this string (e.g., \"Assets/midi/string6.mid\")")]
    public string midiFilePath;

    // Sound clip to be played when a note is plucked
    public AudioClip pluckClip;

    // Audio source that will play the pluckClip
    public AudioSource audioSource;

    // The "base" note - when the MIDI note equals this, pitch will be normal 
    public int baseMidiNote;

    // List of all the notes from the MIDI file
    private IEnumerable<Note> midiNotes;

    // Information about tempo from the MIDI file
    private TempoMap tempoMap;

    // Start is called before the first frame update
    void Start()
    {
        try
        {
            // Load the MIDI file from the given path
            MidiFile midiFile = MidiFile.Read(midiFilePath);
            
            // Get the tempo information 
            tempoMap = midiFile.GetTempoMap();

            // Get all the notes contained in the MIDI file
            midiNotes = midiFile.GetNotes();

            // Start the coroutine that plays the notes
            StartCoroutine(PlayMidiNotes());
        }
        catch (Exception ex)
        {
            // Catch any errors 
            Debug.LogError($"{gameObject.name}: Error loading MIDI file: {ex.Message}");
        }
    }

    // Coroutine that plays the MIDI notes 
    IEnumerator PlayMidiNotes()
    {
        // Record the start time 
        float startTime = Time.time;

        // Loop through all notes in the MIDI file
        foreach (var note in midiNotes)
        {
            // Convert the note's MIDI time to seconds
            double noteStartSec = TimeConverter.ConvertTo<MetricTimeSpan>(note.Time, tempoMap).TotalSeconds;

            // Calculate how long to wait until playing this note
            float delay = (float)(noteStartSec - (Time.time - startTime));
            if (delay > 0)
                yield return new WaitForSeconds(delay);

            // Get the MIDI note number
            int midiNoteNumber = note.NoteNumber;

            // Calculate the pitch shift based on how far the note is from the base note
            float pitchShift = Mathf.Pow(2f, (midiNoteNumber - baseMidiNote) / 12f);

            // Play the note with the calculated pitch
            PlayNote(pitchShift);
        }
    }

    // Function to actually play a note with a given pitch shift
    public void PlayNote(float pitchShift)
    {
        if (audioSource != null && pluckClip != null)
        {
            // Set the audio source pitch and play the pluck sound
            audioSource.pitch = pitchShift;
            audioSource.PlayOneShot(pluckClip);
            Debug.Log($"{gameObject.name}: Playing note with pitch shift {pitchShift}");
        }
        else
        {
            // Warn if the audio source or pluck clip is missing
            Debug.LogWarning($"{gameObject.name}: Missing AudioSource or PluckClip");
        }
    }
}

