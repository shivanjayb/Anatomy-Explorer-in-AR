# Anatomy Explorer AR — college demonstration

## Present it today in Unity

1. Open the existing project at `/Users/shiv/AR` in Unity 6000.6.3f1.
2. Open `Assets/AnatomyExplorer/Scenes/AnatomyExplorer.unity`.
3. Open the Game tab and press Play. Use a landscape Game view, ideally 1440 × 900 or 1920 × 1080. Maximize the Game tab if the controls appear small.
4. The atlas starts with Muscles. Drag over the body to rotate it; scroll or use Zoom to examine it.
   Fascia surfaces are hidden initially to expose the muscle detail. Use Fascia on/off to restore them; selecting a fascia structure also reveals it.
5. Choose Skeleton, Organs, Nerves, Blood vessels, Joints, or Lymphatic. All systems combines the imported layers.
6. Search for a structure, select its name, then use Isolate and Reset. Use the arrows to browse additional pages.
7. Practice quiz highlights a structure and offers four names. Next generates another question.

The atlas has 3,618 selectable source structures: 683 in the muscular atlas, 982 skeletal, 118 visceral, 582 nervous, 676 cardiovascular, 413 joint/connective, and 164 lymphatic. These are source atlas structures, not a claim of 3,618 distinct organs or complete coverage of every anatomical structure. The muscular atlas includes associated tendons and bursae. Heart structures are in Blood vessels and brain structures are in Nerves. All systems displays them together.

## Problem statement

Students studying anatomy from flat diagrams can find it difficult to understand the spatial relationships among muscles, bones, and internal organs. Anatomy Explorer provides an interactive 3D atlas and a mobile rear-camera body-overlay prototype, enabling users to examine anatomical layers, identify named structures, and practise recognition with quizzes.

## What is verified

- Models imported into the current Unity editor and scene assembled.
- Editor Play mode: all seven layers, selection, highlight, isolation, reset, and combined layers checked.
- Full-body atlas framing visually inspected using a real Unity screenshot.
- Imported model file hashes verified against the upstream GitHub catalog.

See `Validation.md` for the final checks. Quiz scoring and tracking checks must be described according to those recorded results.

## Mobile rear-camera overlay

On a phone, the app now opens in camera mode. Keep one person's entire body visible, then tap the person to select them. Anatomy stays hidden until that tap. Choose a body system to change the overlay; use **Clear person** to deselect. Losing body tracking hides the anatomy and requires another tap when the person is detected again. This is a single-person workflow, not multi-person identification or a silhouette mask.

UI text uses TextMesh Pro SDF fonts with larger labels. In Unity's Game view, open the **Free Aspect** dropdown and uncheck **Low Resolution Aspect Ratios**, then use Scale **1x** and a native landscape resolution, rather than magnifying a low-resolution image. The Device Simulator still previews the atlas and layout; its camera button cannot access an iPhone body tracker.

**iPhone:** Source uses AR Foundation `ARHumanBodyManager` and ARKit's 91-joint skeleton. It tracks one detected person's torso and articulates selected limb structures. Body tracking requires a supported physical iPhone; Unity's editor does not simulate a tracked person.

**Android:** Source includes an on-device ML Kit pose bridge using 33-landmark detection, mapped to 13 control points. AR Foundation supplies rear-camera frames. A Gradle build hook adds `com.google.mlkit:pose-detection:18.0.0-beta5`. Subject depth is estimated from an assumed 1.75 m height, so spatial alignment is approximate. ML Kit is a beta dependency.

Neither phone backend has been built or tested on a device in this workspace. Unity initially reported both build modules absent. After the user installed the iOS pack, Unity confirmed iOS build support = true; Android support remains absent. The selected target is still StandaloneOSX. This deliverable is a verified Unity atlas with mobile overlay source, not a verified cross-platform mobile app.

Body articulation moves individual structures rigidly. It does not deform muscle tissue, model joint mechanics, or achieve medically precise registration. There is no person segmentation/occlusion mask, multi-person selection, or clinical measurement. Low-confidence/lost tracking hides the anatomy. The first prototype expects the person's full body, including feet, to be visible.

## Build later

1. iOS Build Support is now installed. For Android, add Android Build Support (SDK/NDK/OpenJDK) through Unity Hub. iPhone deployment also needs the full Xcode app and device signing.
2. Use Build Profiles to switch to Android or iOS; keep AnatomyExplorer as the first enabled scene.
3. Confirm XR Plug-in Management uses ARCore on Android and ARKit on iOS. Both provider assets are already present in this project.
4. For Android, choose ARM64 and IL2CPP as appropriate for the device. The minimum API is 26. Build requires internet to resolve the bundled ML Kit dependency. Grant camera access and choose Live body overlay.
5. For iPhone, build the Xcode project, select your signing team, and deploy to a supported physical device. Camera usage text is configured. Choose Live body overlay.
6. Validate front-facing standing alignment, camera crop/orientation, arm/leg motion, loss/recovery, permission denial, pause/resume, memory, and performance before claiming phone support is verified.

## Source and model credits

Free models are from [LluisV/Z-Anatomy](https://github.com/LluisV/Z-Anatomy/tree/PC-Version/Resources/Models/FBX), revision `6c7f9016bd5899ac8edafd31b9900c151df42ed6`.

Credits and model license conditions are in `Assets/AnatomyExplorer/Models/ATTRIBUTION.md`. Exact source URLs, hashes, and sizes are in `Model-Provenance.json`. No generated anatomy models or paid assets were used.

Scene recreation menu: **Anatomy Explorer → Create demo scene**. Save other work and exit Play mode before rebuilding.
