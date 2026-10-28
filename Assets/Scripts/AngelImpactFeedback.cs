using UnityEngine;

public sealed class AngelImpactFeedback : MonoBehaviour
{
    ParticleSystem sparks;
    TrailRenderer trail;
    Camera cam;
    Light flash;
    Material particleMaterial, trailMaterial;
    Texture2D softDot;
    float shake;
    int lastFrame=-1;
    public void Build(Camera camera,Transform parcel)
    {
        cam=camera;
        softDot=new Texture2D(32,32);var pixels=new Color[1024];
        for(int y=0;y<32;y++)for(int x=0;x<32;x++){float a=Mathf.Clamp01(1-Vector2.Distance(new Vector2(x,y),new Vector2(15.5f,15.5f))/15);pixels[y*32+x]=new Color(1,1,1,a*a);}
        softDot.SetPixels(pixels);softDot.Apply();
        var baseMat=Resources.Load<Material>("SpriteBase");particleMaterial=new Material(baseMat);particleMaterial.mainTexture=softDot;
        var g=new GameObject("Bounce light particles");g.transform.SetParent(transform,false);sparks=g.AddComponent<ParticleSystem>();sparks.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=sparks.main;main.loop=false;main.playOnAwake=false;main.startLifetime=.45f;main.startSpeed=2;main.startSize=.09f;main.simulationSpace=ParticleSystemSimulationSpace.World;main.maxParticles=96;
        var emission=sparks.emission;emission.enabled=false;
        var color=sparks.colorOverLifetime;color.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(new Color(1,.83f,.36f),0),new GradientColorKey(new Color(1,.35f,.12f),1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(0,1)});color.color=gradient;
        var renderer=sparks.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=particleMaterial;renderer.sortingOrder=35;
        trail=parcel.gameObject.AddComponent<TrailRenderer>();trailMaterial=new Material(baseMat);trail.sharedMaterial=trailMaterial;
        trail.time=.28f;trail.minVertexDistance=.035f;trail.startWidth=.055f;trail.endWidth=0;trail.sortingOrder=14;trail.emitting=false;
        trail.startColor=new Color(1,.82f,.36f,.7f);trail.endColor=new Color(1,.38f,.15f,0);
        flash=new GameObject("Bounce point light").AddComponent<Light>();flash.transform.SetParent(transform,false);flash.type=LightType.Point;flash.range=3;flash.intensity=0;flash.color=new Color(1,.64f,.26f);flash.shadows=LightShadows.None;
    }
    public void Flying(bool enabled) {if(trail)trail.emitting=enabled;}
    public void Bounce(Vector2 position,Vector2 normal,float speed)
    {
        if(lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
        shake=Mathf.Clamp(speed*.006f,.025f,.065f);
        flash.transform.position=new Vector3(position.x,position.y,-2);flash.intensity=1.8f;
        for(int i=0;i<18;i++)
        {
            float a=i*2.39996f;Vector2 spread=new Vector2(Mathf.Cos(a),Mathf.Sin(a));
            var p=new ParticleSystem.EmitParams {position=new Vector3(position.x,position.y,-1.2f),velocity=(Vector3)(normal*1.3f+spread*(.6f+i*.06f)),startSize=.045f+(i%3)*.025f,startLifetime=.25f+(i%4)*.08f};sparks.Emit(p,1);
        }
    }
    public void ResetEffects()
    {
        shake=0;if(cam)cam.transform.position=new Vector3(0,0,-10);if(trail){trail.Clear();trail.emitting=false;}if(sparks)sparks.Clear();if(flash)flash.intensity=0;
    }
    void LateUpdate()
    {
        shake=Mathf.MoveTowards(shake,0,Time.unscaledDeltaTime*.7f);
        if(cam)cam.transform.position=new Vector3(Mathf.Sin(Time.unscaledTime*87)*shake,Mathf.Cos(Time.unscaledTime*73)*shake*.6f,-10);
        if(flash)flash.intensity=Mathf.MoveTowards(flash.intensity,0,Time.unscaledDeltaTime*14);
    }
    void OnApplicationFocus(bool focus) {if(!focus)ResetEffects();}
    void OnDestroy() {if(particleMaterial)Destroy(particleMaterial);if(trailMaterial)Destroy(trailMaterial);if(softDot)Destroy(softDot);}
}
