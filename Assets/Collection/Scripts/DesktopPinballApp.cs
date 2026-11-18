using System.Collections.Generic;
using DesktopMinesweeper;
using OldGames.SpaceCadet;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace OldGames.Collection
{
    // The desktop remains loaded. A private physics world renders into a desktop RawImage.
    [DefaultExecutionOrder(200)]
    public sealed class DesktopPinballApp : MonoBehaviour
    {
        const int TableLayer=30;
        public static bool OwnsKeyboard { get; private set; }
        DesktopWindow window;
        RectTransform frame, viewport;
        GameObject simulationRoot;
        SpaceCadetGame game;
        TableOrbitCamera orbit;
        Camera renderCamera;
        RawImage picture;
        TMP_Text scoreLabel;
        RenderTexture texture;
        Scene simulationScene;
        PhysicsScene physics;
        SimulationMode previousSimulationMode;
        bool scriptedSimulation;
        bool focused;
        readonly List<RaycastResult> hits=new List<RaycastResult>();
        readonly Dictionary<Camera,int> cameraMasks=new Dictionary<Camera,int>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if(FindFirstObjectByType<DesktopPinballApp>()!=null)return;
            var mine=FindFirstObjectByType<MinesweeperController>(FindObjectsInactive.Include);
            if(mine==null)return;
            var desktopWindow=mine.GetComponentInParent<DesktopWindow>(true);
            if(desktopWindow==null)desktopWindow=FindFirstObjectByType<DesktopWindow>(FindObjectsInactive.Include);
            if(desktopWindow==null)return;
            var app=new GameObject("Desktop Pinball application").AddComponent<DesktopPinballApp>();
            app.Create(desktopWindow.transform.parent as RectTransform);
        }
        void Create(RectTransform desktop)
        {
            if(desktop==null){Destroy(gameObject);return;}
            DesktopPinballWindow authored=null;
            foreach(var candidate in desktop.GetComponentsInChildren<DesktopPinballWindow>(true))
            { authored=candidate; break; }
            if(authored==null)
            {
                Debug.LogError("The desktop scene has no authored Pinball window. The existing Minesweeper-style shell must be present.",this);
                Destroy(gameObject);return;
            }
            window=authored.shell;frame=(RectTransform)authored.transform;
            picture=authored.viewport;viewport=picture.rectTransform;scoreLabel=authored.scoreLabel;
            // The authored RawImage was serialized with a zero-size UV rect, which stretches a
            // single texel over the viewport. Force the full texture range.
            picture.uvRect=new Rect(0,0,1,1);
            window.Configure(new Vector2(420,360),authored.taskButton);
            var focus=viewport.GetComponent<PinballViewportFocus>();
            if(focus==null)focus=viewport.gameObject.AddComponent<PinballViewportFocus>();
            focus.app=this;
        }
        public void RestartGame() { if(game!=null)game.NewGame(); }
        public static void OpenFromDesktop(RectTransform desktop)
        {
            var app=FindFirstObjectByType<DesktopPinballApp>();
            if(app==null)
            {
                app=new GameObject("Desktop Pinball application").AddComponent<DesktopPinballApp>();
                app.Create(desktop);
            }
            app.Show();
        }
        public void Show()
        {
            if(window==null)return;
            if(game==null && !CreateSimulation())return;
            window.ShowWindow();Focus();renderCamera.enabled=true;
        }
        public void Focus(){focused=true;frame.SetAsLastSibling();OwnsKeyboard=true;}
        void Minimize(){focused=false;OwnsKeyboard=false;window.HideWindow();}
        void Close(){focused=false;OwnsKeyboard=false;window.CloseWindow();}
        bool CreateSimulation()
        {
            var prefab=Resources.Load<GameObject>("DesktopPinballTable");
            if(prefab==null){Debug.LogError("Desktop pinball table resource is missing.");return false;}
            // No scene loading or desktop replacement: only an isolated physics container.
            simulationScene=SceneManager.CreateScene("DesktopPinballPhysics",new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            // A local physics scene is stepped manually, which Unity only allows in Script mode.
            previousSimulationMode=Physics.simulationMode;
            Physics.simulationMode=SimulationMode.Script;
            scriptedSimulation=true;
            physics=simulationScene.GetPhysicsScene();simulationRoot=Instantiate(prefab);
            SceneManager.MoveGameObjectToScene(simulationRoot,simulationScene);
            foreach(var t in simulationRoot.GetComponentsInChildren<Transform>(true))t.gameObject.layer=TableLayer;
            game=simulationRoot.GetComponent<SpaceCadetGame>();game.desktopEmbedded=true;game.controlsEnabled=true;
            foreach(var camera in Camera.allCameras){cameraMasks[camera]=camera.cullingMask;camera.cullingMask&=~(1<<TableLayer);}
            var cameraGo=new GameObject("Pinball window camera");cameraGo.transform.SetParent(simulationRoot.transform,false);
            renderCamera=cameraGo.AddComponent<Camera>();renderCamera.cullingMask=1<<TableLayer;renderCamera.clearFlags=CameraClearFlags.SolidColor;renderCamera.backgroundColor=new Color(.025f,.035f,.055f);renderCamera.fieldOfView=48;renderCamera.farClipPlane=100;
            var data=renderCamera.GetUniversalAdditionalCameraData();data.SetRenderer(0);data.renderPostProcessing=false;data.renderShadows=true;
            orbit=cameraGo.AddComponent<TableOrbitCamera>();orbit.Apply();
            Light("Pinball key",new Vector3(48,-35,0),Color.white,1.6f);
            Light("Pinball fill",new Vector3(40,145,0),new Color(.7f,.8f,1),.9f);
            ResizeTexture();return true;
        }
        void Light(string name,Vector3 angle,Color color,float intensity)
        {
            var go=new GameObject(name);go.transform.SetParent(simulationRoot.transform,false);go.transform.rotation=Quaternion.Euler(angle);
            var light=go.AddComponent<Light>();light.type=LightType.Directional;light.cullingMask=1<<TableLayer;light.color=color;light.intensity=intensity;
        }
        void ResizeTexture()
        {
            Vector2 size=viewport.rect.size;int w=Mathf.Clamp(Mathf.RoundToInt(size.x),256,1920),h=Mathf.Clamp(Mathf.RoundToInt(size.y),256,1440);
            if(texture!=null&&texture.width==w&&texture.height==h)return;
            if(texture!=null){renderCamera.targetTexture=null;texture.Release();Destroy(texture);}
            texture=new RenderTexture(w,h,24){name="Pinball desktop view",antiAliasing=2};texture.Create();
            picture.texture=texture;renderCamera.targetTexture=texture;renderCamera.aspect=w/(float)h;
        }
        void Update()
        {
            if(frame==null)return;
            var mouse=Mouse.current;
            if(mouse!=null&&mouse.leftButton.wasPressedThisFrame&&EventSystem.current!=null)
            {
                var e=new PointerEventData(EventSystem.current){position=mouse.position.ReadValue()};hits.Clear();EventSystem.current.RaycastAll(e,hits);
                if(hits.Count>0)
                {
                    if(hits[0].gameObject.transform.IsChildOf(frame))Focus();
                    else focused=false;
                }
            }
            bool visible=frame.gameObject.activeInHierarchy;
            OwnsKeyboard=visible&&focused;
            if(game==null)return;
            game.controlsEnabled=OwnsKeyboard;
            renderCamera.enabled=visible;
            orbit.inputEnabled=OwnsKeyboard&&mouse!=null&&RectTransformUtility.RectangleContainsScreenPoint(viewport,mouse.position.ReadValue(),null);
            if(!visible)return;
            ResizeTexture();
            scoreLabel.text="SCORE "+game.score.ToString("N0")+"   BALL "+game.ballNumber+"/3   x"+game.multiplier+"   "+(game.paused?"PAUSED":game.Status);
        }
        void FixedUpdate()
        {if(game!=null&&OwnsKeyboard&&!game.paused&&physics.IsValid())physics.Simulate(Time.fixedDeltaTime);}
        void OnDestroy()
        {
            OwnsKeyboard=false;
            if(scriptedSimulation){Physics.simulationMode=previousSimulationMode;scriptedSimulation=false;}
            foreach(var entry in cameraMasks)if(entry.Key!=null)entry.Key.cullingMask=entry.Value;
            if(renderCamera!=null)renderCamera.targetTexture=null;
            if(texture!=null){texture.Release();Destroy(texture);}
            if(simulationScene.IsValid()&&simulationScene.isLoaded)SceneManager.UnloadSceneAsync(simulationScene);
        }
    }
    public sealed class PinballDesktopShortcut:MonoBehaviour,IPointerClickHandler
    {
        public DesktopPinballApp app;
        public void OnPointerClick(PointerEventData e){if(e.button==PointerEventData.InputButton.Left&&e.clickCount==2)app.Show();}
    }
    public sealed class PinballViewportFocus:MonoBehaviour,IPointerDownHandler
    {
        public DesktopPinballApp app;
        public void OnPointerDown(PointerEventData e){app.Focus();}
    }
}

