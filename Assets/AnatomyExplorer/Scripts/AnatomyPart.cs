using UnityEngine;

namespace AnatomyExplorer
{
    public sealed class AnatomyPart : MonoBehaviour
    {
        public string displayName;
        public string systemName;
        public Renderer meshRenderer;
        public Material originalMaterial;
        public Vector3 restPosition;
        public Quaternion restRotation;
        public int segment = -1;
    }
}
