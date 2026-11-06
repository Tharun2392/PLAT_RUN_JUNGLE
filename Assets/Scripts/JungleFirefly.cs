using UnityEngine;

public class JungleFirefly:MonoBehaviour{
    [SerializeField] Transform halo;[SerializeField] float phase,radius;Vector3 home;SpriteRenderer glow;
    public void Configure(Transform glowHalo,float startPhase,float drift){halo=glowHalo;phase=startPhase;radius=drift;glow=GetComponent<SpriteRenderer>();home=transform.position;}
    void Awake(){glow=GetComponent<SpriteRenderer>();}
    void Start(){home=transform.position;}
    void Update(){float t=Time.time+phase;transform.position=home+new Vector3(Mathf.Sin(t*.7f)*radius,Mathf.Sin(t*1.4f)*radius*.75f,0);if(halo)halo.position=transform.position+new Vector3(0,0,.01f);if(glow){Color c=glow.color;c.a=.3f+.65f*(.5f+.5f*Mathf.Sin(t*2.4f));glow.color=c;}}
}
