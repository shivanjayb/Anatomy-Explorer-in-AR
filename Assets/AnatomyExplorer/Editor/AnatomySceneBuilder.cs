using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.XR.CoreUtils;

namespace AnatomyExplorer.Editor
{
    public static class AnatomySceneBuilder
    {
        public const string ScenePath = "Assets/AnatomyExplorer/Scenes/AnatomyExplorer.unity";
        static readonly string[] Files = { "MuscularSystem100", "SkeletalSystem100", "VisceralSystem100", "NervousSystem100", "CardioVascular41", "Joints100", "LymphoidOrgans100" };
        static readonly Color[] Colors = {new Color(.72f,.20f,.22f),new Color(.9f,.85f,.71f),new Color(.82f,.44f,.28f),new Color(.96f,.73f,.16f),new Color(.75f,.16f,.24f),new Color(.28f,.63f,.77f),new Color(.39f,.78f,.52f)};
        [MenuItem("Anatomy Explorer/Create demo scene")]
        public static void Build()
        {
            if(EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before rebuilding.");
            foreach(string file in Files) if(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AnatomyExplorer/Models/"+file+".fbx")==null) throw new InvalidOperationException("Missing model: "+file);
            Directory.CreateDirectory("Assets/AnatomyExplorer/Scenes");Directory.CreateDirectory("Assets/AnatomyExplorer/Materials");
            var previous=SceneManager.GetActiveScene(); if(previous.isDirty && !string.IsNullOrEmpty(previous.path))EditorSceneManager.SaveScene(previous);
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var appObject=new GameObject("Anatomy Explorer");var app=appObject.AddComponent<AnatomyExplorerApp>();
            var anatomy=new GameObject("Human Anatomy — Z-Anatomy");app.anatomyRoot=anatomy.transform;
            app.layers=new GameObject[Files.Length];
            int total=0;
            for(int i=0;i<Files.Length;i++)
            {
                var layer=new GameObject(app.layerNames[i]);layer.transform.SetParent(anatomy.transform,false);app.layers[i]=layer;
                var source=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AnatomyExplorer/Models/"+Files[i]+".fbx"));
                var material=MakeMaterial(app.layerNames[i],Colors[i]);
                var renderers=source.GetComponentsInChildren<MeshRenderer>();
                var allNames=new HashSet<string>(renderers.Select(r=>r.name));
                var usedDisplayNames=new HashSet<string>();
                foreach(var original in renderers)
                {
                    string n=original.name;
                    if(n.StartsWith("Cross Section",StringComparison.OrdinalIgnoreCase)||n.Contains("-profile"))continue;
                    var filter=original.GetComponent<MeshFilter>();if(filter==null||filter.sharedMesh==null||filter.sharedMesh.vertexCount==0)continue;
                    if(n.EndsWith(".j")||n.EndsWith(".i"))
                    {
                        // ".i"/".j" normally mark a duplicate annotation/helper copy of a structure that also
                        // exists under its plain name — drop those. But a handful of real organs (Brain, Cerebrum,
                        // the heart's surface patches, …) have NO plain-named sibling, so their ".i"/".j" copy is
                        // the only copy; dropping it unconditionally silently deletes the whole organ.
                        // However: some "sole copy" .i/.j meshes are themselves just a 24-vertex bounding-box
                        // stub Z-Anatomy uses as a collapsed-group marker (Brain.j, Thymus.j, Pelvic girdle.j, …),
                        // not real geometry — keeping those renders a flat floating box/line instead of an organ.
                        bool hasPlainSibling=allNames.Contains(n.Substring(0,n.Length-2));
                        bool isGroupMarkerStub=filter.sharedMesh.vertexCount==24;
                        if(hasPlainSibling||isGroupMarkerStub)continue;
                    }
                    var obj=new GameObject(n);obj.transform.SetParent(layer.transform,false);
                    obj.transform.SetPositionAndRotation(original.transform.position,original.transform.rotation);obj.transform.localScale=original.transform.lossyScale;
                    obj.AddComponent<MeshFilter>().sharedMesh=filter.sharedMesh;
                    var renderer=obj.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                    // A handful of source names are corrupted to literal "?" characters (encoding loss in the
                    // upstream export). Fall back to the parent's name rather than show "????????" in the UI.
                    string nameSource=n.Contains("?")&&original.transform.parent!=null?original.transform.parent.name:n;
                    string readableName=Readable(nameSource);
                    // A sole-surviving ".i"+".j" pair (two overlapping patches of one landmark), or a garbled
                    // name that fell back to its parent's name, renders fully but becomes one selectable entry.
                    if(!usedDisplayNames.Add(readableName))continue;
                    var collider=obj.AddComponent<MeshCollider>();collider.sharedMesh=filter.sharedMesh;
                    var part=obj.AddComponent<AnatomyPart>();part.displayName=readableName;part.systemName=app.layerNames[i];part.meshRenderer=renderer;part.originalMaterial=material;
                    // Flatten model hierarchy while retaining source geometry and anatomical coordinates.
                    part.restPosition=obj.transform.localPosition;part.restRotation=obj.transform.localRotation;part.segment=Segment(renderer.bounds.center,n,i);
                    total++;
                }
                UnityEngine.Object.DestroyImmediate(source);
                layer.SetActive(i==0);
            }
            app.highlightMaterial=MakeMaterial("Selection",new Color(.12f,.92f,.79f));
            var originGO=new GameObject("XR Origin");var origin=originGO.AddComponent<XROrigin>();
            origin.CameraYOffset=0;
            var offset=new GameObject("Camera Offset");offset.transform.SetParent(originGO.transform,false);origin.CameraFloorOffsetObject=offset;
            var cameraGO=new GameObject("Rear AR Camera");cameraGO.tag="MainCamera";cameraGO.transform.SetParent(offset.transform,false);
            var camera=cameraGO.AddComponent<Camera>();camera.nearClipPlane=.05f;camera.farClipPlane=30;camera.fieldOfView=42;
            camera.transform.position=new Vector3(.04f,.93f,3.2f);camera.transform.LookAt(new Vector3(.04f,.9f,0));
            cameraGO.AddComponent<AudioListener>();origin.Camera=camera;
            var cameraManager=cameraGO.AddComponent<ARCameraManager>();cameraManager.requestedFacingDirection=CameraFacingDirection.World;
            cameraGO.AddComponent<ARCameraBackground>();
            var driver=cameraGO.AddComponent<TrackedPoseDriver>();
            driver.positionInput=new UnityEngine.InputSystem.InputActionProperty(new UnityEngine.InputSystem.InputAction("Position",UnityEngine.InputSystem.InputActionType.Value,"<XRHMD>/centerEyePosition"));
            driver.rotationInput=new UnityEngine.InputSystem.InputActionProperty(new UnityEngine.InputSystem.InputAction("Rotation",UnityEngine.InputSystem.InputActionType.Value,"<XRHMD>/centerEyeRotation"));
            var bodies=originGO.AddComponent<ARHumanBodyManager>();bodies.pose3DRequested=true;bodies.pose3DScaleEstimationRequested=true;
            var session=new GameObject("AR Session");session.AddComponent<ARSession>();session.AddComponent<ARInputManager>();
            var tracking=appObject.AddComponent<AnatomyTracking>();tracking.cameraManager=cameraManager;tracking.bodyManager=bodies;tracking.viewCamera=camera;
            app.tracking=tracking;app.arSession=session;app.viewCamera=camera;
            var events=new GameObject("Event System");events.AddComponent<EventSystem>();events.AddComponent<InputSystemUIInputModule>();
            var key=new GameObject("Key Light").AddComponent<Light>();key.type=LightType.Directional;key.intensity=1.25f;key.transform.rotation=Quaternion.Euler(30,150,0);
            var fill=new GameObject("Fill Light").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.6f;fill.color=new Color(.66f,.8f,1);fill.transform.rotation=Quaternion.Euler(15,-30,0);
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.42f,.46f,.51f);
            // Keep the editor demonstration deterministic and independent of camera permissions.
            session.SetActive(false);cameraManager.enabled=false;cameraGO.GetComponent<ARCameraBackground>().enabled=false;driver.enabled=false;bodies.enabled=false;
            PlayerSettings.companyName="Anatomy Explorer";PlayerSettings.productName="Anatomy Explorer AR";
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android,"com.anatomyexplorer.app");PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS,"com.anatomyexplorer.app");
            PlayerSettings.defaultInterfaceOrientation=UIOrientation.LandscapeLeft;
            PlayerSettings.Android.minSdkVersion=AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.iOS.targetOSVersionString="14.0";PlayerSettings.iOS.cameraUsageDescription="The rear camera detects a person for the educational anatomy overlay.";
            EditorSceneManager.SaveScene(scene,ScenePath);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)}.Concat(EditorBuildSettings.scenes.Where(s=>s.path!=ScenePath)).ToArray();
            AssetDatabase.SaveAssets();
            Selection.activeGameObject=anatomy;
            if(SceneView.lastActiveSceneView!=null)SceneView.lastActiveSceneView.LookAt(new Vector3(0,.9f,0),Quaternion.Euler(0,180,0),2.2f);
            Debug.Log("ANATOMY_SCENE_READY: "+total+" selectable structures, 7 systems. Open "+ScenePath+" and press Play.");
        }
        static Material MakeMaterial(string name,Color color)
        {
            string path="Assets/AnatomyExplorer/Materials/"+name.Replace(" ","")+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}
            material.SetColor("_BaseColor",color);material.SetFloat("_Smoothness",.38f);material.SetFloat("_Metallic",.02f);material.SetFloat("_Cull",0);return material;
        }
        static string Readable(string n)
        {
            // Z-Anatomy suffix codes: plain .l/.r for side, or a variant marker (e/o, optionally numbered)
            // before the side letter (.e1l, .o2r, …), or a bare .i/.j with no side meaning at all.
            string suffix=null;
            int dot=n.LastIndexOf('.');
            if(dot>0&&dot<n.Length-1)
            {
                string code=n.Substring(dot+1);
                char last=code[code.Length-1];
                if(last=='i'||last=='j')
                {
                    n=n.Substring(0,dot);
                }
                else if(last=='l'||last=='r')
                {
                    string marker=code.Substring(0,code.Length-1);
                    if(marker.Length<=3&&marker.All(c=>c=='e'||c=='o'||char.IsDigit(c)))
                    {
                        suffix=last=='l'?" (left)":" (right)";
                        n=n.Substring(0,dot);
                    }
                }
            }
            if(n.StartsWith("(")&&n.EndsWith(")"))n=n.Substring(1,n.Length-2);
            n=n.Replace("_"," ");
            return suffix!=null?n+suffix:n;
        }
        static int Segment(Vector3 p,string name,int layer)
        {
            // Per-structure rigid articulation, not a deforming musculoskeletal simulation.
            // Torso organs and long vessels stay attached to the torso.
            if(layer==2||layer==4||layer==6)return -1;
            bool left=p.x<0; // Z-Anatomy's left side is at negative model X.
            if(p.y<.88f && Mathf.Abs(p.x)>.06f)return p.y>.47f?(left?4:6):(left?5:7);
            if(Mathf.Abs(p.x)>.18f && p.y>.87f && p.y<1.48f)return p.y>1.13f?(left?0:2):(left?1:3);
            return -1;
        }
    }
}
