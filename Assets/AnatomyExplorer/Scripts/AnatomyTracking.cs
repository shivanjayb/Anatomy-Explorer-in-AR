using System;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.Collections;

namespace AnatomyExplorer
{
    public sealed class AnatomyTracking : MonoBehaviour
    {
        public ARCameraManager cameraManager;
        public ARHumanBodyManager bodyManager;
        public Camera viewCamera;
        public AnatomyPose Pose { get; private set; } = new AnatomyPose();
        public string Status { get; private set; } = "Unity preview — no live body tracking";
        public float assumedHeight = 1.75f;
        public bool live;
        float nextFrame;
        Matrix4x4 displayMatrix;
        bool hasDisplayMatrix;
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaObject detector;
        long lastSequence;
#endif
        void Start()
        {
            if (cameraManager != null)
            {
                cameraManager.requestedFacingDirection = CameraFacingDirection.World;
                cameraManager.frameReceived += OnCameraFrame;
            }
#if UNITY_ANDROID && !UNITY_EDITOR
            try { detector = new AndroidJavaObject("com.anatomyexplorer.PoseBridge"); Status = "Android pose tracker ready — show the whole person"; }
            catch (Exception e) { Status = "Android pose tracker unavailable: " + e.Message; }
#elif UNITY_IOS && !UNITY_EDITOR
            if (bodyManager != null) { bodyManager.pose3DRequested = true; bodyManager.pose3DScaleEstimationRequested = true; }
            Status = "iPhone body tracker — show the whole person";
#endif
        }
        void OnCameraFrame(ARCameraFrameEventArgs args)
        {
            if (args.displayMatrix.HasValue) { displayMatrix = args.displayMatrix.Value; hasDisplayMatrix = true; }
        }
        void Update()
        {
            if (!live) { Pose.valid = false; return; }
#if UNITY_IOS && !UNITY_EDITOR
            ReadAppleBody();
#elif UNITY_ANDROID && !UNITY_EDITOR
            ReadAndroidPose();
            if (Time.unscaledTime >= nextFrame && detector != null && cameraManager != null)
            {
                nextFrame = Time.unscaledTime + 0.10f;
                if (!detector.Call<bool>("isBusy") && cameraManager.TryAcquireLatestCpuImage(out var image))
                {
                    try
                    {
                        // Keep original image orientation. Map image landmarks to viewport using AR's display matrix.
                        var size = new Vector2Int(Mathf.Max(1, image.width / 3), Mathf.Max(1, image.height / 3));
                        var conversion = new XRCpuImage.ConversionParams(image, TextureFormat.RGBA32, XRCpuImage.Transformation.None);
                        conversion.outputDimensions = size;
                        using (var buffer = new NativeArray<byte>(image.GetConvertedDataSize(conversion), Allocator.Temp))
                        {
                            image.Convert(conversion, buffer);
                            detector.Call("submit", buffer.ToArray(), size.x, size.y);
                        }
                    }
                    catch (Exception e) { Status = "Camera frame error: " + e.Message; }
                    finally { image.Dispose(); }
                }
            }
#else
            Status = "Live tracking requires a phone build. Use Atlas preview in Unity.";
#endif
            if (Time.unscaledTime - Pose.receivedAt > 0.7f) Pose.valid = false;
        }
#if UNITY_IOS && !UNITY_EDITOR
        void ReadAppleBody()
        {
            if (bodyManager == null) return;
            foreach (var body in bodyManager.trackables)
            {
                if (body.trackingState != TrackingState.Tracking || !body.joints.IsCreated || body.joints.Length < 91) continue;
                // ARKit's documented 91-joint skeleton order; indexes are checked against official sample.
                int[] indexes = {51,20,64,21,65,22,66,2,7,3,8,4,9};
                for (int i = 0; i < indexes.Length; i++)
                {
                    var j = body.joints[indexes[i]];
                    Pose.points[i] = body.transform.TransformPoint(j.anchorPose.position);
                    Pose.confidence[i] = j.tracked ? 1f : 0f;
                }
                Pose.valid = Pose.confidence[1] > 0 && Pose.confidence[2] > 0 && Pose.confidence[7] > 0 && Pose.confidence[8] > 0;
                Pose.provider = "ARKit 3D body tracking";
                Pose.subjectId = body.trackableId.ToString();
                Pose.receivedAt = Time.unscaledTime;
                Status = Pose.valid ? "ARKit: person tracked" : "ARKit: keep shoulders and hips visible";
                return;
            }
            Pose.valid = false; Status = "ARKit: looking for a person — step back to show the full body";
        }
#endif
#if UNITY_ANDROID && !UNITY_EDITOR
        void ReadAndroidPose()
        {
            if (detector == null) return;
            string error = detector.Call<string>("getError");
            if (!string.IsNullOrEmpty(error)) { Status = error; Pose.valid = false; return; }
            long sequence = detector.Call<long>("getSequence");
            if (sequence == lastSequence) return;
            lastSequence = sequence;
            float[] data = detector.Call<float[]>("getPoints");
            if (data == null || data.Length < 52 || !hasDisplayMatrix) { Pose.valid = false; return; }
            Vector2[] uv = new Vector2[13];
            for (int i = 0; i < 13; i++)
            {
                // Unity AR background samples camera texture with row-vector displayMatrix multiplication.
                var imageUV = new Vector3(data[i * 4], 1f - data[i * 4 + 1], 1);
                var m = new Matrix4x4();
                m.SetRow(0, new Vector4(displayMatrix.m00, displayMatrix.m10, displayMatrix.m20, 0));
                m.SetRow(1, new Vector4(displayMatrix.m01, displayMatrix.m11, displayMatrix.m21, 0));
                m.SetRow(2, new Vector4(0,0,1,0)); m.m33 = 1;
                Vector3 screenUV = m.inverse.MultiplyPoint3x4(imageUV);
                uv[i] = new Vector2(screenUV.x, screenUV.y);
                Pose.confidence[i] = data[i * 4 + 3];
            }
            bool core = Pose.confidence[1] > .55f && Pose.confidence[2] > .55f && Pose.confidence[7] > .55f && Pose.confidence[8] > .55f;
            bool full = Pose.confidence[11] > .4f && Pose.confidence[12] > .4f && Pose.confidence[0] > .4f;
            if (!core || !full) { Pose.valid = false; Status = "Android: show head, shoulders, hips and both feet"; return; }
            float span = Mathf.Abs(uv[0].y - (uv[11].y + uv[12].y) * .5f);
            float depth = Mathf.Clamp(assumedHeight / (2 * Mathf.Tan(viewCamera.fieldOfView * Mathf.Deg2Rad * .5f) * Mathf.Max(.1f, span)), .7f, 8f);
            for (int i = 0; i < 13; i++) Pose.points[i] = viewCamera.ViewportToWorldPoint(new Vector3(uv[i].x, uv[i].y, depth));
            Pose.valid = true; Pose.receivedAt = Time.unscaledTime; Pose.provider = "ML Kit 2D pose + estimated depth";
            Pose.subjectId = "single-person";
            Status = "Android: person tracked • approximate depth";
        }
#endif
        void OnDestroy()
        {
            if (cameraManager != null) cameraManager.frameReceived -= OnCameraFrame;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (detector != null) { detector.Call("close"); detector.Dispose(); }
#endif
        }
    }
}
