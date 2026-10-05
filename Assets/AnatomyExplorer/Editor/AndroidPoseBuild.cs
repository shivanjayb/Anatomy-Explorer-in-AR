#if UNITY_ANDROID
using System.IO;
using UnityEditor.Android;

namespace AnatomyExplorer.Editor
{
    public sealed class AndroidPoseBuild : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 100;
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string file = Path.Combine(path, "build.gradle");
            string text = File.ReadAllText(file);
            if (!text.Contains("com.google.mlkit:pose-detection:"))
                File.AppendAllText(file, "\n// Anatomy Explorer on-device body pose tracker\ndependencies { implementation 'com.google.mlkit:pose-detection:18.0.0-beta5' }\n");
            string rules = Path.Combine(path, "proguard-unity.txt");
            File.AppendAllText(rules, "\n-keep class com.anatomyexplorer.PoseBridge { *; }\n");
        }
    }
}
#endif
