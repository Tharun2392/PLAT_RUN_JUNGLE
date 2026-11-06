using UnityEngine;

public class JungleCoin:MonoBehaviour{
    [SerializeField] Transform visual,glow;[SerializeField] float phase;SpriteRenderer glowRenderer;
    public void Configure(Transform coinVisual,Transform aura,float animationPhase){visual=coinVisual;glow=aura;glowRenderer=aura?aura.GetComponent<SpriteRenderer>():null;phase=animationPhase;}
    void Awake(){glowRenderer=glow?glow.GetComponent<SpriteRenderer>():null;}
    void Update(){if(!visual)return;float t=Time.time*2.2f+phase;float bob=Mathf.Sin(t)*.11f;visual.localPosition=new Vector3(0,bob,0);visual.localScale=new Vector3(.82f*Mathf.Lerp(.3f,1f,Mathf.Abs(Mathf.Cos(t*1.25f))),.82f,1);visual.localRotation=Quaternion.Euler(0,0,Mathf.Sin(t*.55f)*4f);
        if(glow){glow.localPosition=new Vector3(0,bob,.08f);glow.localScale=Vector3.one*(1.05f+.08f*Mathf.Sin(t));}if(glowRenderer){Color c=glowRenderer.color;c.a=.09f+.04f*(.5f+.5f*Mathf.Sin(t*1.7f));glowRenderer.color=c;}}
    void OnTriggerEnter2D(Collider2D c){Pickup(c);}void OnTriggerStay2D(Collider2D c){Pickup(c);}void Pickup(Collider2D c){var player=c.GetComponent<JunglePlayerVisual>()??c.GetComponentInParent<JunglePlayerVisual>();if(player&&JungleQuestGame.I)JungleQuestGame.I.Collect(gameObject);}
}
