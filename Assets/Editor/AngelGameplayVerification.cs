using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Exercises actual runtime code in Play mode without a new test-framework dependency.
[InitializeOnLoad]
public static class AngelGameplayVerification
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly List<string> results = new List<string>();
    static double readyAt;
    static AngelGameplayVerification() { EditorApplication.update += Update; }
    [MenuItem("Titan/Verify Gameplay")]
    public static void Start()
    {
        if (!EditorApplication.isPlaying) TitanBuild.CreateScene();
        SessionState.SetBool("Angel.Verify",true);readyAt=EditorApplication.timeSinceStartup+1;
        EditorApplication.isPlaying=true;
    }
    static object Get(AngelBounceGame g,string name) => typeof(AngelBounceGame).GetField(name,Flags).GetValue(g);
    static void Set(AngelBounceGame g,string name,object value) => typeof(AngelBounceGame).GetField(name,Flags).SetValue(g,value);
    static void Call(AngelBounceGame g,string method,params object[] args) => typeof(AngelBounceGame).GetMethod(method,Flags).Invoke(g,args);
    static void Check(bool pass,string message)
    {
        results.Add((pass?"PASS ":"FAIL ")+message);
        if(!pass) throw new Exception(message);
    }
    static void Steps(AngelBounceGame g,int frames) {for(int i=0;i<frames;i++) Call(g,"Tick",1f/240);}
    static void Route(AngelBounceGame g,float targetAngle)
    {
        Call(g,"ResetLevel");g.Throw();
        for(int i=0;i<2100;i++)
        {
            if((int)Get(g,"bounces")==1)
            {
                Vector2 delta=new Vector2(-3.5f-(float)Get(g,"angelX"),1f-(float)Get(g,"angelY"));
                Set(g,"desiredMove",Vector2.ClampMagnitude(delta/(5f/240),1));
                Set(g,"desiredTurn",Mathf.Clamp((targetAngle-(float)Get(g,"angle"))/(75f/240),-1,1));
            }
            else {Set(g,"desiredMove",Vector2.zero);Set(g,"desiredTurn",0f);}
            Steps(g,1);
        }
    }
    static void Update()
    {
        if(!SessionState.GetBool("Angel.Verify",false) || !EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.timeSinceStartup<readyAt) return;
        var g=UnityEngine.Object.FindFirstObjectByType<AngelBounceGame>();if(!g) return;
        SessionState.SetBool("Angel.Verify",false);
        results.Clear();Gamepad pad=null;
        var audioSources=UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
        var muteStates=new bool[audioSources.Length];
        for(int i=0;i<audioSources.Length;i++){muteStates[i]=audioSources[i].mute;audioSources[i].mute=true;}
        try
        {
            Set(g,"paused",false);Call(g,"ShowTitle");
            Check(Get(g,"mode").ToString()=="Title","Title screen is available");
            var audio=g.GetComponent<AngelBounceAudio>();
            Check(audio.CurrentScene==AngelBounceAudio.SceneMusic.Title && audio.CurrentTrack.name=="Before_the_Sun_Breaks","Title selects Before the Sun Breaks");
            Call(g,"BeginGame");
            Check(audio.CurrentScene==AngelBounceAudio.SceneMusic.Action && audio.CurrentTrack.name=="Running_Toward_the_Sun","Start selects Running Toward the Sun");
            Call(g,"ResetLevel");Set(g,"paused",false);
            Check((int)Get(g,"bounces")==0 && (float)Get(g,"elapsed")==0,"Reset starts with zero bounces/time");
            Check(UnityEngine.Object.FindFirstObjectByType<VirtualStick>()!=null,"Virtual controller exists");
            foreach(string art in new[]{"Canyon","Characters","SupportingAngel","Actions","TitleLogo","Despair","Happy"})
                Check(Resources.Load<Texture2D>("Art/"+art)!=null,"Artwork loaded: "+art);
            Check(Resources.Load<AudioClip>("Audio/Boyoon")!=null,"Spoken boyoon clip imported");
            Check(UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Length>=3,"BGM, effect and voice audio sources exist");
            pad=InputSystem.AddDevice<Gamepad>("AngelVerificationPad");
            InputSystem.QueueStateEvent(pad,new GamepadState {leftStick=new Vector2(1,0)});InputSystem.Update();
            Call(g,"ReadInput");float initial=(float)Get(g,"angelX");Steps(g,120);
            Check((float)Get(g,"angelX")>initial+1,"Gamepad stick moves angel through actual input reader");
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.LeftShoulder));InputSystem.Update();
            Call(g,"ReadInput");float angle=(float)Get(g,"angle");Steps(g,60);
            Check((float)Get(g,"angle")>angle+5,"Gamepad shoulder rotates board");
            InputSystem.QueueStateEvent(pad,new GamepadState());InputSystem.Update();Call(g,"ReadInput");Call(g,"ResetLevel");Call(g,"ReadInput");
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.South));InputSystem.Update();Call(g,"ReadInput");
            Check(Get(g,"mode").ToString()=="Throwing","Gamepad south button starts throwing animation");
            g.Throw();Check(Get(g,"mode").ToString()=="Throwing","Throw spam does not restart a live throw");
            InputSystem.QueueStateEvent(pad,new GamepadState());InputSystem.Update();Call(g,"ReadInput");Steps(g,93);
            Check(Get(g,"mode").ToString()=="Flying","Windup releases package into ballistic flight");
            Steps(g,70);Check(((Vector2)Get(g,"velocity")).y<5,"Gravity reduces vertical velocity during free flight");
            Steps(g,1300);
            Check((int)Get(g,"bounces")==1,"Uncontrolled throw makes one genuine bounce");
            Check(Get(g,"mode").ToString()=="Lost","Leaving controls untouched cannot deliver the gift");
            Route(g,0);
            Check(Get(g,"mode").ToString()=="Lost","The same second position with a flat board misses the hands");
            Route(g,30);
            Check(Get(g,"mode").ToString()=="Lost","An overly steep second reflection also misses the hands");
            Route(g,12);
            Check((int)Get(g,"bounces")==2,"Player movement and angle create a two-bounce route");
            Check(Get(g,"mode").ToString()=="Won","A physically reachable skilled route delivers the gift");
            Check(audio.CurrentScene==AngelBounceAudio.SceneMusic.Result && audio.CurrentTrack.name=="A_Heart_in_a_Quiet_Room","Delivery selects A Heart in a Quiet Room");
            Call(g,"Animate",.016f);
            Check(((SpriteRenderer)Get(g,"womanRenderer")).sprite==(Sprite)Get(g,"womanHappy"),"Receiver switches to happy ending pose");
            Check(((GameObject)Get(g,"resultScreen")).activeSelf,"Happy ending card is displayed");
            Check(((SpriteRenderer)Get(g,"manRenderer")).sprite==(Sprite)Get(g,"manHappy") && ((SpriteRenderer)Get(g,"angelRenderer")).sprite==(Sprite)Get(g,"angelHappy"),"Man and angel celebrate delivery");
            // Reflect the same incoming state twice, changing only board angle.
            Call(g,"ResetLevel");Set(g,"mode",Enum.Parse(typeof(AngelBounceGame).GetNestedType("Mode",BindingFlags.NonPublic),"Flying"));
            Set(g,"angle",0f);Call(g,"SetAngel");var board=(Transform)Get(g,"paddle");var box=(Transform)Get(g,"crate");
            box.position=board.position+Vector3.up*.26f;Set(g,"velocity",new Vector2(0,-8));Steps(g,2);var flat=(Vector2)Get(g,"velocity");
            Check(flat.y>0 && flat.magnitude<=8.2f,"Restitution reflects upward without creating energy on stationary board");
            Check((int)Get(g,"bounces")==1,"Single contact increments bounce exactly once");
            Steps(g,10);Check((int)Get(g,"bounces")==1,"Separation prevents duplicate bounce counts");
            Call(g,"ResetLevel");Set(g,"mode",Enum.Parse(typeof(AngelBounceGame).GetNestedType("Mode",BindingFlags.NonPublic),"Flying"));
            Set(g,"angle",25f);Call(g,"SetAngel");Vector2 n=new Vector2(-Mathf.Sin(25*Mathf.Deg2Rad),Mathf.Cos(25*Mathf.Deg2Rad));
            box.position=(Vector2)board.position+n*.26f;Set(g,"velocity",new Vector2(0,-8));Steps(g,2);
            Check(((Vector2)Get(g,"velocity")).x < -2,"Tilted board redirects momentum to the left");
            // Passing above the hands must not trigger a catch or bend the flight towards the receiver.
            Call(g,"ResetLevel");Set(g,"mode",Enum.Parse(typeof(AngelBounceGame).GetNestedType("Mode",BindingFlags.NonPublic),"Flying"));
            box.position=new Vector3(-5.48f,1.8f,0);Set(g,"velocity",new Vector2(-6,0));Steps(g,2);
            Check(Get(g,"mode").ToString()=="Flying","A high shot crossing the receiver is not caught");
            Vector2 untouched=(Vector2)Get(g,"velocity");
            Check(Mathf.Abs(untouched.x+6)<.0001f && Mathf.Abs(untouched.y+9.2f/120)<.001f,"Free flight near woman contains gravity only, no attraction");
            Check(UnityEngine.Object.FindFirstObjectByType<AngelGiftModel>()!=null && UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Length>=20,"Gift uses real 3D mesh, satin bow and lighting");
            Check(UnityEngine.Object.FindFirstObjectByType<AngelImpactFeedback>()!=null && UnityEngine.Object.FindFirstObjectByType<TrailRenderer>()!=null,"Native impact particles and trail are present");
            // Touch ownership, simultaneous fingers and exit release.
            var stick=UnityEngine.Object.FindFirstObjectByType<VirtualStick>();Canvas.ForceUpdateCanvases();
            var e=new PointerEventData(EventSystem.current) {pointerId=51,position=RectTransformUtility.WorldToScreenPoint(null,stick.transform.position)+new Vector2(15,0)};
            ExecuteEvents.Execute(stick.gameObject,e,ExecuteEvents.pointerDownHandler);Check(stick.Value.x>0,"Touch joystick responds to owned pointer");
            var held=UnityEngine.Object.FindFirstObjectByType<HoldDirection>();var second=new PointerEventData(EventSystem.current){pointerId=52};
            ExecuteEvents.Execute(held.gameObject,second,ExecuteEvents.pointerDownHandler);
            Check(held.Held && stick.Value.x>0,"Two fingers move and rotate independently");
            ExecuteEvents.Execute(stick.gameObject,e,ExecuteEvents.pointerExitHandler);Check(stick.Value==Vector2.zero,"Leaving stick releases input");
            Call(g,"OnApplicationFocus",false);Check(!held.Held,"Focus loss releases held rotation");Call(g,"OnApplicationFocus",true);
            Call(g,"ResetLevel");g.Throw();Steps(g,100);Set(g,"elapsed",59.999f);Steps(g,2);
            Check(Get(g,"mode").ToString()=="Lost","60 second deadline stops flight");
            Call(g,"ResetLevel");Set(g,"angelX",-4.4f);Call(g,"SetAngel");g.Throw();Steps(g,1400);
            Check(Get(g,"mode").ToString()=="Lost","Missed package reaches failure state");
            Call(g,"Animate",.016f);
            Check(((SpriteRenderer)Get(g,"womanRenderer")).sprite==(Sprite)Get(g,"womanSad") && ((SpriteRenderer)Get(g,"manRenderer")).sprite==(Sprite)Get(g,"manSad") && ((SpriteRenderer)Get(g,"angelRenderer")).sprite==(Sprite)Get(g,"angelSad"),"All three characters show despair after package loss");
            Check(((GameObject)Get(g,"resultScreen")).activeSelf,"Despair ending card is displayed");
            Call(g,"ResetLevel");Check((int)Get(g,"bounces")==0 && (float)Get(g,"elapsed")==0 && stick.Value==Vector2.zero,"Retry clears gameplay and input state");
            Debug.Log("ANGEL_VERIFICATION_PASSED: "+results.Count+" checks");
        }
        catch(Exception ex) {results.Add("ERROR "+ex);Debug.LogError("ANGEL_VERIFICATION_FAILED: "+ex);}
        finally
        {
            if(pad!=null) InputSystem.RemoveDevice(pad);
            for(int i=0;i<audioSources.Length;i++)if(audioSources[i]){audioSources[i].Stop();audioSources[i].mute=muteStates[i];}
            Call(g,"ResetLevel");
            string day=Directory.GetParent(Application.dataPath).Parent.FullName;Directory.CreateDirectory(Path.Combine(day,"QA"));
            File.WriteAllLines(Path.Combine(day,"QA/gameplay-checks.txt"),results);
            var model=UnityEngine.Object.FindFirstObjectByType<AngelGiftModel>();
            if(model)
            {
                var camera=(Camera)typeof(AngelGiftModel).GetField("previewCamera",Flags).GetValue(model);
                camera.Render();var previous=RenderTexture.active;RenderTexture.active=model.Preview;
                var image=new Texture2D(256,256,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,256,256),0,0);image.Apply();
                File.WriteAllBytes(Path.Combine(day,"QA/gift-3d.png"),image.EncodeToPNG());UnityEngine.Object.Destroy(image);RenderTexture.active=previous;
            }
            ScreenCapture.CaptureScreenshot(Path.Combine(day,"QA/gameplay.png"));
        }
    }
}
