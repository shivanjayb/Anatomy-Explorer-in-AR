#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;

namespace AnatomyExplorer.Editor
{
    // Records the Game view to MP4 for the duration this component is enabled.
    // Adapted from the Unity Recorder package's bundled MovieRecorderExample.
    public sealed class BragRecorder : MonoBehaviour
    {
        RecorderController controller;

        // Called explicitly after the output path is set — AddComponent() would fire OnEnable()
        // before the caller gets a chance to configure the component, starting with an empty path.
        public void BeginRecording(string outputPath, int width, int height, float frameRate)
        {
            var controllerSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            controller = new RecorderController(controllerSettings);

            var settings = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            settings.name = "BragCapture";
            settings.Enabled = true;
            settings.EncoderSettings = new CoreEncoderSettings
            {
                EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High,
                Codec = CoreEncoderSettings.OutputCodec.MP4
            };
            settings.ImageInputSettings = new GameViewInputSettings { OutputWidth = width, OutputHeight = height };
            settings.OutputFile = outputPath;

            controllerSettings.AddRecorderSettings(settings);
            controllerSettings.SetRecordModeToManual();
            controllerSettings.FrameRate = frameRate;

            RecorderOptions.VerboseMode = false;
            controller.PrepareRecording();
            controller.StartRecording();
            Debug.Log("BRAG_RECORDING_STARTED: " + outputPath + ".mp4");
        }

        void OnDisable()
        {
            if (controller != null && controller.IsRecording()) controller.StopRecording();
        }
    }
}
#endif
