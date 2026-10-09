# Anatomy Explorer AR

Unity college anatomy project with an interactive 3D atlas and a mobile rear-camera body-overlay prototype.

## Open the demo

1. Open this repository in Unity **6000.6.3f1**.
2. Open `Assets/AnatomyExplorer/Scenes/AnatomyExplorer.unity` and press Play.
3. Choose Muscles, Skeleton, Organs, Nerves, Blood vessels, Joints, or Lymphatic.
4. Search and select structures, then isolate them for a closer look.

For a sharp editor preview, disable **Low Resolution Aspect Ratios** in the Game view's aspect dropdown and use **1x** scale.

## Camera prototype

The phone flow is: rear camera → detect one full-body person → tap that person → choose an anatomy layer. Clear person removes the overlay; tracking loss requires reselection.

iOS uses ARKit body tracking through AR Foundation. Android uses an on-device ML Kit pose bridge with estimated depth. This is a single-person workflow without a person silhouette mask or medically precise registration.

**Unity editor checks passed. Neither phone backend has been built or tested on a physical device.** iOS requires iOS Build Support, Xcode, signing, and a supported iPhone. Android requires Android Build Support and ARCore support.

See [setup and demo instructions](AnatomyDocumentation/START-HERE.md), [selection validation](AnatomyDocumentation/Selection-Validation.md), and [project validation](AnatomyDocumentation/Validation.md).

## Models and credits

The atlas contains 3,250 selectable source structures from Z-Anatomy / BodyParts3D. Downloaded FBX sources are included with their Unity metadata.

See [required attribution and asset license conditions](Assets/AnatomyExplorer/Models/ATTRIBUTION.md) and [source provenance](AnatomyDocumentation/Model-Provenance.json). Some components have noncommercial licenses; retain the credits and applicable ShareAlike terms.
