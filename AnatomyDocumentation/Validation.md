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

# Naming, missing-organ, and quiz-removal repair — 9 October 2026

The scene builder's ".i"/".j" suffix filter was unconditionally excluding every structure that carried either suffix, on the assumption that they only ever marked duplicate annotation/helper copies. That assumption was wrong for structures whose *only* mesh used that suffix: Brain, Brainstem, and Cerebrum (filed under Nerves) were entirely absent, along with roughly 1,300 other structures across the dataset. The filter now keeps a ".i"/".j" mesh when no plain-named sibling exists, and collapses true duplicate pairs (e.g. paired bone-landmark patches) into a single selectable/listed entry while still rendering both mesh halves so no surface geometry is lost.

Display names also leaked Z-Anatomy's raw internal codes into the UI — plain `.l`/`.r` was handled, but variant-numbered codes (`.e1l`, `.o2r`, `.e10l`, …) and bare `.i`/`.j` were not, and names wrapped in parentheses in the source data produced doubled parentheses once a side suffix was appended (e.g. `(Accessory parotid gland) (left)`). The name-cleanup logic now strips all of these correctly; a scripted audit across all 4,201 structures found no remaining raw-code leaks — the 6 names that still contain a `.` are genuine anatomical abbreviations (`abd.`, `ext.`, `ant.`, `post.`), not artifacts.

Total selectable structures: 4,201 (683 Muscles, 1,222 Skeleton, 288 Organs, 699 Nerves, 676 Blood vessels, 413 Joints, 220 Lymphatic), up from 3,618. The heart remains unmodeled as a whole organ — its surface meshes in the source FBX have zero vertices (empty placeholders); only the coronary vessels around it exist. That is a source-data gap, not something the scene-builder code can fix.

The practice quiz was removed from the app (fields, UI panel, and footer button) at the user's request — this is a single-person identification/demonstration tool, not a study quiz. The editor verification harness (`AnatomySelectionVerification.cs`) was updated to drop its quiz-scoring step and its exact `StructureCount==3618` assertion in favor of a sanity bound, since the exact count is now derived from the filter rather than a fixed historical figure.

## Correction — same day: the widened ".i"/".j" filter above was too loose

The fix just described treated every "sole copy" ".i"/".j" mesh as a real organ, but roughly 950 of them (including "Brain.j", "Cerebrum.j", "Thymus.j", and group names like "Pelvic girdle.j", "Abdominopelvic cavity.j") turned out to be 24-vertex bounding-box stubs Z-Anatomy uses as a collapsed-group marker, not real surface geometry — several of them degenerate (zero-thickness in one axis), which rendered as the thin radiating lines the user spotted sticking out of the body in the Game view. The filter now also excludes a sole ".i"/".j" copy when its mesh is exactly 24 vertices, since no real structure in this dataset naturally has that vertex count. The real sub-structures these umbrella stubs sat above (Midbrain, thymus lobes, individual pelvic bones, …) were never affected and remain selectable.

Separately, three structures in Blood vessels had names corrupted to literal `?` characters in the source FBX (`?x.l`, `?x.r`, `????????`) — an upstream encoding loss, not something introduced here. They have real geometry (not stubs), so rather than drop them they now fall back to displaying their parent structure's name.

Total selectable structures after this correction: 3,250 (683 Muscles, 612 Skeleton, 121 Organs, 584 Nerves, 673 Blood vessels, 413 Joints, 164 Lymphatic) — down from the 4,201 reported above, because that figure included the ~950 fake box-stub "structures." Brain and Cerebrum are consequently *not* selectable as one whole entry — only their real substructures are — same situation as the heart.
