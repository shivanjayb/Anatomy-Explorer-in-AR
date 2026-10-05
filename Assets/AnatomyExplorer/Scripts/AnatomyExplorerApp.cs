using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using TMPro;

namespace AnatomyExplorer
{
    public sealed class AnatomyExplorerApp : MonoBehaviour
    {
        public Transform anatomyRoot;
        public Camera viewCamera;
        public AnatomyTracking tracking;
        public GameObject arSession;
        public GameObject[] layers;
        public string[] layerNames = { "Muscles", "Skeleton", "Organs", "Nerves", "Blood vessels", "Joints", "Lymphatic" };
        public Material highlightMaterial;
        public float personHeight = 1.75f;
        public bool LiveMode { get; private set; }
        public bool PersonSelected { get; private set; }
        string selectedSubjectId;
        public int SelectedLayer { get; private set; }
        public AnatomyPart SelectedPart { get; private set; }
        public int StructureCount => parts.Count;
        public int QuizScore => quizScore;
        public int QuizAttempts => quizAttempts;
        readonly List<AnatomyPart> parts = new List<AnatomyPart>();
        readonly Dictionary<AnatomyPart, Vector3> originalScales = new Dictionary<AnatomyPart, Vector3>();
        TMP_Text statusText, countText, selectedTitle, selectedBody, modeText, quizText, pageText;
        TMP_InputField search;
        RectTransform listContent;
        Button[] layerButtons;
        readonly List<GameObject> listItems = new List<GameObject>();
        GameObject quizPanel;
        Button[] answers;
        AnatomyPart quizTarget;
        int quizScore, quizAttempts;
        int currentPage, pageCount;
        const int PageSize=80;
        bool quizAnswered;
        bool isolated, autoRotate, allSystems, showFascia;
        float yaw, zoom = 1f;
        Vector2 previousPointer;
        readonly Color ink = new Color(.055f,.085f,.13f);
        readonly Color panel = new Color(.065f,.105f,.16f,.96f);
        readonly Color accent = new Color(.16f,.88f,.74f);
        readonly Color muted = new Color(.64f,.72f,.80f);
        TMP_FontAsset font;
        Vector3 defaultCameraPosition;
        Quaternion defaultCameraRotation;
        // Reference standing pose, metres, with feet at y=0. Source meshes are segmented rigid structures.
        readonly Vector3[] rest = {
            new Vector3(0,1.62f,0), new Vector3(-.19f,1.43f,0),new Vector3(.19f,1.43f,0),
            new Vector3(-.27f,1.13f,0),new Vector3(.27f,1.13f,0),new Vector3(-.32f,.88f,0),new Vector3(.32f,.88f,0),
            new Vector3(-.10f,.91f,0),new Vector3(.10f,.91f,0),new Vector3(-.10f,.50f,0),new Vector3(.10f,.50f,0),
            new Vector3(-.10f,.08f,0),new Vector3(.10f,.08f,0)
        };
        readonly int[,] segmentJoints = { {1,3},{3,5},{2,4},{4,6},{7,9},{9,11},{8,10},{10,12} };
        void Awake()
        {
            if (anatomyRoot == null || viewCamera == null) return;
            defaultCameraPosition = new Vector3(.04f,.93f,3.2f);
            viewCamera.allowDynamicResolution=false;
            defaultCameraRotation = Quaternion.LookRotation(new Vector3(.04f,.9f,0)-defaultCameraPosition);
            parts.AddRange(anatomyRoot.GetComponentsInChildren<AnatomyPart>(true));
            foreach (var p in parts) originalScales[p] = p.transform.localScale;
            font = TMP_Settings.defaultFontAsset;
            if(font==null)font=TMP_FontAsset.CreateFontAsset(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
            BuildUI(); SetMode(Application.isMobilePlatform && !Application.isEditor); ShowLayer(0);
        }
        void Update()
        {
            if (anatomyRoot == null) return;
            if (!LiveMode)
            {
                viewCamera.transform.SetPositionAndRotation(defaultCameraPosition,defaultCameraRotation);
                int simulationLayer=LayerMask.NameToLayer("XR Simulation");
                if(simulationLayer>=0)viewCamera.cullingMask&=~(1<<simulationLayer);
                if (autoRotate) yaw += Time.deltaTime * 15;
                anatomyRoot.rotation = Quaternion.Euler(0,yaw,0);
                anatomyRoot.localScale = Vector3.one * zoom;
                HandlePointer();
            }
            else
            {
                tracking.assumedHeight = personHeight;
                if(!tracking.Pose.valid || (PersonSelected && selectedSubjectId!=tracking.Pose.subjectId)) ClearPersonSelection();
                ApplyPose(tracking.Pose);
                HandlePointer();
            }
            if (statusText != null)
                statusText.text = LiveMode ? (PersonSelected ? "PERSON SELECTED  •  Choose an anatomy layer  •  Clear person to select again" : tracking.Pose.valid ? "PERSON DETECTED  •  Tap the person to show anatomy" : tracking.Status) : "UNITY ATLAS PREVIEW  •  Drag to rotate  •  Scroll to zoom  •  Tap a structure";
        }
        void HandlePointer()
        {
            var pointer = Pointer.current;
            if (pointer == null) return;
            Vector2 position = pointer.position.ReadValue();
            bool overUI=false;
            if(EventSystem.current!=null)
            {
                var hits=new List<RaycastResult>();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=position},hits);
                overUI=hits.Any(h=>h.module is UnityEngine.UI.GraphicRaycaster);
            }
            if (pointer.press.wasPressedThisFrame)
            {
                previousPointer = position;
                if(!overUI && LiveMode && !PersonSelected) { TrySelectPerson(position); return; }
                if (!overUI && Physics.Raycast(viewCamera.ScreenPointToRay(position),out var hit,30))
                {
                    var p = hit.collider.GetComponent<AnatomyPart>(); if(p != null) SelectPart(p);
                }
            }
            if (!LiveMode && pointer.press.isPressed && !overUI)
            {
                yaw -= (position.x - previousPointer.x) * .35f; previousPointer = position;
            }
            if (!LiveMode && Mouse.current != null && !overUI)
                zoom = Mathf.Clamp(zoom + Mouse.current.scroll.ReadValue().y * .0006f,.55f,2.2f);
        }
        public void SetMode(bool live)
        {
            LiveMode = live; tracking.live = live;
            ClearPersonSelection();
            var origin=viewCamera.GetComponentInParent<Unity.XR.CoreUtils.XROrigin>(); if(origin!=null)origin.enabled=live;
            ResetPose();
            if (arSession != null) arSession.SetActive(live);
            var background = viewCamera.GetComponent<ARCameraBackground>(); if(background != null) background.enabled = live;
            var manager = viewCamera.GetComponent<ARCameraManager>(); if(manager != null) manager.enabled = live;
            var driver = viewCamera.GetComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>(); if(driver != null) driver.enabled = live;
            if (tracking.bodyManager != null) tracking.bodyManager.enabled = live && Application.platform == RuntimePlatform.IPhonePlayer;
            anatomyRoot.gameObject.SetActive(!live);
            viewCamera.clearFlags = live ? CameraClearFlags.SolidColor : CameraClearFlags.SolidColor;
            viewCamera.backgroundColor = ink;
            if (!live)
            {
                viewCamera.transform.SetPositionAndRotation(defaultCameraPosition,defaultCameraRotation);
                anatomyRoot.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
                anatomyRoot.localScale = Vector3.one;
            }
            if(modeText != null) modeText.text = live ? "REAR CAMERA / BODY OVERLAY" : "INTERACTIVE 3D ATLAS";
        }
        public void ClearPersonSelection()
        {
            PersonSelected=false;selectedSubjectId=null;
            if(LiveMode && anatomyRoot!=null)anatomyRoot.gameObject.SetActive(false);
        }
        public bool TrySelectPerson(Vector2 screenPoint)
        {
            var pose=tracking.Pose;
            if(!LiveMode || !pose.valid)return false;
            // One-person selection: require the tap to be near a detected body segment.
            Vector2[] projected=new Vector2[13];bool[] visible=new bool[13];
            for(int i=0;i<13;i++){var point=viewCamera.WorldToScreenPoint(pose.points[i]);projected[i]=point;visible[i]=point.z>0&&pose.confidence[i]>.4f;}
            int[,] segments={{0,1},{0,2},{1,2},{1,3},{3,5},{2,4},{4,6},{1,7},{2,8},{7,8},{7,9},{9,11},{8,10},{10,12}};
            float radius=visible[1]&&visible[2]?Mathf.Max(24,Vector2.Distance(projected[1],projected[2])*.55f):24;
            for(int i=0;i<segments.GetLength(0);i++)
            {
                int a=segments[i,0],b=segments[i,1];if(!visible[a]||!visible[b])continue;
                Vector2 delta=projected[b]-projected[a];float t=delta.sqrMagnitude>.001f?Mathf.Clamp01(Vector2.Dot(screenPoint-projected[a],delta)/delta.sqrMagnitude):0;
                if(Vector2.Distance(screenPoint,projected[a]+t*delta)>radius)continue;
                PersonSelected=true;selectedSubjectId=pose.subjectId;ApplyPose(pose);return true;
            }
            return false;
        }
        public void ShowLayer(int index)
        {
            if(index < 0 || index >= layers.Length) return;
            SelectedLayer = index; isolated = false; allSystems = false; ClearSelection();
            for(int i=0;i<layers.Length;i++) layers[i].SetActive(i==index);
            foreach(var p in parts) { p.gameObject.SetActive(true); ApplySurfaceVisibility(p); }
            if(layerButtons != null) for(int i=0;i<layerButtons.Length;i++) layerButtons[i].GetComponent<Image>().color = i==index ? new Color(.12f,.35f,.34f) : new Color(.10f,.16f,.22f);
            int n = parts.Count(p=>p.systemName==layerNames[index]);
            if(countText != null) countText.text = n + " named structures";
            RefreshList();
        }
        public void ShowAllSystems()
        {
            allSystems=true;isolated=false;ClearSelection();
            foreach(var layer in layers)layer.SetActive(true);
            foreach(var p in parts){p.gameObject.SetActive(true);ApplySurfaceVisibility(p);}
            countText.text=parts.Count+" structures / all systems";RefreshList();
        }
        public void SelectPart(AnatomyPart part)
        {
            if(part == null) return;
            ClearSelection(); SelectedPart = part;
            ApplySurfaceVisibility(part);
            if(part.meshRenderer != null) part.meshRenderer.sharedMaterial = highlightMaterial;
            selectedTitle.text = part.displayName;
            selectedBody.text = part.systemName.ToUpperInvariant() + "\n\n" + Explain(part) + "\n\nUse Isolate to examine this structure. Reset restores the complete layer.";
        }
        void ClearSelection()
        {
            var previous=SelectedPart;
            if(previous != null && previous.meshRenderer != null) previous.meshRenderer.sharedMaterial = previous.originalMaterial;
            SelectedPart = null;
            if(previous!=null)ApplySurfaceVisibility(previous);
            if(selectedTitle != null) selectedTitle.text = "Explore the human body";
            if(selectedBody != null) selectedBody.text = "Choose a body system, then tap a structure or search the atlas.\n\nMuscles are the starting layer. Each imported anatomical structure can be selected independently.";
        }
        public void ToggleFascia()
        {
            showFascia=!showFascia;
            foreach(var part in parts)ApplySurfaceVisibility(part);
        }
        void ApplySurfaceVisibility(AnatomyPart part)
        {
            bool fascia=part.systemName=="Muscles"&&part.displayName.IndexOf("fascia",StringComparison.OrdinalIgnoreCase)>=0&&part.displayName.IndexOf("fasciae",StringComparison.OrdinalIgnoreCase)<0;
            bool visible=!fascia||showFascia||part==SelectedPart;
            if(part.meshRenderer!=null)part.meshRenderer.enabled=visible;
            var collider=part.GetComponent<Collider>();if(collider!=null)collider.enabled=visible;
        }
        public void IsolateSelected()
        {
            if(SelectedPart == null) return;
            isolated = !isolated;
            foreach(var p in parts) if(allSystems||p.systemName==layerNames[SelectedLayer]) p.gameObject.SetActive(!isolated || p==SelectedPart);
        }
        public void ResetView()
        {
            yaw=0; zoom=1; autoRotate=false; ResetPose(); ShowLayer(SelectedLayer);
        }
        void ResetPose()
        {
            foreach(var p in parts) { p.transform.localPosition=p.restPosition; p.transform.localRotation=p.restRotation; p.transform.localScale=originalScales[p]; }
        }
        public void ApplyPose(AnatomyPose pose)
        {
            if(LiveMode && !PersonSelected){anatomyRoot.gameObject.SetActive(false);return;}
            if(!pose.valid) { if(LiveMode)ClearPersonSelection(); anatomyRoot.gameObject.SetActive(false); return; }
            anatomyRoot.gameObject.SetActive(true);
            Vector3 hips=(pose.points[7]+pose.points[8])*.5f;
            Vector3 shoulders=(pose.points[1]+pose.points[2])*.5f;
            Vector3 up=(shoulders-hips).normalized;
            Vector3 right=(pose.points[2]-pose.points[1]).normalized;
            Vector3 forward=Vector3.Cross(right,up).normalized;
            if(forward.sqrMagnitude < .5f) return;
            Quaternion rotation=Quaternion.LookRotation(forward,up);
            float scale=Mathf.Clamp(Vector3.Distance(hips,shoulders)/.52f,.5f,1.8f);
            anatomyRoot.SetPositionAndRotation(hips-rotation*new Vector3(0,.91f*scale,0),rotation);
            anatomyRoot.localScale=Vector3.one*scale;
            foreach(var p in parts)
            {
                if(p.segment < 0) continue;
                int a=segmentJoints[p.segment,0],b=segmentJoints[p.segment,1];
                if(pose.confidence[a] < .4f || pose.confidence[b] < .4f) { p.transform.localPosition=p.restPosition; p.transform.localRotation=p.restRotation; continue; }
                Vector3 start=anatomyRoot.InverseTransformPoint(pose.points[a]);
                Vector3 end=anatomyRoot.InverseTransformPoint(pose.points[b]);
                Quaternion delta=Quaternion.FromToRotation(rest[b]-rest[a],end-start);
                p.transform.localPosition=start+delta*(p.restPosition-rest[a]);
                p.transform.localRotation=delta*p.restRotation;
            }
        }
        public static string Explain(AnatomyPart p)
        {
            string n=p.displayName.ToLowerInvariant();
            if(n.Contains("deltoid"))return "The deltoid forms the rounded shoulder and helps lift the arm away from the body.";
            if(n.Contains("biceps brachii"))return "Biceps brachii bends the elbow and helps turn the forearm so the palm faces upward.";
            if(n.Contains("triceps brachii"))return "Triceps brachii straightens the elbow. It lies on the back of the upper arm.";
            if(n.Contains("pectoralis major"))return "Pectoralis major is a large chest muscle that brings the arm toward the body and rotates it inward.";
            if(n.Contains("rectus abdominis"))return "Rectus abdominis helps bend the trunk forward and supports the abdominal wall.";
            if(n.Contains("gluteus maximus"))return "Gluteus maximus extends the hip, particularly when rising, climbing, or running.";
            if(n.Contains("gastrocnemius"))return "Gastrocnemius is a calf muscle involved in pointing the foot downward and bending the knee.";
            if(n.Contains("heart"))return "The heart pumps blood through the pulmonary and systemic circulations.";
            if(n.Contains("lung"))return "The lungs exchange oxygen and carbon dioxide between air and blood.";
            if(n.Contains("liver"))return "The liver processes nutrients, produces bile, and performs many metabolic functions.";
            if(n.Contains("kidney"))return "The kidneys filter blood, form urine, and help regulate fluid and electrolyte balance.";
            if(n.Contains("stomach"))return "The stomach stores and mixes food and begins protein digestion.";
            if(n.Contains("femur"))return "The femur is the thigh bone. It connects the hip to the knee and supports body weight.";
            if(n.Contains("humerus"))return "The humerus is the upper-arm bone, extending from shoulder to elbow.";
            switch(p.systemName)
            {
                case "Muscles":return "A structure in the muscular system. Explore its shape and position relative to nearby bones and organs.";
                case "Skeleton":return "A structure in the skeletal system, which supports the body, protects organs, and provides attachment sites for muscles.";
                case "Organs":return "An internal anatomical structure. Use the organ layer to examine its position within the body.";
                case "Nerves":return "A structure in the nervous system, which carries and processes signals coordinating sensation and movement.";
                case "Blood vessels":return "A structure in the cardiovascular system, which circulates blood throughout the body.";
                case "Joints":return "A joint or associated connective structure. Joints connect bones and support movement or stability.";
                default:return "A structure in the lymphatic system, involved in fluid balance and immune function.";
            }
        }
        void BuildUI()
        {
            var canvasGO=new GameObject("Anatomy Interface",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform,false);
            canvasGO.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;canvasGO.GetComponent<Canvas>().pixelPerfect=true;
            var scaler=canvasGO.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1440,900);scaler.matchWidthOrHeight=.5f;
            var header=Panel(canvasGO.transform,"Header",new Vector2(0,1),new Vector2(1,1),new Vector2(0,-96),Vector2.zero,panel);
            Label(header,"ANATOMY / AR",30,accent,new Vector2(28,-14),new Vector2(350,40));
            modeText=Label(header,"",15,muted,new Vector2(30,-57),new Vector2(450,25));
            ButtonAt(header,"Clear person",new Vector2(-990,-25),new Vector2(190,45),ClearPersonSelection,true);
            ButtonAt(header,"Fascia on/off",new Vector2(-785,-25),new Vector2(175,45),ToggleFascia,true);
            ButtonAt(header,"All systems",new Vector2(-598,-25),new Vector2(175,45),ShowAllSystems,true);
            ButtonAt(header,"Atlas preview",new Vector2(-408,-25),new Vector2(170,45),()=>SetMode(false),true);
            ButtonAt(header,"Live body overlay",new Vector2(-225,-25),new Vector2(200,45),()=>SetMode(true),true);
            var left=Panel(canvasGO.transform,"Systems",new Vector2(0,0),new Vector2(0,1),new Vector2(18,95),new Vector2(250,-114),panel);
            Label(left,"BODY SYSTEMS",17,muted,new Vector2(18,-20),new Vector2(220,28));
            layerButtons=new Button[layers.Length];
            for(int i=0;i<layers.Length;i++){int idx=i;layerButtons[i]=ButtonAt(left,layerNames[i],new Vector2(16,-66-i*53),new Vector2(216,44),()=>ShowLayer(idx));}
            Label(left,"VIEW CONTROLS",15,muted,new Vector2(18,-461),new Vector2(220,26));
            ButtonAt(left,"Rotate / pause",new Vector2(16,-500),new Vector2(216,40),()=>autoRotate=!autoRotate);
            ButtonAt(left,"Front",new Vector2(16,-550),new Vector2(102,40),()=>yaw=0);
            ButtonAt(left,"Back",new Vector2(130,-550),new Vector2(102,40),()=>yaw=180);
            ButtonAt(left,"Zoom +",new Vector2(16,-600),new Vector2(102,40),()=>zoom=Mathf.Min(2.2f,zoom+.15f));
            ButtonAt(left,"Zoom −",new Vector2(130,-600),new Vector2(102,40),()=>zoom=Mathf.Max(.55f,zoom-.15f));
            var right=Panel(canvasGO.transform,"Inspector",new Vector2(1,0),new Vector2(1,1),new Vector2(-340,95),new Vector2(-18,-114),panel);
            selectedTitle=Label(right,"",22,Color.white,new Vector2(18,-18),new Vector2(286,67));
            selectedBody=Label(right,"",15,muted,new Vector2(18,-96),new Vector2(286,205));
            selectedBody.overflowMode=TextOverflowModes.Truncate;
            ButtonAt(right,"Isolate",new Vector2(18,-312),new Vector2(135,42),IsolateSelected);
            ButtonAt(right,"Reset",new Vector2(166,-312),new Vector2(135,42),ResetView);
            countText=Label(right,"",15,accent,new Vector2(18,-373),new Vector2(284,24));
            search=SearchField(right,new Vector2(18,-409),new Vector2(286,40)); search.onValueChanged.AddListener(_=>RefreshList());
            ButtonAt(right,"‹",new Vector2(18,-457),new Vector2(45,32),()=>{currentPage=Mathf.Max(0,currentPage-1);RefreshList(false);});
            pageText=Label(right,"",13,muted,new Vector2(73,-463),new Vector2(168,25));pageText.alignment=TextAlignmentOptions.Center;
            ButtonAt(right,"›",new Vector2(259,-457),new Vector2(45,32),()=>{currentPage=Mathf.Min(pageCount-1,currentPage+1);RefreshList(false);});
            var scrollGO=new GameObject("Structure list",typeof(RectTransform),typeof(Image),typeof(ScrollRect));scrollGO.transform.SetParent(right,false);
            var sr=scrollGO.GetComponent<RectTransform>(); sr.anchorMin=new Vector2(0,0);sr.anchorMax=new Vector2(1,1);sr.offsetMin=new Vector2(18,18);sr.offsetMax=new Vector2(-18,-503);
            scrollGO.GetComponent<Image>().color=new Color(.04f,.075f,.12f);
            var viewportGO=new GameObject("Viewport",typeof(RectTransform),typeof(Image),typeof(Mask)); viewportGO.transform.SetParent(sr,false);Stretch(viewportGO.GetComponent<RectTransform>());viewportGO.GetComponent<Image>().color=Color.white;viewportGO.GetComponent<Mask>().showMaskGraphic=false;
            var contentGO=new GameObject("Content",typeof(RectTransform));contentGO.transform.SetParent(viewportGO.transform,false);listContent=contentGO.GetComponent<RectTransform>();listContent.anchorMin=new Vector2(0,1);listContent.anchorMax=new Vector2(1,1);listContent.pivot=new Vector2(.5f,1);listContent.anchoredPosition=Vector2.zero;
            var scroll=scrollGO.GetComponent<ScrollRect>();scroll.viewport=viewportGO.GetComponent<RectTransform>();scroll.content=listContent;scroll.horizontal=false;scroll.scrollSensitivity=30;
            var bottom=Panel(canvasGO.transform,"Footer",Vector2.zero,new Vector2(1,0),Vector2.zero,new Vector2(0,78),panel);
            statusText=Label(bottom,"",15,muted,new Vector2(22,-13),new Vector2(990,27));
            Label(bottom,"Z-Anatomy / BodyParts3D  •  Educational atlas",12,muted,new Vector2(22,-44),new Vector2(800,23));
            ButtonAt(bottom,"Practice quiz",new Vector2(-200,-15),new Vector2(175,47),StartQuiz,true);
            quizPanel=Panel(canvasGO.transform,"Quiz",new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(-280,-215),new Vector2(280,215),panel).gameObject;
            quizText=Label(quizPanel.transform,"",22,Color.white,new Vector2(25,-20),new Vector2(510,85));
            answers=new Button[4];for(int i=0;i<4;i++){int idx=i; answers[i]=ButtonAt(quizPanel.transform,"",new Vector2(25,-115-i*53),new Vector2(510,45),()=>Answer(idx));}
            ButtonAt(quizPanel.transform,"Next",new Vector2(25,-355),new Vector2(245,45),NextQuestion);
            ButtonAt(quizPanel.transform,"Close",new Vector2(285,-355),new Vector2(250,45),()=>quizPanel.SetActive(false));quizPanel.SetActive(false);
        }
        void RefreshList(bool resetPage=true)
        {
            if(listContent==null)return;
            foreach(var item in listItems)Destroy(item);listItems.Clear();
            string query=search==null?"":search.text.Trim();
            var filtered=parts.Where(p=>(allSystems||p.systemName==layerNames[SelectedLayer])&&(query.Length==0||p.displayName.IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0)).OrderBy(p=>p.displayName).ToList();
            if(resetPage)currentPage=0;pageCount=Mathf.Max(1,Mathf.CeilToInt(filtered.Count/(float)PageSize));currentPage=Mathf.Clamp(currentPage,0,pageCount-1);
            if(pageText!=null)pageText.text="Page "+(currentPage+1)+" / "+pageCount;
            filtered=filtered.Skip(currentPage*PageSize).Take(PageSize).ToList();
            listContent.sizeDelta=new Vector2(0,filtered.Count*39);
            listContent.anchoredPosition=Vector2.zero;
            for(int i=0;i<filtered.Count;i++) {var p=filtered[i];var button=ButtonAt(listContent,p.displayName,new Vector2(3,-i*39),new Vector2(278,35),()=>SelectPart(p));button.GetComponentInChildren<TMP_Text>().fontSize=16;listItems.Add(button.gameObject);}
        }
        public void StartQuiz(){if(isolated)ResetView();quizPanel.SetActive(true);NextQuestion();}
        public void NextQuestion()
        {
            var pool=parts.Where(p=>allSystems||p.systemName==layerNames[SelectedLayer]).GroupBy(p=>p.displayName).Select(g=>g.First()).ToList();
            if(pool.Count<4){quizText.text="Choose a layer with at least four structures.";return;}
            quizAnswered=false;quizTarget=pool[UnityEngine.Random.Range(0,pool.Count)];SelectPart(quizTarget);
            selectedTitle.text="Identify the highlighted structure"; selectedBody.text="Choose its name in the practice quiz.";
            var choices=pool.Where(p=>p!=quizTarget).OrderBy(_=>UnityEngine.Random.value).Take(3).ToList();choices.Add(quizTarget);choices=choices.OrderBy(_=>UnityEngine.Random.value).ToList();
            quizText.text="Name the teal structure\nScore: "+quizScore+" / "+quizAttempts;
            for(int i=0;i<4;i++){answers[i].GetComponentInChildren<TMP_Text>().text=choices[i].displayName;answers[i].interactable=true;}
        }
        void Answer(int index)
        {
            if(quizAnswered||quizTarget==null)return;quizAnswered=true;quizAttempts++;
            bool correct=answers[index].GetComponentInChildren<TMP_Text>().text==quizTarget.displayName;if(correct)quizScore++;
            quizText.text=(correct?"Correct!":"Answer: "+quizTarget.displayName)+"\nScore: "+quizScore+" / "+quizAttempts;
            foreach(var b in answers)b.interactable=false;SelectPart(quizTarget);
        }
        RectTransform Panel(Transform parent,string name,Vector2 min,Vector2 max,Vector2 lower,Vector2 upper,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);var r=go.GetComponent<RectTransform>();r.anchorMin=min;r.anchorMax=max;r.offsetMin=lower;r.offsetMax=upper;go.GetComponent<Image>().color=color;return r;
        }
        TMP_Text Label(Transform parent,string text,int size,Color color,Vector2 position,Vector2 dimensions)
        {
            var go=new GameObject("Text",typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);var r=go.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=position;r.sizeDelta=dimensions;
            var t=go.GetComponent<TextMeshProUGUI>();t.font=font;t.fontSize=Mathf.Max(18,size);t.color=color;t.text=text;t.raycastTarget=false;t.overflowMode=TextOverflowModes.Ellipsis;return t;
        }
        Button ButtonAt(Transform parent,string text,Vector2 position,Vector2 dimensions,UnityEngine.Events.UnityAction action,bool right=false)
        {
            var go=new GameObject(text,typeof(RectTransform),typeof(Image),typeof(Button));go.transform.SetParent(parent,false);var r=go.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=new Vector2(right?1:0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=position;r.sizeDelta=dimensions;
            go.GetComponent<Image>().color=new Color(.10f,.16f,.22f);var b=go.GetComponent<Button>();b.onClick.AddListener(action);
            var label=Label(go.transform,text,16,Color.white,Vector2.zero,dimensions);label.alignment=TextAlignmentOptions.Center;return b;
        }
        TMP_InputField SearchField(Transform parent,Vector2 position,Vector2 size)
        {
            var go=new GameObject("Search",typeof(RectTransform),typeof(Image),typeof(TMP_InputField));go.transform.SetParent(parent,false);var r=go.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=position;r.sizeDelta=size;go.GetComponent<Image>().color=new Color(.12f,.18f,.25f);
            var field=go.GetComponent<TMP_InputField>();field.textViewport=r;field.textComponent=Label(go.transform,"",15,Color.white,new Vector2(10,-8),size-new Vector2(20,12));var placeholder=Label(go.transform,"Search structures…",15,muted,new Vector2(10,-8),size-new Vector2(20,12));field.placeholder=placeholder;return field;
        }
        static void Stretch(RectTransform r){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
    }
}
