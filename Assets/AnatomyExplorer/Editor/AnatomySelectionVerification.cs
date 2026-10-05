using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace AnatomyExplorer.Editor
{
    [InitializeOnLoad]
    public static class AnatomySelectionVerification
    {
        const string Request="AnatomyDocumentation/verify-selection.request";
        const string Pending="AnatomyExplorer.SelectionVerification.Pending";
        static double readyDeadline;
        static AnatomySelectionVerification()
        {
            EditorApplication.playModeStateChanged += OnPlay;
            EditorApplication.delayCall += CheckRequest;
        }
        [MenuItem("Anatomy Explorer/Verify person selection and UI")]
        public static void Run()
        {
            SessionState.SetBool(Pending,true);
            if(EditorApplication.isPlaying)EditorApplication.isPlaying=false;
            else OpenDemo();
        }
        static void OpenDemo()
        {
            var scene=SceneManager.GetActiveScene();
            if(scene.isDirty&&!string.IsNullOrEmpty(scene.path))EditorSceneManager.SaveScene(scene);
            EditorSceneManager.OpenScene(AnatomySceneBuilder.ScenePath);
            EditorApplication.isPlaying=true;
        }
        static void CheckRequest()
        {
            var gameViewType=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if(gameViewType!=null)
            {
                var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
                foreach(var window in Resources.FindObjectsOfTypeAll(gameViewType))
                {
                    gameViewType.GetProperty("lowResolutionForAspectRatios",flags)?.SetValue(window,false);
                    var zoom=gameViewType.GetField("m_ZoomArea",flags)?.GetValue(window);
                    zoom?.GetType().GetProperty("scale",flags)?.SetValue(zoom,Vector2.one);
                    ((EditorWindow)window).Repaint();
                }
            }
            if(File.Exists(Request)){File.Delete(Request);Run();}
            else if(SessionState.GetBool(Pending,false)&&EditorApplication.isPlaying)WaitForReady();
        }
        static void OnPlay(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(Pending,false))return;
            if(state==PlayModeStateChange.EnteredEditMode)EditorApplication.delayCall+=OpenDemo;
            if(state==PlayModeStateChange.EnteredPlayMode)WaitForReady();
        }
        static void WaitForReady(){readyDeadline=EditorApplication.timeSinceStartup+10;EditorApplication.update-=CheckReady;EditorApplication.update+=CheckReady;}
        static void CheckReady()
        {
            var app=UnityEngine.Object.FindAnyObjectByType<AnatomyExplorerApp>();
            if((app==null||app.StructureCount==0)&&EditorApplication.timeSinceStartup<readyDeadline)return;
            EditorApplication.update-=CheckReady;Verify();
        }
        static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        static void Verify()
        {
            if(!SessionState.GetBool(Pending,false))return;
            SessionState.SetBool(Pending,false);
            var app=UnityEngine.Object.FindAnyObjectByType<AnatomyExplorerApp>();
            try
            {
                Require(app!=null&&app.StructureCount==3618,"Scene did not initialize.");
                var texts=app.GetComponentsInChildren<TMP_Text>();
                Require(texts.Length>0&&texts.All(t=>t.font!=null),"SDF fonts are missing.");
                var search=app.GetComponentInChildren<TMP_InputField>();
                Require(search!=null,"Search input is missing.");
                search.text="deltoid";
                Require(app.GetComponentsInChildren<TMP_Text>().Any(t=>t.text.IndexOf("deltoid",StringComparison.OrdinalIgnoreCase)>=0),"TMP search failed.");
                search.text="";
                app.StartQuiz();var target=app.SelectedPart;int score=app.QuizScore;
                var quiz=app.transform.Find("Anatomy Interface/Quiz");
                var answer=quiz.GetComponentsInChildren<Button>().First(b=>b.GetComponentInChildren<TMP_Text>().text==target.displayName);
                answer.onClick.Invoke();Require(app.QuizScore==score+1,"TMP quiz scoring failed.");quiz.gameObject.SetActive(false);
                app.SetMode(true);app.tracking.enabled=false;
                var pose=app.tracking.Pose;
                Vector2[] uv={new Vector2(.5f,.82f),new Vector2(.42f,.68f),new Vector2(.58f,.68f),new Vector2(.39f,.54f),new Vector2(.61f,.54f),new Vector2(.38f,.43f),new Vector2(.62f,.43f),new Vector2(.46f,.46f),new Vector2(.54f,.46f),new Vector2(.46f,.29f),new Vector2(.54f,.29f),new Vector2(.46f,.12f),new Vector2(.54f,.12f)};
                for(int i=0;i<13;i++){pose.points[i]=app.viewCamera.ViewportToWorldPoint(new Vector3(uv[i].x,uv[i].y,3));pose.confidence[i]=1;}
                pose.subjectId="synthetic-person";pose.valid=true;
                app.ApplyPose(pose);Require(!app.anatomyRoot.gameObject.activeSelf,"Anatomy appeared before selection.");
                Require(!app.TrySelectPerson(new Vector2(Screen.width*.01f,Screen.height*.01f)),"Background tap incorrectly selected a person.");
                var tap=app.viewCamera.WorldToScreenPoint((pose.points[1]+pose.points[2])*.5f);
                Require(app.TrySelectPerson(tap)&&app.PersonSelected&&app.anatomyRoot.gameObject.activeSelf,"Person tap failed.");
                app.ShowLayer(1);Require(app.PersonSelected&&app.layers[1].activeSelf,"Layer change cleared person selection.");
                app.ClearPersonSelection();Require(!app.PersonSelected&&!app.anatomyRoot.gameObject.activeSelf,"Clear person failed.");
                Require(app.TrySelectPerson(tap),"Reselection failed.");
                pose.valid=false;app.ApplyPose(pose);Require(!app.PersonSelected&&!app.anatomyRoot.gameObject.activeSelf,"Tracking loss did not clear selection.");
                app.SetMode(false);app.tracking.enabled=true;app.ResetView();app.ShowLayer(0);
                File.WriteAllText("AnatomyDocumentation/Selection-Validation.md","# Single-person selection and UI validation\n\nPASS in Unity Editor: SDF text fonts; TMP search; quiz scoring; no anatomy before selection; background tap rejected; person tap accepted; layer change retains selection; Clear person hides overlay; tracking loss hides overlay and requires reselection.\n\nPerson selection was checked using a synthetic camera pose. Live phone camera selection and silhouette registration are not verified. Android uses one pose stream and does not identify a person biometrically.\n");
                ScreenCapture.CaptureScreenshot(Path.GetFullPath("AnatomyDocumentation/Unity-Sharp-UI.png"));
                Debug.Log("ANATOMY_SELECTION_UI_PASS");
            }
            catch(Exception e)
            {
                File.WriteAllText("AnatomyDocumentation/Selection-Validation.md","# Verification failed\n\n"+e);
                Debug.LogException(e);
                if(app!=null){app.SetMode(false);app.tracking.enabled=true;}
            }
        }
    }
}
