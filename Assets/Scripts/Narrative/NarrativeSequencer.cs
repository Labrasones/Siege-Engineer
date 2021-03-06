﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NarrativeSequencer : MonoBehaviour {

    public enum DialogStructure {
        LEFT,
        RIGHT
    }
    public enum SequenceState {
        NOT_STARTED, // Sequence has not been triggered, Dialog not visible
        SHOWING_DIALOG, // Dialog entry animations playing
        PRINTING, // Dialog is displayed, printing text
        PRINTING_FAST, // Dialog is displayed, printing text at a faster rate
        WAITING_FOR_PROMPT, // Dialog is displayed, waiting for user confirmation
        HIDING_DIALOG, // Dialog exit animations playing, will show the next dialog when complete
        FINISHED // All sequences have been completed and the dialog is exiting or has exited
    }

    public enum CharacterEmotion {
        NEUTRAL = 0,
        HAPPY = 1,
        ANGRY = 2,
        EXHAUSTED = 3,
        SMUG = 4,
        LAUGHING = 5,
        SHIFTY = 6,
        SATISFIED = 7,
        SCARED = 8,
        SUPPRISED = 9
    }

    [Header ("UI References")]
    [Tooltip ("Controller for left side character dialog")]
    public DialogUIController leftController;
    [Tooltip ("Controller for right side character dialog")]
    public DialogUIController rightController;

    [Header ("Sequence Queue")]
    [Tooltip ("The NarrativeSequences to play, in order of playback")]
    public NarrativeSequence[] sequenceQueue;
    public bool standaloneScene = false;
    public SequenceState State {
        get {
            return curState;
        }
        set { }
    }
    SequenceState curState = SequenceState.NOT_STARTED;

    NarrativeSequence curSequence;
    DialogUIController controller;
    int curSequenceIndex = 0;
    int curSequenceCharacterIndex = 0;
    float timeSinceCharAdded = 0;
    float elapsedAdvanceWaitTime = 0;
    float scrollSpeed = 0;

#if UNITY_EDITOR
    // Just a little debug thing to add a camera to the scene if there isn't one, so we can test the narrative in it's own scene without having the camera interfere when we add it to another scene
    private void Awake () {
        if (Camera.main == null) {
            GameObject cam = new GameObject ();
            cam.transform.position = new Vector3 (0, 0, -11);
            Camera camComponent = cam.AddComponent<Camera> ();
            camComponent.backgroundColor = new Color (0, 0, 0);
            cam.tag = "MainCamera";
        }
    }
#endif

    void Start () { // Loading the scene should play the sequence
        curSequence = sequenceQueue[0];
        GotoState (SequenceState.SHOWING_DIALOG);
    }

    void CompleteNarrativeSequence () {
        if (standaloneScene) {
            CinematicManager.Instance.EnqueueCinematic (GameStateSwitcher.Instance.VictoryScene, false);
            if (curSequence != null) {
                curSequence.OnSequenceEnd ();
            }
            curSequence = null;
            CinematicManager.Instance.OnCinematicFinished (false);
        } else {
            if (curSequence != null) {
                curSequence.OnSequenceEnd ();
            }
            curSequence = null;
            CinematicManager.Instance.OnCinematicFinished ();
        }

    }

    void AdvanceSequence () {
        // Check if there are remaining states
        if (curSequenceIndex + 1 >= sequenceQueue.Length) { // No states remain. Finish the sequence and stop
            GotoState (SequenceState.FINISHED);
            return;
        }

        if (curSequence != null) {
            curSequence.OnSequenceEnd ();
        }

        NarrativeSequence nextSequence = sequenceQueue[++curSequenceIndex];

        bool newDialogNeeded = nextSequence.NeedsNewDialog (curSequence);

        curSequence = nextSequence;

        if (newDialogNeeded) {
            GotoState (SequenceState.HIDING_DIALOG);
        } else {
            GotoState (SequenceState.PRINTING);
        }
    }

    void GotoState (SequenceState state) {
        switch (state) {
            case SequenceState.SHOWING_DIALOG:
                controller = leftController;
                switch (curSequence.dialogType) {
                    case DialogStructure.LEFT:
                        controller = leftController;
                        break;

                    case DialogStructure.RIGHT:
                        controller = rightController;
                        break;
                }
                controller.Initialize (curSequence);
                controller.ShowDialog (() => {
                    GotoState (SequenceState.PRINTING);
                });
                break;

            case SequenceState.PRINTING:
                curSequenceCharacterIndex = 0;
                timeSinceCharAdded = 0;
                controller.DialogTextArea.text = ""; // Clear the text for printing
                scrollSpeed = curSequence.scrollSpeed;
                curSequence.OnSequenceStart ();
                controller.PlayPortraitEmotionAnim (curSequence.emotionState);
                break;

            case SequenceState.PRINTING_FAST:
                scrollSpeed *= 2; // Double the scroll speed for fast printing
                break;

            case SequenceState.WAITING_FOR_PROMPT:
                elapsedAdvanceWaitTime = 0.0f;
                break;

            case SequenceState.HIDING_DIALOG:
                controller.DialogTextArea.text = ""; // Clear the text before hiding
                controller.HideDialog (() => {
                    curSequence.OnSequenceEnd ();
                    GotoState (SequenceState.SHOWING_DIALOG);
                });
                break;

            case SequenceState.FINISHED:
                controller.DialogTextArea.text = ""; // Clear the text before hiding
                controller.HideDialog (() => {
                    CompleteNarrativeSequence ();
                });
                break;
        }
        curState = state;
    }

    void Update () {
        switch (curState) {
            case SequenceState.PRINTING:
            case SequenceState.PRINTING_FAST:
                /*
                 * Disable dialog advancement from anykey as per current design
                 * 
                if (curState == SequenceState.PRINTING)
                {   // Check if the user want's to speed things up
                    if (Input.anyKeyDown)
                    {
                        GotoState(SequenceState.PRINTING_FAST);
                    }
                }
                */

                timeSinceCharAdded += Time.deltaTime;
                int charsToAdd = Mathf.RoundToInt (scrollSpeed * timeSinceCharAdded); // (chars/second) * (seconds since last character was added)

                if (charsToAdd > 0) {
                    if (curSequenceCharacterIndex + charsToAdd >= curSequence.dialogText.Length) {
                        charsToAdd = curSequence.dialogText.Length - curSequenceCharacterIndex;
                        GotoState (SequenceState.WAITING_FOR_PROMPT);
                    }
                    controller.DialogTextArea.text = controller.DialogTextArea.text + curSequence.dialogText.Substring (curSequenceCharacterIndex, charsToAdd);
                    curSequenceCharacterIndex += charsToAdd;
                    timeSinceCharAdded = 0;
                }

                break;

            case SequenceState.WAITING_FOR_PROMPT:
                elapsedAdvanceWaitTime += Time.deltaTime;
                /*
                 * No longer require player input to advance
                if ((curSequence.autoAdvance && elapsedAdvanceWaitTime >= curSequence.advanceDelay) || Input.anyKeyDown)
                */
                if (elapsedAdvanceWaitTime >= curSequence.advanceDelay) {
                    AdvanceSequence ();
                }
                break;
        }
    }
}