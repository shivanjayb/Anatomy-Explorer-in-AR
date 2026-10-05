using UnityEngine;

namespace AnatomyExplorer
{
    // A compact provider-neutral pose: head, shoulders, elbows, wrists, hips, knees, ankles.
    public sealed class AnatomyPose
    {
        public readonly Vector3[] points = new Vector3[13];
        public readonly float[] confidence = new float[13];
        public bool valid;
        public string provider;
        public string subjectId;
        public float receivedAt;
    }
}
