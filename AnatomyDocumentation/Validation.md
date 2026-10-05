# Validation — 5 October 2026

Unity 6000.6.3f1 editor integration checks passed:

- 3,618 source structures initialize.
- Seven layers switch exclusively; combined view enables all seven.
- Selection changes the material; isolation leaves one structure; reset restores 683 muscular atlas structures.
- Deltoid search returns matching names.
- Quiz produces four distinct options; a correct selection increments score and attempts; answer buttons then lock.
- A synthetic provider-neutral pose produces finite transforms; invalid tracking hides the model. This is a synthetic state check, not a live camera test.
- Android and iOS C# tracking branches compiled through the Unity command compiler with their conditional sections enabled separately.
- Model file Git blob hashes match all seven entries in the upstream catalog.
- Actual Unity Game-view screenshots were captured and the full-body muscular atlas framing was visually checked.
- Fascia starts hidden, toggles on/off, appears when selected, and returns to the hidden state after reset. Directional lighting was adjusted to expose muscle detail.

Integration log: `ANATOMY_INTEGRATION_PASS: initialization, layer exclusivity, combined view, highlight, isolation, reset, search, quiz scoring and repeat guard, synthetic pose, lost tracking visibility.`

No native Android/Gradle or iOS/Xcode build was run. Both build modules were initially absent; after the user installed the iOS pack, Unity confirmed iOS build support = true. Android support remains absent. Camera mapping, live tracking, anatomical alignment, body motion, permissions, interruption recovery, and phone performance remain unverified.

Unity discarded several self-intersecting polygons while importing the upstream meshes. The atlas has not undergone independent anatomical or clinical validation. Source file bytes are unchanged; the presentation scene filters annotation/composite/helper meshes and uses new display materials.
# Play launch repair

Unity restored SampleScene at startup. The anatomy scene is now registered as the editor Play-mode start scene on project load and script reload. Removed the preview helper's attempt to set the read-only Game view scale property, which caused `ArgumentException: Set Method not found for 'scale'`.

After the repair, Unity's anatomy initialization and selection/UI checks passed (`ANATOMY_SELECTION_UI_PASS`), and the user confirmed Play works. Physical phone testing remains pending.
