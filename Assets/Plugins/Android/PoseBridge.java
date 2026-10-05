package com.anatomyexplorer;

import android.graphics.Bitmap;
import android.content.Context;
import android.hardware.camera2.CameraCharacteristics;
import android.hardware.camera2.CameraManager;
import android.view.Surface;
import android.view.WindowManager;
import com.unity3d.player.UnityPlayer;
import com.google.mlkit.vision.common.InputImage;
import com.google.mlkit.vision.pose.PoseDetection;
import com.google.mlkit.vision.pose.PoseDetector;
import com.google.mlkit.vision.pose.PoseLandmark;
import com.google.mlkit.vision.pose.defaults.PoseDetectorOptions;

/** Offline, on-device inference. Camera frames are not uploaded. */
public final class PoseBridge {
    private final PoseDetector detector = PoseDetection.getClient(new PoseDetectorOptions.Builder()
        .setDetectorMode(PoseDetectorOptions.STREAM_MODE).build());
    private volatile boolean busy;
    private volatile float[] points = new float[52];
    private volatile long sequence;
    private volatile String error = "";
    private final int[] ids = {0,11,12,13,14,15,16,23,24,25,26,27,28};
    private int sensorOrientation = 90;
    public PoseBridge() {
        try {
            CameraManager cameras = (CameraManager)UnityPlayer.currentActivity.getSystemService(Context.CAMERA_SERVICE);
            for (String id : cameras.getCameraIdList()) {
                CameraCharacteristics c = cameras.getCameraCharacteristics(id);
                Integer facing = c.get(CameraCharacteristics.LENS_FACING);
                if (facing != null && facing == CameraCharacteristics.LENS_FACING_BACK) {
                    Integer sensor = c.get(CameraCharacteristics.SENSOR_ORIENTATION);
                    if (sensor != null) sensorOrientation = sensor;
                    break;
                }
            }
        } catch (Exception e) { error = "Camera orientation unavailable: " + e.getMessage(); }
    }
    public boolean isBusy() { return busy; }
    public float[] getPoints() { return points.clone(); }
    public long getSequence() { return sequence; }
    public String getError() { return error; }
    public void submit(byte[] rgba, int width, int height) {
        if (busy) return;
        busy = true;
        int[] colors = new int[width * height];
        for (int i = 0; i < colors.length; i++) {
            int p = i * 4;
            colors[i] = 0xff000000 | ((rgba[p]&255)<<16) | ((rgba[p+1]&255)<<8) | (rgba[p+2]&255);
        }
        Bitmap bitmap = Bitmap.createBitmap(colors, width, height, Bitmap.Config.ARGB_8888);
        WindowManager window = (WindowManager)UnityPlayer.currentActivity.getSystemService(Context.WINDOW_SERVICE);
        int display = window.getDefaultDisplay().getRotation();
        int degrees = display == Surface.ROTATION_90 ? 90 : display == Surface.ROTATION_180 ? 180 : display == Surface.ROTATION_270 ? 270 : 0;
        final int rotation = (sensorOrientation - degrees + 360) % 360;
        final int orientedWidth = rotation == 90 || rotation == 270 ? height : width;
        final int orientedHeight = rotation == 90 || rotation == 270 ? width : height;
        detector.process(InputImage.fromBitmap(bitmap, rotation)).addOnSuccessListener(pose -> {
            float[] next = new float[52];
            for (int i = 0; i < ids.length; i++) {
                PoseLandmark l = pose.getPoseLandmark(ids[i]);
                if (l != null) {
                    float x = l.getPosition().x / orientedWidth;
                    float y = l.getPosition().y / orientedHeight;
                    // Return unrotated image coordinates; Unity applies AR camera crop/display mapping.
                    next[i*4] = rotation == 90 ? y : rotation == 180 ? 1-x : rotation == 270 ? 1-y : x;
                    next[i*4+1] = rotation == 90 ? 1-x : rotation == 180 ? 1-y : rotation == 270 ? x : y;
                    next[i*4+2] = l.getPosition3D().getZ();
                    next[i*4+3] = l.getInFrameLikelihood();
                }
            }
            points = next; sequence++; error = "";
        }).addOnFailureListener(e -> { error = "Pose detection failed: " + e.getMessage(); })
          .addOnCompleteListener(task -> { bitmap.recycle(); busy = false; });
    }
    public void close() { detector.close(); }
}
