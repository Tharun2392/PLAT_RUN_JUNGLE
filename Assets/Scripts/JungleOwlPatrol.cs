using UnityEngine;

public class JungleOwlPatrol:MonoBehaviour{
    public float left,right;Vector3 origin;float offset;Rigidbody2D body;JungleOwlVisual visual;
    void Start(){origin=transform.position;offset=Mathf.Clamp(origin.x-left,0,Mathf.Max(.01f,right-left));body=GetComponent<Rigidbody2D>();visual=GetComponent<JungleOwlVisual>();if(body){body.sleepMode=RigidbodySleepMode2D.NeverSleep;body.simulated=true;body.WakeUp();}}
    void FixedUpdate(){float range=Mathf.Max(.01f,right-left);float phase=Time.fixedTime*1.15f+offset;float x=left+Mathf.PingPong(phase,range);float step=Mathf.Repeat(phase,range*2f);int facing=step<range?1:-1;
        float cycle=Mathf.Repeat(Time.fixedTime+1.1f,6.2f);float diveIn=Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.2f,1.7f,cycle));float diveOut=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(2f,2.45f,cycle));float dive=diveIn*diveOut;
        float y=origin.y+Mathf.Sin(Time.fixedTime*1.8f)*.1f-dive*1.15f;if(visual)visual.SetFlight(dive,facing);Vector2 next=new Vector2(x,y);if(body)body.MovePosition(next);else transform.position=new Vector3(next.x,next.y,origin.z);}
}
