#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AnatomyExplorer.Editor
{
    // Plays a scripted feature tour in real Play-mode time while BragRecorder captures the Game view.
    [InitializeOnLoad]
    public static class AnatomyBragDirector
    {
        const string Pending = "AnatomyExplorer.BragDirector.Pending";
        static List<(float t, string label, Action<AnatomyExplorerApp> run)> steps;
        static int cursor;
        static float startTime;
        static AnatomyExplorerApp app;

        static AnatomyBragDirector()
        {
            EditorApplication.playModeStateChanged += OnPlay;
        }

        [MenuItem("Anatomy Explorer/Record brag footage")]
        public static void Run()
        {
            SessionState.SetBool(Pending, true);
            var scene = SceneManager.GetActiveScene();
            if (scene.isDirty && !string.IsNullOrEmpty(scene.path)) EditorSceneManager.SaveScene(scene);
            EditorSceneManager.OpenScene(AnatomySceneBuilder.ScenePath);
            EditorApplication.isPlaying = true;
        }

        static void OnPlay(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Pending, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                EditorApplication.delayCall += Begin;
            }
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                SessionState.SetBool(Pending, false);
                EditorApplication.update -= Tick;
            }
        }

        static void Begin()
        {
            app = UnityEngine.Object.FindAnyObjectByType<AnatomyExplorerApp>();
            if (app == null) { Debug.LogError("BRAG_DIRECTOR_FAILED: no AnatomyExplorerApp in scene."); EditorApplication.isPlaying = false; return; }

            var captureDir = System.IO.Path.Combine(Application.dataPath, "..", "brag-output", "raw-capture");
            System.IO.Directory.CreateDirectory(captureDir);
            var recorder = app.gameObject.AddComponent<BragRecorder>();
            recorder.BeginRecording(System.IO.Path.Combine(captureDir, "take"), 1920, 1080, 30f);

            steps = BuildSchedule();
            cursor = 0;
            startTime = Time.realtimeSinceStartup;
            EditorApplication.update += Tick;
        }

        static void Tick()
        {
            if (app == null) return;
            float elapsed = Time.realtimeSinceStartup - startTime;
            while (cursor < steps.Count && elapsed >= steps[cursor].t)
            {
                var step = steps[cursor];
                try { step.run(app); Debug.Log("BRAG_STEP[" + step.t.ToString("0.0") + "s]: " + step.label); }
                catch (Exception e) { Debug.LogError("BRAG_STEP_FAILED[" + step.label + "]: " + e); }
                cursor++;
            }
            if (cursor >= steps.Count)
            {
                EditorApplication.update -= Tick;
                var recorder = app.GetComponent<BragRecorder>();
                if (recorder != null) UnityEngine.Object.DestroyImmediate(recorder); // OnDisable stops recording
                Debug.Log("BRAG_CAPTURE_DONE");
                EditorApplication.isPlaying = false;
            }
        }

        static Transform FindByName(Transform root, string name)
        {
            var queue = new Queue<Transform>();
            queue.Enqueue(root);
            while (queue.Count > 0)
            {
                var t = queue.Dequeue();
                if (t.name == name) return t;
                for (int i = 0; i < t.childCount; i++) queue.Enqueue(t.GetChild(i));
            }
            return null;
        }

        static void Click(AnatomyExplorerApp app, string buttonName)
        {
            var t = FindByName(app.transform, buttonName);
            if (t == null) { Debug.LogWarning("BRAG_BUTTON_NOT_FOUND: " + buttonName); return; }
            var button = t.GetComponent<Button>();
            if (button == null) { Debug.LogWarning("BRAG_NOT_A_BUTTON: " + buttonName); return; }
            button.onClick.Invoke();
        }

        static List<(float, string, Action<AnatomyExplorerApp>)> BuildSchedule()
        {
            return new List<(float, string, Action<AnatomyExplorerApp>)>
            {
                (2.5f, "Layer: Skeleton", a => Click(a, "Skeleton")),
                (4.5f, "Layer: Organs", a => Click(a, "Organs")),
                (6.5f, "Layer: Nerves", a => Click(a, "Nerves")),
                (8.0f, "Layer: Blood vessels", a => Click(a, "Blood vessels")),
                (9.5f, "Layer: Joints", a => Click(a, "Joints")),
                (11.0f, "Layer: Lymphatic", a => Click(a, "Lymphatic")),
                (12.5f, "All systems", a => Click(a, "All systems")),
                (14.5f, "Layer: Muscles", a => Click(a, "Muscles")),
                (15.5f, "Rotate on", a => Click(a, "Rotate / pause")),
                (21.5f, "Rotate off", a => Click(a, "Rotate / pause")),
                (22.0f, "Zoom +", a => Click(a, "Zoom +")),
                (22.3f, "Zoom +", a => Click(a, "Zoom +")),
                (22.6f, "Zoom +", a => Click(a, "Zoom +")),
                (23.5f, "Search: deltoid", a =>
                {
                    var field = a.GetComponentInChildren<TMP_InputField>();
                    if (field != null) field.text = "deltoid";
                }),
                (24.3f, "Select first result", a =>
                {
                    var content = FindByName(a.transform, "Content");
                    var first = content != null && content.childCount > 0 ? content.GetChild(0).GetComponent<Button>() : null;
                    if (first != null) first.onClick.Invoke();
                }),
                (26.0f, "Isolate", a => Click(a, "Isolate")),
                (28.5f, "Reset", a => Click(a, "Reset")),
                (30.0f, "Zoom -", a => Click(a, "Zoom −")),
                (30.3f, "Zoom -", a => Click(a, "Zoom −")),
                (30.6f, "Zoom -", a => Click(a, "Zoom −")),
                (31.2f, "Fascia on", a => Click(a, "Fascia on/off")),
                (33.0f, "Fascia off", a => Click(a, "Fascia on/off")),
                (34.5f, "Live body overlay", a => Click(a, "Live body overlay")),
                (37.0f, "Atlas preview", a => Click(a, "Atlas preview")),
                (39.0f, "End", a => { }),
            };
        }
    }
}
#endif
