using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using System.Collections.Generic;

public sealed class AngelBounceGame : MonoBehaviour
{
    enum Mode { Title, Ready, Throwing, Flying, Won, Lost }
    Mode mode;
    [Header("Physics (world units / seconds)")]
    [SerializeField, Min(.1f)] float gravity = 9.2f;
    [SerializeField, Range(0,1)] float restitution = .94f;
    [SerializeField, Range(0,1)] float surfaceFriction = .025f;
    const float Radius = .24f, HalfBoard = .94f, Step = 1f / 240f;
    const float CatchX = -5.5f, CatchY = 1.12f, CatchHalfHeight = .24f;
    float spin = -85;
    AngelGiftModel giftModel;
    AngelImpactFeedback feedback;
    LineRenderer catchRing;
    static Sprite white, circle;
    Camera cam;
    Transform angel, paddle, crate, crateVisual, man, woman;
    SpriteRenderer manRenderer, womanRenderer, angelRenderer;
    Sprite manIdle, womanIdle, manThrow, womanCatch, supportingAngel;
    Sprite womanSad, manSad, angelSad, womanHappy, manHappy, angelHappy;
    GameObject resultScreen, endingGift;
    Text endingTitle, endingLine;
    Image[] endingPortraits;
    Image endingShade;
    CanvasGroup endingGroup;
    readonly List<GameObject> boardPieces = new List<GameObject>();
    Vector2 velocity;
    float angelX = 1, angelY = -.1f, angle = 0, elapsed, accumulator, phaseTime, squash, impactTime;
    float desiredTurn;
    Vector2 desiredMove;
    int bounces;
    bool paused, armed;
    Text status, counter, hints, muteLabel, titleMute;
    GameObject titleScreen;
    RectTransform logoRect;
    string announcement;
    RectTransform safe;
    Font font;
    VirtualStick stick;
    AngelBounceAudio audioFx;
    readonly List<HoldDirection> holds = new List<HoldDirection>();
    readonly List<Sprite> ownedSprites = new List<Sprite>();
    readonly Queue<Vector3> trail = new Queue<Vector3>();
    Transform[] trailDots;
    Text bounceWord;
    Transform bounceCaption;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Launch()
    {
        if (!FindFirstObjectByType<AngelBounceGame>()) new GameObject("ANGEL BOUNCE").AddComponent<AngelBounceGame>();
    }
    static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
    static Sprite White()
    {
        if (white) return white;
        var t = new Texture2D(1,1); t.SetPixel(0,0,Color.white); t.Apply();
        return white = Sprite.Create(t, new Rect(0,0,1,1), Vector2.one * .5f, 1);
    }
    static Sprite Circle()
    {
        if (circle) return circle;
        const int size = 64; var t = new Texture2D(size,size); var pixels = new Color[size*size];
        for (int y=0;y<size;y++) for (int x=0;x<size;x++) pixels[y*size+x] = Vector2.Distance(new Vector2(x,y),new Vector2(31.5f,31.5f)) < 31 ? Color.white : Color.clear;
        t.SetPixels(pixels); t.Apply(); return circle = Sprite.Create(t,new Rect(0,0,size,size),Vector2.one*.5f,size);
    }
    GameObject Shape(string label, Transform parent, Vector2 position, Vector2 size, string color, int order)
    {
        var g = new GameObject(label); g.transform.SetParent(parent,false); g.transform.localPosition=position; g.transform.localScale=new Vector3(size.x,size.y,1);
        var sr=g.AddComponent<SpriteRenderer>(); sr.sprite=White(); sr.color=Hex(color); sr.sortingOrder=order;
        var mat=Resources.Load<Material>("SpriteBase"); if(mat) sr.sharedMaterial=mat;
        return g;
    }
    Sprite Slice(string resource, float start, float end, Vector2 pivot, float worldHeight, bool trim = true)
    {
        var t=Resources.Load<Texture2D>(resource);
        if(!t) throw new System.InvalidOperationException("Missing game artwork: "+resource);
        int x0=Mathf.RoundToInt(t.width*start), x1=Mathf.RoundToInt(t.width*end), y0=0,y1=t.height;
        if(trim && t.isReadable)
        {
            var pixels=t.GetPixels32(); int lx=x1,rx=x0,by=y1,ty=0;
            for(int y=0;y<t.height;y++) for(int x=x0;x<x1;x++) if(pixels[y*t.width+x].a>32) {lx=Mathf.Min(lx,x);rx=Mathf.Max(rx,x);by=Mathf.Min(by,y);ty=Mathf.Max(ty,y);}
            if(rx>=lx) {x0=lx;x1=rx+1;y0=by;y1=ty+1;}
        }
        var sprite=Sprite.Create(t,new Rect(x0,y0,x1-x0,y1-y0),pivot,(y1-y0)/worldHeight,0,SpriteMeshType.FullRect);
        ownedSprites.Add(sprite); return sprite;
    }
    SpriteRenderer Picture(string label, Transform parent, Sprite sprite, Vector2 position, int order)
    {
        var g=Shape(label,parent,position,Vector2.one,"#FFFFFF",order); var sr=g.GetComponent<SpriteRenderer>(); sr.sprite=sprite;return sr;
    }
    void Awake()
    {
        Application.targetFrameRate=60; Application.runInBackground=true;
        Screen.sleepTimeout=SleepTimeout.NeverSleep;
        cam=Camera.main;
        if(!cam) {cam=new GameObject("Main Camera").AddComponent<Camera>();cam.tag="MainCamera";}
        var clearCamera=new GameObject("Full-screen background clear").AddComponent<Camera>();clearCamera.transform.SetParent(transform,false);
        clearCamera.clearFlags=CameraClearFlags.SolidColor;clearCamera.backgroundColor=Hex("#16293D");clearCamera.cullingMask=0;clearCamera.depth=-100;
        cam.depth=0;
        cam.orthographic=true;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Hex("#F8BC83");cam.transform.position=new Vector3(0,0,-10);
        if(!FindFirstObjectByType<AudioListener>()) cam.gameObject.AddComponent<AudioListener>();
        audioFx=gameObject.AddComponent<AngelBounceAudio>();
        BuildWorld(); BuildUI(); ResetLevel(); ShowTitle();
    }
    void BuildWorld()
    {
        var stage=Picture("16-bit sandstone canyon",transform,Slice("Art/Canyon",0,1,Vector2.one*.5f,12,false),new Vector2(0,-1.4f),0);
        stage.transform.localScale=new Vector3(17/stage.sprite.bounds.size.x,1,1);
        womanIdle=Slice("Art/Characters",0,.29f,new Vector2(.5f,0),2.45f);
        manIdle=Slice("Art/Characters",.30f,.55f,new Vector2(.5f,0),2.45f);
        womanCatch=Slice("Art/Actions",0,.5f,new Vector2(.5f,0),2.35f);
        manThrow=Slice("Art/Actions",.5f,1,new Vector2(.5f,0),2.4f);
        womanSad=Slice("Art/Despair",0,1f/3,new Vector2(.5f,0),1.55f);
        manSad=Slice("Art/Despair",1f/3,2f/3,new Vector2(.5f,0),1.65f);
        angelSad=Slice("Art/Despair",2f/3,1,new Vector2(.5f,.54f),3.1f);
        womanHappy=Slice("Art/Happy",0,.29f,new Vector2(.5f,0),2.45f);
        manHappy=Slice("Art/Happy",.29f,.57f,new Vector2(.5f,0),2.45f);
        angelHappy=Slice("Art/Happy",.57f,1,new Vector2(.5f,.54f),3.1f);
        woman=new GameObject("Woman - receiver").transform;woman.SetParent(transform,false);woman.localPosition=new Vector3(-6.4f,-.68f,0);
        womanRenderer=Picture("Woman pose",woman,womanIdle,Vector2.zero,5);
        man=new GameObject("Man - thrower").transform;man.SetParent(transform,false);man.localPosition=new Vector3(6.4f,-.68f,0);
        manRenderer=Picture("Man pose",man,manIdle,Vector2.zero,5);
        angel=new GameObject("Angel - player").transform;angel.SetParent(transform,false);
        paddle=new GameObject("Reflector - held in BOTH hands").transform;paddle.SetParent(angel,false);paddle.localPosition=new Vector3(0,-.85f,0);
        // The sprite pivots at its palm line. Body + hands and board share one rigid support rig.
        supportingAngel=Slice("Art/SupportingAngel",0,1,new Vector2(.5f,.54f),3.1f);
        angelRenderer=Picture("Angel supporting board",paddle,supportingAngel,new Vector2(0,-.14f),9);
        Shape("Board outline",paddle,Vector2.zero,new Vector2(2.42f,.22f),"#382343",10);
        Shape("Board copper edge",paddle,new Vector2(0,.005f),new Vector2(2.34f,.15f),"#BF773F",11);
        Shape("Board gold face",paddle,new Vector2(0,.065f),new Vector2(2.30f,.07f),"#F8D57D",12);
        for(int i=0;i<15;i++) Shape("Board inlay",paddle,new Vector2(-1.05f+i*.15f,-.015f),new Vector2(.07f,.045f),"#895139",12);
        foreach(Transform part in paddle) if(part!=angelRenderer.transform)
        {
            part.localPosition=new Vector3(part.localPosition.x*.8f,part.localPosition.y,part.localPosition.z);
            part.localScale=new Vector3(part.localScale.x*.8f,part.localScale.y,part.localScale.z);
            boardPieces.Add(part.gameObject);
        }
        crate=new GameObject("Supply package").transform;crate.SetParent(transform,false);
        crateVisual=new GameObject("Package squash visual").transform;crateVisual.SetParent(crate,false);
        crateVisual.localPosition=new Vector3(0,0,-1.2f);
        var model=new GameObject("Lit 3D parcel model");model.transform.SetParent(crateVisual,false);
        model.transform.localScale=Vector3.one*.44f;model.transform.localRotation=Quaternion.Euler(18,28,0);
        giftModel=model.AddComponent<AngelGiftModel>();giftModel.Build();
        feedback=gameObject.AddComponent<AngelImpactFeedback>();feedback.Build(cam,crate);
        var goal=new GameObject("Hand-level receiving window");goal.transform.SetParent(transform,false);
        catchRing=goal.AddComponent<LineRenderer>();catchRing.sharedMaterial=Resources.Load<Material>("SpriteBase");
        catchRing.useWorldSpace=true;catchRing.loop=true;catchRing.positionCount=32;catchRing.startWidth=catchRing.endWidth=.025f;
        catchRing.startColor=catchRing.endColor=new Color(1,.83f,.4f,.8f);catchRing.sortingOrder=25;
        for(int i=0;i<32;i++) {float r=i*Mathf.PI*2/32;catchRing.SetPosition(i,new Vector3(CatchX+Mathf.Cos(r)*CatchHalfHeight,CatchY+Mathf.Sin(r)*CatchHalfHeight,-.2f));}
        trailDots=new Transform[12];
        for(int i=0;i<trailDots.Length;i++) {var g=Shape("Package trail",transform,Vector2.zero,Vector2.one*.06f,"#FFF0B0",13);g.GetComponent<SpriteRenderer>().sprite=Circle();trailDots[i]=g.transform;g.SetActive(false);}
        var captionCanvas=new GameObject("Bounce speech",typeof(Canvas));captionCanvas.transform.SetParent(transform,false);
        var c=captionCanvas.GetComponent<Canvas>();c.renderMode=RenderMode.WorldSpace;c.sortingOrder=50;
        captionCanvas.transform.localScale=Vector3.one*.008f;bounceCaption=captionCanvas.transform;
        var rt=(RectTransform)captionCanvas.transform;rt.sizeDelta=new Vector2(320,100);
    }
    void ResetLevel()
    {
        if(titleScreen) titleScreen.SetActive(false);
        if(resultScreen) resultScreen.SetActive(false);
        foreach(var part in boardPieces) part.SetActive(true);
        if(angelRenderer) {angelRenderer.sprite=supportingAngel;angelRenderer.transform.localRotation=Quaternion.identity;}
        if(giftModel)giftModel.ShowPreview(false);
        if(feedback)feedback.ResetEffects();
        if(catchRing)catchRing.enabled=true;
        spin=-85;
        audioFx.SetScene(AngelBounceAudio.SceneMusic.Action);
        mode=Mode.Ready;elapsed=0;accumulator=0;phaseTime=0;bounces=0;squash=0;impactTime=0;
        angelX=1;angelY=-.1f;angle=0;velocity=new Vector2(-3.2f,5);armed=false;
        ReleaseControls(); SetAngel(); crate.rotation=Quaternion.identity;crateVisual.localScale=Vector3.one;crate.position=new Vector3(5.95f,.9f,0);
        man.rotation=woman.rotation=Quaternion.identity;manRenderer.sprite=manIdle;womanRenderer.sprite=womanIdle;
        trail.Clear();foreach(var dot in trailDots) dot.gameObject.SetActive(false);
        announcement="まず上へ反射 → 左へ飛んで、手元の光の輪を狙おう。";
    }
    public void Throw()
    {
        if(mode==Mode.Title) {BeginGame();return;}
        if(paused || mode==Mode.Throwing || mode==Mode.Flying) return;
        audioFx.Throw();
        if(mode==Mode.Won || mode==Mode.Lost) ResetLevel();
        mode=Mode.Throwing;phaseTime=0;armed=true;
        announcement="振りかぶって……投げる！";
    }
    void ShowTitle()
    {
        mode=Mode.Title;if(feedback)feedback.ResetEffects();if(giftModel)giftModel.ShowPreview(false);ReleaseControls();if(resultScreen)resultScreen.SetActive(false);titleScreen.SetActive(true);
        audioFx.SetScene(AngelBounceAudio.SceneMusic.Title);
    }
    void BeginGame() {audioFx.BeginAudio();ResetLevel();}
    void ReadInput()
    {
        desiredMove=stick?stick.Value:Vector2.zero;desiredTurn=0;
        if(mode==Mode.Title)
        {
            var titleKeyboard=Keyboard.current;var titlePad=Gamepad.current;
            if((Mouse.current!=null && Mouse.current.leftButton.wasPressedThisFrame) ||
               (Touchscreen.current!=null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)) audioFx.BeginAudio();
            if((titleKeyboard!=null && (titleKeyboard.spaceKey.wasPressedThisFrame || titleKeyboard.enterKey.wasPressedThisFrame)) ||
               (titlePad!=null && (titlePad.buttonSouth.wasPressedThisFrame || titlePad.startButton.wasPressedThisFrame))) BeginGame();
            return;
        }
        if(!armed) {var gp=Gamepad.current; if(gp==null || (!gp.buttonSouth.isPressed && !gp.buttonEast.isPressed && !gp.startButton.isPressed)) armed=true;}
#if ENABLE_INPUT_SYSTEM
        var kb=Keyboard.current;
        if(kb!=null)
        {
            if(kb.rKey.wasPressedThisFrame) ResetLevel();
            if(kb.spaceKey.wasPressedThisFrame) Throw();
            if(kb.aKey.isPressed || kb.leftArrowKey.isPressed) desiredMove.x--;
            if(kb.dKey.isPressed || kb.rightArrowKey.isPressed) desiredMove.x++;
            if(kb.sKey.isPressed || kb.downArrowKey.isPressed) desiredMove.y--;
            if(kb.wKey.isPressed || kb.upArrowKey.isPressed) desiredMove.y++;
            if(kb.qKey.isPressed) desiredTurn++;
            if(kb.eKey.isPressed) desiredTurn--;
            if(kb.mKey.wasPressedThisFrame) audioFx.ToggleMute();
        }
        var pad=Gamepad.current;
        if(pad!=null)
        {
            desiredMove+=pad.leftStick.ReadValue()+pad.dpad.ReadValue();
            desiredTurn+=(pad.leftShoulder.isPressed?1:0)-(pad.rightShoulder.isPressed?1:0);
            desiredTurn+=pad.leftTrigger.ReadValue()-pad.rightTrigger.ReadValue();
            if(armed && (pad.buttonEast.wasPressedThisFrame || pad.startButton.wasPressedThisFrame)) ResetLevel();
            else if(armed && pad.buttonSouth.wasPressedThisFrame) Throw();
        }
#else
        if(Input.GetKeyDown(KeyCode.R)) ResetLevel();if(Input.GetKeyDown(KeyCode.Space)) Throw();
        if(Input.GetKey(KeyCode.A)||Input.GetKey(KeyCode.LeftArrow)) desiredMove.x--;
        if(Input.GetKey(KeyCode.D)||Input.GetKey(KeyCode.RightArrow)) desiredMove.x++;
        if(Input.GetKey(KeyCode.W)||Input.GetKey(KeyCode.UpArrow)) desiredMove.y++;
        if(Input.GetKey(KeyCode.S)||Input.GetKey(KeyCode.DownArrow)) desiredMove.y--;
        if(Input.GetKey(KeyCode.Q)) desiredTurn++;if(Input.GetKey(KeyCode.E)) desiredTurn--;
#endif
        foreach(var h in holds) if(h.Held) desiredTurn+=h.Direction.x;
        desiredMove=Vector2.ClampMagnitude(desiredMove,1);desiredTurn=Mathf.Clamp(desiredTurn,-1,1);
        if(desiredMove.sqrMagnitude>0 || desiredTurn!=0) audioFx.BeginAudio();
    }
    void Update()
    {
        UpdateLayout();
        if(!paused)
        {
            ReadInput();
            accumulator+=Mathf.Min(Time.deltaTime,.1f);
            while(accumulator>=Step) {Tick(Step);accumulator-=Step;}
        }
        feedback.Flying(mode==Mode.Flying && !paused);
        Animate(Time.deltaTime);
        status.text=announcement;
        counter.text="反射 "+bounces+"/3   板 "+angle.ToString("+0;-0;0")+"°   "+Mathf.CeilToInt(Mathf.Max(0,60-elapsed))+"秒";
        hints.text=Gamepad.current!=null?"PAD  左スティック / 十字キー：移動  •  LB/RB：板  •  A/×：投げる  •  B/○：再挑戦":"WASD / 矢印：移動  •  Q/E：板  •  SPACE：投げる  •  R：再挑戦";
        muteLabel.text=audioFx.Muted?"音 OFF":"音 ON";
        if(titleMute) titleMute.text=muteLabel.text;
        if(mode==Mode.Title && logoRect) logoRect.anchoredPosition=new Vector2(0,Mathf.Sin(Time.unscaledTime*.9f)*5);
    }
    void SetAngel() {angel.position=new Vector3(angelX,angelY,0);paddle.localRotation=Quaternion.Euler(0,0,angle);}
    void Tick(float dt)
    {
        Vector2 before=paddle.position;float beforeAngle=angle;
        if(mode==Mode.Ready || mode==Mode.Throwing || mode==Mode.Flying)
        {
            angelX=Mathf.Clamp(angelX+desiredMove.x*5*dt,-4.4f,4.4f);
            angelY=Mathf.Clamp(angelY+desiredMove.y*5*dt,-2.5f,1.5f);
            angle=Mathf.Clamp(angle+desiredTurn*75*dt,-65,65);SetAngel();
        }
        if(mode==Mode.Throwing)
        {
            phaseTime+=dt;
            float throwProgress=Mathf.Clamp01(phaseTime/.38f);
            crate.position=Vector2.Lerp((Vector2)man.position+new Vector2(-.38f,1.35f),new Vector2(5.25f,1.45f),Mathf.SmoothStep(0,1,throwProgress))+Vector2.up*(Mathf.Sin(throwProgress*Mathf.PI)*.3f);
            if(phaseTime>=.38f) {mode=Mode.Flying;phaseTime=0;crate.position=new Vector3(5.25f,1.45f,0);velocity=new Vector2(-3.2f,5);announcement="落下地点へ！ 板の角度で反射方向が変わるよ。";}
        }
        else if(mode==Mode.Flying)
        {
            elapsed+=dt;Simulate(dt,before,beforeAngle);
            if(mode==Mode.Flying && elapsed>=60) Finish(false,"時間切れ。もう一度つないでみよう！");
        }
        else if(mode==Mode.Won || mode==Mode.Lost) phaseTime+=dt;
    }
    void Simulate(float dt,Vector2 oldBoard,float oldAngle)
    {
        Vector2 previous=crate.position;
        Vector2 next=previous+velocity*dt+Vector2.down*(.5f*gravity*dt*dt);
        velocity+=Vector2.down*(gravity*dt);
        Vector2 center=paddle.position;
        Vector2 tangent=new Vector2(Mathf.Cos(angle*Mathf.Deg2Rad),Mathf.Sin(angle*Mathf.Deg2Rad));
        Vector2 normal=new Vector2(-tangent.y,tangent.x);
        Vector2 oldNormal=new Vector2(-Mathf.Sin(oldAngle*Mathf.Deg2Rad),Mathf.Cos(oldAngle*Mathf.Deg2Rad));
        float from=Vector2.Dot(previous-oldBoard,oldNormal)-Radius;
        float to=Vector2.Dot(next-center,normal)-Radius;
        float along=Vector2.Dot(next-center,tangent);
        // Swept circle vs board top. Test crossing rather than overlap to avoid tunnelling or duplicate hits.
        if(bounces<3 && from>=-.012f && to<=0 && Mathf.Abs(along)<=HalfBoard+Radius*.5f)
        {
            Vector2 boardLinear=(center-oldBoard)/dt;
            float omega=(angle-oldAngle)*Mathf.Deg2Rad/dt;
            Vector2 contact=tangent*Mathf.Clamp(along,-HalfBoard,HalfBoard);
            Vector2 surfaceVelocity=boardLinear+new Vector2(-contact.y,contact.x)*omega;
            Vector2 relative=velocity-surfaceVelocity;
            float incoming=Vector2.Dot(relative,normal);
            if(incoming<-.02f)
            {
                // Reflection in the moving surface frame: normal restitution and tangential friction.
                relative-=normal*((1+restitution)*incoming);
                relative-=tangent*(Vector2.Dot(relative,tangent)*surfaceFriction);
                velocity=relative+surfaceVelocity;
                float fraction=Mathf.Clamp01(from/Mathf.Max(.00001f,from-to));
                Vector2 at=Vector2.Lerp(previous,next,fraction);
                float hitAlong=Mathf.Clamp(Vector2.Dot(at-center,tangent),-HalfBoard,HalfBoard);
                next=center+tangent*hitAlong+normal*(Radius+.012f);
                next+=velocity*dt*(1-fraction);
                bounces++;squash=Mathf.Clamp(-incoming/10,.35f,1);impactTime=.65f;
                bounceCaption.position=new Vector3(next.x,next.y+.8f,0);
                spin=Mathf.Clamp(spin-along*incoming*22,-280,280);
                feedback.Bounce(next,normal,-incoming);
                audioFx.Bounce();announcement="ぼよーん！ 反射 "+bounces+" / 3";
            }
        }
        crate.position=next;crate.Rotate(0,0,spin*dt);
        // Only the flight crossing the woman's hands can succeed. This never alters velocity.
        if(previous.x>=CatchX && next.x<=CatchX && velocity.x<0)
        {
            float fraction=(CatchX-previous.x)/(next.x-previous.x);
            float crossingY=Mathf.Lerp(previous.y,next.y,fraction);
            if(Mathf.Abs(crossingY-CatchY)<=CatchHalfHeight)
            {Finish(true,"DELIVERED! 手元に届いた！");audioFx.Win();return;}
            announcement=crossingY>CatchY?"高すぎる！ 次は板をもう少し傾けよう。":"届く高さが足りない！ もう一度バウンドさせよう。";
        }
        bool rock=(next.x < -5.05f || next.x > 5.05f) && next.y < -.68f-Radius;
        if(next.y < -5.1f || Mathf.Abs(next.x)>8.9f || rock) Finish(false,"谷に落ちた！ 板の角度を変えて再挑戦しよう。");
    }
    void Finish(bool won,string text)
    {
        audioFx.SetScene(AngelBounceAudio.SceneMusic.Result);mode=won?Mode.Won:Mode.Lost;
        phaseTime=0;velocity=Vector2.zero;ReleaseControls();announcement=text;
        endingTitle.text=won?"HAPPY END":"HOPE LOST";
        endingTitle.color=Hex(won?"#FFE3A1":"#B2C1D9");
        endingLine.text=won?"届いた。想いも、いっしょに。":"救援物資は、谷の底へ。";
        endingShade.color=Hex(won?"#452A1944":"#060E24BD");
        endingPortraits[0].sprite=won?womanHappy:womanSad;
        endingPortraits[1].sprite=won?angelHappy:angelSad;
        endingPortraits[2].sprite=won?manHappy:manSad;
        endingGift.SetActive(won);giftModel.ShowPreview(won);feedback.Flying(false);catchRing.enabled=false;
        for(int i=0;i<3;i++)
        {
            var r=endingPortraits[i].rectTransform;
            r.anchorMax=new Vector2(r.anchorMax.x,(won || i==1) ? .70f : .60f);
        }
        foreach(var part in boardPieces) part.SetActive(false);
        angelRenderer.sprite=won?angelHappy:angelSad;
        angelRenderer.transform.localRotation=Quaternion.Euler(0,0,-angle);
    }
    void Animate(float dt)
    {
        if(paused) return;
        squash=Mathf.MoveTowards(squash,0,dt*5);
        float deformation=squash*Mathf.Cos((.65f-impactTime)*31);
        crateVisual.localScale=new Vector3(1+deformation*.45f,1-deformation*.38f,1);
        impactTime=Mathf.Max(0,impactTime-dt);
        if(bounceWord) {bounceWord.enabled=impactTime>0;bounceWord.color=new Color(1,.93f,.57f,Mathf.Min(1,impactTime*4));}
        if(mode==Mode.Throwing)
        {
            float t=Mathf.Clamp01(phaseTime/.38f);man.rotation=Quaternion.Euler(0,0,Mathf.Sin(t*Mathf.PI)*-10);
            manRenderer.sprite=t>.46f?manThrow:manIdle;
        }
        else
        {
            manRenderer.sprite=mode==Mode.Won?manHappy:mode==Mode.Lost?manSad:mode==Mode.Flying && elapsed<.28f?manThrow:manIdle;
            man.rotation=Quaternion.identity;
        }
        bool approaching=mode==Mode.Flying && crate.position.x < -3.9f;
        womanRenderer.sprite=mode==Mode.Won?womanHappy:mode==Mode.Lost?womanSad:approaching?womanCatch:womanIdle;
        if(mode==Mode.Won)
        {
            woman.localScale=new Vector3(1,1-Mathf.Sin(Mathf.Clamp01(phaseTime/.32f)*Mathf.PI)*.06f,1);
            crate.rotation=Quaternion.identity;crate.position=woman.position+new Vector3(.08f,1.5f,0);
        }
        else woman.localScale=Vector3.one;
        if(mode==Mode.Won || mode==Mode.Lost)
        {
            if(phaseTime>.9f) {resultScreen.SetActive(true);endingGroup.alpha=Mathf.Clamp01((phaseTime-.9f)/.55f);}
            float pulse=mode==Mode.Won?Mathf.Sin(Time.unscaledTime*3)*3:0;
            for(int i=0;i<3;i++) endingPortraits[i].rectTransform.anchoredPosition=new Vector2(0,pulse*(i==1?1:.4f));
        }
        if(mode==Mode.Flying)
        {
            trail.Enqueue(crate.position);if(trail.Count>12) trail.Dequeue();int i=0;
            foreach(var p in trail) {trailDots[i].position=p;trailDots[i].gameObject.SetActive(true);trailDots[i].localScale=Vector3.one*(.025f+i*.004f);i++;}
        }
        else foreach(var d in trailDots) d.gameObject.SetActive(false);
    }
    void ReleaseControls() {desiredMove=Vector2.zero;desiredTurn=0;foreach(var h in holds) h.Release();if(stick) stick.Release();}
    void OnApplicationFocus(bool focus) {paused=!focus;accumulator=0;if(!focus) {ReleaseControls();armed=false;}}
    void OnApplicationPause(bool pause) {paused=pause;accumulator=0;if(pause) {ReleaseControls();armed=false;}}
    void OnDestroy() {foreach(var s in ownedSprites) if(s) Destroy(s);}

