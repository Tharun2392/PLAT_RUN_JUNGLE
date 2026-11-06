using UnityEngine;

public class JunglePatrol:MonoBehaviour{
    public float left,right;Vector3 origin;float offset;Rigidbody2D body;JungleBeetleVisual visual;
    void Start(){origin=transform.position;offset=Mathf.Clamp(origin.x-left,0,Mathf.Max(.01f,right-left));body=GetComponent<Rigidbody2D>();visual=GetComponent<JungleBeetleVisual>();if(body){body.sleepMode=RigidbodySleepMode2D.NeverSleep;body.simulated=true;body.WakeUp();}}
    void FixedUpdate(){float range=Mathf.Max(.01f,right-left);float phase=Time.fixedTime*2.1f+offset;float x=left+Mathf.PingPong(phase,range);float step=Mathf.Repeat(phase,range*2f);if(visual)visual.SetFacing(step<range?1:-1);Vector2 next=new Vector2(x,origin.y+Mathf.Sin(Time.fixedTime*5)*.08f);if(body)body.MovePosition(next);else transform.position=new Vector3(next.x,next.y,origin.z);}
}
