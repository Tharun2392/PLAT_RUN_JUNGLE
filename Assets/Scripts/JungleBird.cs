using UnityEngine;

public class JungleBird:MonoBehaviour{
    [SerializeField] float phase,speed;Vector3 home;Vector3 baseScale;
    public void Configure(float startPhase,float flightSpeed){phase=startPhase;speed=flightSpeed;home=transform.position;baseScale=transform.localScale;}
    void Start(){home=transform.position;baseScale=transform.localScale;}
    void Update(){float t=Time.time+phase;float x=Mathf.Repeat(home.x+Time.time*speed+12f,145f)-12f;transform.position=new Vector3(x,home.y+Mathf.Sin(t*.65f)*.18f,home.z);float flap=.55f+Mathf.Abs(Mathf.Sin(t*5.5f))*.9f;transform.localScale=new Vector3(baseScale.x,baseScale.y*flap,baseScale.z);}
}