    RectTransform Panel(string name,Transform parent,Vector2 min,Vector2 max,string color)
    {
        var g=new GameObject(name,typeof(RectTransform),typeof(Image));g.transform.SetParent(parent,false);
        var r=(RectTransform)g.transform;r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;
        g.GetComponent<Image>().color=Hex(color);return r;
    }
    Text Label(string name,Transform parent,Vector2 min,Vector2 max,string text,int size)
    {
        var g=new GameObject(name,typeof(RectTransform),typeof(Text));g.transform.SetParent(parent,false);
        var r=(RectTransform)g.transform;r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;
        var t=g.GetComponent<Text>();t.font=font;t.text=text;t.fontSize=size;t.color=Hex("#F8E7C6");t.alignment=TextAnchor.MiddleCenter;
        t.resizeTextForBestFit=true;t.resizeTextMinSize=10;t.resizeTextMaxSize=size;t.raycastTarget=false;return t;
    }
    Text Button(string text,Vector2 min,Vector2 max,System.Action action,float turn=0)
    {
        var r=Panel(text,safe,min,max,"#344C65");var b=r.gameObject.AddComponent<Button>();
        var nav=b.navigation;nav.mode=Navigation.Mode.None;b.navigation=nav;
        var colors=b.colors;colors.highlightedColor=Hex("#7096AD");colors.pressedColor=Hex("#D89B5A");b.colors=colors;
        var outline=r.gameObject.AddComponent<Outline>();outline.effectColor=Hex("#CC9E64");outline.effectDistance=new Vector2(1,-1);
        if(turn!=0) {var h=r.gameObject.AddComponent<HoldDirection>();h.Direction=new Vector2(turn,0);holds.Add(h);}
        else b.onClick.AddListener(()=>action());
        return Label("Label",r,Vector2.zero,Vector2.one,text,23);
    }
    void BuildUI()
    {
        font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX || UNITY_IOS
        var japanese=Font.CreateDynamicFontFromOSFont(new[]{"Hiragino Sans","Hiragino Kaku Gothic ProN","Arial"},26);if(japanese)font=japanese;
#endif
        var canvas=new GameObject("Angel Bounce UI",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));canvas.transform.SetParent(transform,false);
        canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
        var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
        if(!FindFirstObjectByType<EventSystem>())
        {
#if ENABLE_INPUT_SYSTEM
            var ui=new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule)).GetComponent<InputSystemUIInputModule>();
            ui.move=null;ui.submit=null;ui.cancel=null;
