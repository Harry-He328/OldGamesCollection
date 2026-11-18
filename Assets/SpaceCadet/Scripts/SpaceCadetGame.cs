using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
namespace OldGames.SpaceCadet
{
    /// <summary>Self-contained basic pinball loop; rules are configurable approximations, not the original mission engine.</summary>
    public sealed class SpaceCadetGame : MonoBehaviour
    {
        public enum Phase { Ready, Playing, Captured, BetweenBalls, GameOver }
        public bool desktopEmbedded;
        public bool controlsEnabled = true;
        float clock;
        float Clock => desktopEmbedded ? clock : Time.time;
        public Rigidbody ball;
        public SpaceCadetFlipper[] flippers;
        public SpaceCadetTarget[] targets;
        public Transform shooterHandle;
        public Vector3 launchPosition=new Vector3(4.25f,.28f,-7.7f);
        public int ballsPerGame=3;
        public float ballSaveSeconds=6, maxSpeed=26;
        public float minimumLaunchSpeed=14, maximumLaunchSpeed=24;
        public Phase phase=Phase.Ready;
        public int score,ballNumber=1,multiplier=1;
        public int highScore;
        public bool paused;
        public bool ballInLauncher => phase==Phase.Ready;
        public bool gameOver => phase==Phase.GameOver;
        float charge, nextAction, launchTime, captureCooldown, nudgeHeat, lastNudge;
        bool tilted, charging;
        int bonus, bumperHits, rampPasses;
        float previousFixedStep;
        string status="READY FOR DEPLOYMENT";
        Vector3 handleHome, releasePosition, releaseVelocity;
        SpaceCadetZone releaseZone;
        readonly Dictionary<int,float> contactTimes=new Dictionary<int,float>();
        readonly Dictionary<string,float> rampEntries=new Dictionary<string,float>();
        readonly HashSet<string> lanes=new HashSet<string>();
        public float Charge => charge;
        public string Status => status;
        public int BumperHits => bumperHits;
        public int RampPasses => rampPasses;
        void Awake()
        {
            // Recover references in scenes saved before the V2 flipper component migration.
            flippers = System.Array.FindAll(GetComponentsInChildren<SpaceCadetFlipper>(true), f => f != null);
            if (flippers.Length == 0) flippers = FindObjectsByType<SpaceCadetFlipper>(FindObjectsSortMode.None);
            if (targets == null) targets = System.Array.Empty<SpaceCadetTarget>();
            if (ball == null)
            {
                foreach (var body in FindObjectsByType<Rigidbody>(FindObjectsSortMode.None))
                    if (body.name == "Pinball") { ball = body; break; }
            }
            if (ball == null) { Debug.LogError("Space Cadet: missing Pinball body in the desktop table prefab.", this); enabled = false; }
        }
        void Start()
        {
            previousFixedStep=Time.fixedDeltaTime;Time.fixedDeltaTime=1f/120;
            if(shooterHandle!=null)handleHome=shooterHandle.localPosition;
            highScore=PlayerPrefs.GetInt("OldGames.SpaceCadet.HighScore",0);
            NewGame();
        }
        void OnDestroy()
        {
            if(previousFixedStep>0)Time.fixedDeltaTime=previousFixedStep;
            if(paused && !desktopEmbedded)Time.timeScale=1;
        }
        public void NewGame()
        {
            paused=false;if(!desktopEmbedded)Time.timeScale=1;score=0;ballNumber=1;multiplier=1;bonus=0;bumperHits=0;rampPasses=0;
            contactTimes.Clear();rampEntries.Clear();lanes.Clear();
            foreach(var t in targets)if(t!=null)t.ResetTarget();
            Serve();
        }
        void Serve()
        {
            StopBall();ball.position=launchPosition;ball.rotation=Quaternion.identity;
            phase=Phase.Ready;charge=0;charging=false;tilted=false;nudgeHeat=0;nextAction=0;
            status="HOLD SPACE TO LAUNCH";
        }
        void StopBall()
        {
            if(!ball.isKinematic){ball.linearVelocity=Vector3.zero;ball.angularVelocity=Vector3.zero;}
            ball.isKinematic=true;
        }
        void Launch()
        {
            ball.isKinematic=false;ball.linearVelocity=Vector3.forward*Mathf.Lerp(minimumLaunchSpeed,maximumLaunchSpeed,charge);
            phase=Phase.Playing;launchTime=Clock;charge=0;status="FLIGHT IN PROGRESS";
        }
        void Update()
        {
            var k=(!desktopEmbedded || controlsEnabled) ? Keyboard.current : null;
            if(k!=null)
            {
                if(k.f2Key.wasPressedThisFrame)NewGame();
                if(k.pKey.wasPressedThisFrame){paused=!paused;if(!desktopEmbedded)Time.timeScale=paused?0:1;}
            }
            if(paused || (desktopEmbedded && !controlsEnabled))return;
            if(desktopEmbedded)clock += Time.deltaTime;
            nudgeHeat=Mathf.Max(0,nudgeHeat-Time.deltaTime*.32f);
            if(phase==Phase.BetweenBalls && Clock>=nextAction)Serve();
            if(phase==Phase.Captured && Clock>=nextAction)
            {
                ball.position=releasePosition;ball.isKinematic=false;ball.linearVelocity=releaseVelocity;
                phase=Phase.Playing;captureCooldown=Clock+1.3f;
                if(releaseZone!=null)releaseZone.Suppress(1.5f);
            }
            if(phase==Phase.Ready)
            {
                charging=k!=null&&k.spaceKey.isPressed;
                if(charging)charge=Mathf.Clamp01(charge+Time.deltaTime*.7f);
                if(k!=null&&k.spaceKey.wasReleasedThisFrame)Launch();
            }
            else charging=false;
            if(shooterHandle!=null)shooterHandle.localPosition=Vector3.Lerp(shooterHandle.localPosition,handleHome+Vector3.back*(charging?charge*.45f:0),Time.deltaTime*20);
            if(k!=null&&k.upArrowKey.wasPressedThisFrame&&phase==Phase.Playing&&Clock-lastNudge>.25f)Nudge();
            if(phase==Phase.Playing&&(ball.position.z<-10.2f||ball.position.y<-1.5f||Mathf.Abs(ball.position.x)>7))Drain();
        }
        void FixedUpdate()
        {
            if(paused || (desktopEmbedded && !controlsEnabled))return;
            var k=(!desktopEmbedded || controlsEnabled) ? Keyboard.current : null;
            foreach(var f in flippers)
            {
                if (f == null) continue;
                bool pressed=!tilted&&phase!=Phase.GameOver&&k!=null&&(f.left?(k.aKey.isPressed||k.leftShiftKey.isPressed||k.leftArrowKey.isPressed):(k.dKey.isPressed||k.rightShiftKey.isPressed||k.rightArrowKey.isPressed));
                f.Drive(pressed,Time.fixedDeltaTime);
            }
            if(phase!=Phase.Playing)return;
            ball.AddForce(new Vector3(0,-9.81f,-1.22f),ForceMode.Acceleration);
            ball.linearVelocity=Vector3.ClampMagnitude(ball.linearVelocity,maxSpeed);
        }
        void Nudge()
        {
            lastNudge=Clock;nudgeHeat+=1;
            if(nudgeHeat>=3){tilted=true;bonus=0;status="TILT  /  FLIPPERS DISABLED";return;}
            ball.AddForce(new Vector3(0,.10f,1.4f),ForceMode.VelocityChange);status=nudgeHeat>1.7f?"DANGER  /  TILT WARNING":"NUDGE";
        }
        public void Drain()
        {
            if(phase!=Phase.Playing)return;
            StopBall();
            if(!tilted&&Clock-launchTime<ballSaveSeconds){status="BALL SAVED";phase=Phase.BetweenBalls;nextAction=Clock+.8f;return;}
            int awarded=tilted?0:bonus*multiplier;score+=awarded;bonus=0;UpdateRecord();
            if(ballNumber>=ballsPerGame){phase=Phase.GameOver;status="GAME OVER  /  F2 TO RESTART";return;}
            ballNumber++;multiplier=1;lanes.Clear();rampEntries.Clear();
            foreach(var t in targets)t.ResetTarget();
            phase=Phase.BetweenBalls;nextAction=Clock+1.2f;status=awarded>0?"BONUS  +"+awarded.ToString("N0"):"BALL LOST";
        }
        void AddScore(int amount,string message)
        {
            if(tilted)return;score+=amount;bonus+=10;status=message;UpdateRecord();
        }
        void UpdateRecord()
        {
            if(score<=highScore)return;highScore=score;PlayerPrefs.SetInt("OldGames.SpaceCadet.HighScore",highScore);
        }
        public void Hit(SpaceCadetContact contact,Collision collision)
        {
            if(phase!=Phase.Playing||tilted)return;
            int id=contact.GetInstanceID();
            if(contactTimes.TryGetValue(id,out float time)&&Clock-time<.16f)return;
            contactTimes[id]=Clock;
            if(contact.target!=null)
            {
                if(!contact.target.Register())return;
                AddScore(contact.points*multiplier,contact.label);
                string bank=contact.target.bank;bool complete=true;int members=0;
                foreach(var t in targets)if(t.bank==bank){members++;if(!t.hit)complete=false;}
                if(complete&&members>1)
                {
                    multiplier=Mathf.Min(multiplier+1,5);AddScore(3000,"TARGET BANK COMPLETE  /  x"+multiplier);
                    foreach(var t in targets)if(t.bank==bank)t.ResetTarget();
                }
            }
            else if(contact.points>0){AddScore(contact.points*multiplier,contact.label);if(contact.kick>=4)bumperHits++;}
            if(contact.spinner!=null)contact.spinner.Spin(collision.relativeVelocity.magnitude);
            if(contact.kick>0)
            {
                Vector3 normal=collision.contactCount>0?collision.GetContact(0).normal:ball.position-contact.transform.position;
                if(Vector3.Dot(normal,ball.position-collision.GetContact(0).point)<0)normal=-normal;
                normal.y=0;if(normal.sqrMagnitude<.001f)normal=Vector3.forward;
                ball.AddForce(normal.normalized*contact.kick,ForceMode.VelocityChange);
            }
        }
        public void Enter(SpaceCadetZone zone)
        {
            if(phase!=Phase.Playing)return;
            if(zone.kind==TableZoneKind.Drain){Drain();return;}
            if(tilted)return;
            switch(zone.kind)
            {
                case TableZoneKind.Lane:
                    if(lanes.Add(zone.id))AddScore(zone.points*multiplier,"LANE  +"+(zone.points*multiplier));
                    else AddScore(100,"LANE  +100");
                    break;
                case TableZoneKind.RampEntry:rampEntries[zone.id]=Clock;break;
                case TableZoneKind.RampExit:
                    if(rampEntries.TryGetValue(zone.id,out float entered)&&Clock-entered<7)
                    {rampEntries.Remove(zone.id);rampPasses++;AddScore(5000*multiplier,"RAMP COMPLETE  +"+(5000*multiplier).ToString("N0"));}
                    break;
                case TableZoneKind.Wormhole:
                case TableZoneKind.BlackHole:
                case TableZoneKind.Hyperspace:
                    if(Clock<captureCooldown)return;
                    AddScore(zone.points*multiplier,zone.id.ToUpperInvariant());
                    StopBall();phase=Phase.Captured;nextAction=Clock+.65f;
                    releaseZone=zone.destination!=null?zone.destination.GetComponent<SpaceCadetZone>():zone;
                    releasePosition=(zone.destination!=null?zone.destination.position:zone.transform.position)+Vector3.up*.35f;
                    releaseVelocity=zone.ejectVelocity;
                    if(zone.destination==null)releasePosition+=releaseVelocity.normalized*.7f;
                    break;
            }
        }
        void OnGUI()
        {
            if(desktopEmbedded)return;
            float scale=Mathf.Clamp(Screen.height/900f,.65f,1.5f);GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));
            float w=Screen.width/scale,h=Screen.height/scale;
            var small=new GUIStyle(GUI.skin.label){fontSize=12,normal={textColor=new Color(.65f,.73f,.84f)}};
            var title=new GUIStyle(small){fontSize=25,fontStyle=FontStyle.Bold,normal={textColor=Color.white}};
            var digits=new GUIStyle(title){fontSize=36,normal={textColor=new Color(1,.69f,.27f)}};
            GUI.color=new Color(.025f,.04f,.065f,.95f);GUI.DrawTexture(new Rect(22,22,294,238),Texture2D.whiteTexture);GUI.color=Color.white;
            GUI.Label(new Rect(40,33,270,32),"SPACE CADET",title);GUI.Label(new Rect(41,67,270,22),"3D PINBALL  /  SPACE ACADEMY",small);
            GUI.Label(new Rect(40,92,270,49),score.ToString("N0"),digits);
            GUI.Label(new Rect(41,145,270,22),"BALL  "+ballNumber+" / "+ballsPerGame+"       MULTIPLIER  x"+multiplier,small);
            GUI.Label(new Rect(41,172,270,22),"BEST  "+highScore.ToString("N0"),small);
            GUI.Label(new Rect(41,203,270,40),paused?"PAUSED  /  P TO CONTINUE":status,small);
            if(charging){GUI.color=new Color(1,.69f,.27f);GUI.DrawTexture(new Rect(41,250,254*charge,3),Texture2D.whiteTexture);GUI.color=Color.white;}
            GUI.Label(new Rect(28,h-61,w-50,24),"RMB DRAG  Orbit     WHEEL  Zoom     R  Reset     1 / 2 / 3  Views     V  Turntable",small);
            GUI.Label(new Rect(28,h-38,w-50,24),"SPACE  Launch     A / D or SHIFT  Flippers     UP  Nudge     P  Pause     F2  New game",small);
        }
    }
}