#else
            new GameObject("EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));
#endif
        }
        safe=Panel("Safe Area",canvas.transform,Vector2.zero,Vector2.one,"#00000000");safe.GetComponent<Image>().raycastTarget=false;
        Panel("Header",safe,new Vector2(0,.85f),Vector2.one,"#16293DF8");
        Label("Title",safe,new Vector2(.02f,.925f),new Vector2(.44f,.997f),"ANGEL BOUNCE / RESCUE",30);
        counter=Label("Counter",safe,new Vector2(.44f,.925f),new Vector2(.78f,.997f),"",23);
        Button("TITLE",new Vector2(.79f,.934f),new Vector2(.875f,.99f),ShowTitle);
        muteLabel=Button("音 ON",new Vector2(.88f,.934f),new Vector2(.99f,.99f),()=>{audioFx.BeginAudio();audioFx.ToggleMute();});
        status=Label("Status",safe,new Vector2(.02f,.852f),new Vector2(.98f,.925f),"",22);
        Panel("Controller dock",safe,Vector2.zero,new Vector2(1,.23f),"#16293DF8");
        var area=Panel("Thumbstick area",safe,new Vector2(.01f,.035f),new Vector2(.235f,.225f),"#00000000");
        var baseRect=Panel("Thumbstick",area,Vector2.zero,Vector2.one,"#3B5777");baseRect.GetComponent<Image>().sprite=Circle();baseRect.GetComponent<Image>().preserveAspect=true;
        var fitter=baseRect.gameObject.AddComponent<AspectRatioFitter>();fitter.aspectMode=AspectRatioFitter.AspectMode.FitInParent;fitter.aspectRatio=1;
        stick=baseRect.gameObject.AddComponent<VirtualStick>();
        var knob=Panel("Stick knob",baseRect,new Vector2(.31f,.31f),new Vector2(.69f,.69f),"#E9C386");knob.GetComponent<Image>().sprite=Circle();knob.GetComponent<Image>().raycastTarget=false;stick.Knob=knob;
        Button("L / Q\n板を左へ",new Vector2(.25f,.06f),new Vector2(.385f,.205f),null,1);
        Button("R / E\n板を右へ",new Vector2(.40f,.06f),new Vector2(.535f,.205f),null,-1);
        Button("投げる\nA / SPACE",new Vector2(.57f,.06f),new Vector2(.795f,.205f),Throw);
        Button("再挑戦\nB / R",new Vector2(.82f,.06f),new Vector2(.99f,.205f),ResetLevel);
        hints=Label("Controls hint",safe,new Vector2(.22f,0),new Vector2(.99f,.045f),"",15);
        BuildEnding();
        BuildTitle();
        bounceWord=Label("Boing",bounceCaption,Vector2.zero,Vector2.one,"ぼよーん！",38);
        bounceWord.GetComponent<RectTransform>().sizeDelta=Vector2.zero;
        var shadow=bounceWord.gameObject.AddComponent<Shadow>();shadow.effectColor=Hex("#25213FFF");shadow.effectDistance=new Vector2(3,-3);
    }
    void BuildEnding()
    {
        var root=Panel("Ending screen",safe,Vector2.zero,Vector2.one,"#14253D");resultScreen=root.gameObject;
        endingGroup=root.gameObject.AddComponent<CanvasGroup>();endingGroup.alpha=0;
        var bg=Panel("Ending canyon",root,Vector2.zero,Vector2.one,"#FFFFFF");bg.GetComponent<Image>().sprite=Slice("Art/Canyon",0,1,Vector2.one*.5f,1,false);
        endingShade=Panel("Ending mood",root,Vector2.zero,Vector2.one,"#060E24BD").GetComponent<Image>();
        endingTitle=Label("Ending title",root,new Vector2(.05f,.81f),new Vector2(.95f,.98f),"",68);endingTitle.fontStyle=FontStyle.Bold;
        endingLine=Label("Ending line",root,new Vector2(.05f,.72f),new Vector2(.95f,.82f),"",26);
        endingPortraits=new Image[3];
        for(int i=0;i<3;i++)
        {
            float x=.02f+i*.325f;
            var portrait=Panel("Ending character "+i,root,new Vector2(x,.255f),new Vector2(x+.31f,.72f),"#FFFFFF");
            endingPortraits[i]=portrait.GetComponent<Image>();endingPortraits[i].preserveAspect=true;endingPortraits[i].raycastTarget=false;
        }
        endingGift=new GameObject("Received 3D gift",typeof(RectTransform),typeof(RawImage));endingGift.transform.SetParent(endingPortraits[0].transform,false);
        var gift=(RectTransform)endingGift.transform;gift.anchorMin=gift.anchorMax=new Vector2(.5f,.65f);gift.anchoredPosition=Vector2.zero;gift.sizeDelta=new Vector2(64,64);
        endingGift.GetComponent<RawImage>().texture=giftModel.Preview;endingGift.GetComponent<RawImage>().raycastTarget=false;
        var retry=Panel("Ending retry",root,new Vector2(.25f,.06f),new Vector2(.55f,.19f),"#D4AC70");
        retry.gameObject.AddComponent<Button>().onClick.AddListener(ResetLevel);var t=Label("Retry label",retry,Vector2.zero,Vector2.one,"もう一度",28);t.color=Hex("#16293D");
        var back=Panel("Ending title button",root,new Vector2(.60f,.06f),new Vector2(.79f,.19f),"#263C57");
        back.gameObject.AddComponent<Button>().onClick.AddListener(ShowTitle);Label("Back label",back,Vector2.zero,Vector2.one,"TITLE",24);
        resultScreen.SetActive(false);
    }
    void BuildTitle()
    {
        var root=Panel("Title screen",safe,Vector2.zero,Vector2.one,"#13273D");titleScreen=root.gameObject;
        var bg=Panel("Title canyon",root,Vector2.zero,Vector2.one,"#FFFFFF");
        bg.GetComponent<Image>().sprite=Slice("Art/Canyon",0,1,Vector2.one*.5f,1,false);
        Panel("Cinematic shade",root,Vector2.zero,Vector2.one,"#101A3855");
        logoRect=Panel("ANGEL BOUNCE title logo",root,new Vector2(.11f,.33f),new Vector2(.89f,.94f),"#FFFFFF");
        var image=logoRect.GetComponent<Image>();image.sprite=Slice("Art/TitleLogo",0,1,Vector2.one*.5f,1);image.preserveAspect=true;image.raycastTarget=false;
        var start=Panel("START",root,new Vector2(.35f,.12f),new Vector2(.65f,.255f),"#D4AC70");
        var button=start.gameObject.AddComponent<Button>();button.onClick.AddListener(BeginGame);
        var nav=button.navigation;nav.mode=Navigation.Mode.None;button.navigation=nav;
        var label=Label("Start label",start,Vector2.zero,Vector2.one,"START",32);label.color=Hex("#192A43");label.fontStyle=FontStyle.Bold;
        var mute=Panel("Title audio",root,new Vector2(.87f,.025f),new Vector2(.98f,.105f),"#182B43DD");
        mute.gameObject.AddComponent<Button>().onClick.AddListener(()=>{audioFx.BeginAudio();audioFx.ToggleMute();});
        titleMute=Label("Title audio label",mute,Vector2.zero,Vector2.one,"音 ON",20);
    }
    void UpdateLayout()
    {
        Rect a=Screen.safeArea;
        safe.anchorMin=new Vector2(a.xMin/Screen.width,a.yMin/Screen.height);safe.anchorMax=new Vector2(a.xMax/Screen.width,a.yMax/Screen.height);
        Rect available=new Rect(a.x/Screen.width,(a.y+a.height*.235f)/Screen.height,a.width/Screen.width,a.height*.61f/Screen.height);
        float aspect=Screen.width*available.width/(Screen.height*available.height);
        cam.rect=available;cam.orthographicSize=8.5f/aspect;
        // In portrait retain a bounded stage viewport rather than stretching the world into the controls.
        if(aspect<1.4f)
        {
            float h=(a.width/1.6f)/Screen.height;
            cam.rect=new Rect(available.x,available.y+(available.height-h)*.5f,available.width,h);cam.orthographicSize=8.5f/1.6f;
        }
    }
}
