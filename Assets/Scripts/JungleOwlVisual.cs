using UnityEngine;

public class JungleOwlVisual:MonoBehaviour{
    [SerializeField] Transform body,farWing,nearWing;Quaternion farHome,nearHome;float dive;int facing=1;
    void Awake(){CacheRestPose();}
    void CacheRestPose(){if(farWing)farHome=farWing.localRotation;if(nearWing)nearHome=nearWing.localRotation;}
    public void Configure(Transform owlBody,Transform backWing,Transform frontWing){body=owlBody;farWing=backWing;nearWing=frontWing;CacheRestPose();}
    public void SetFlight(float swoop,int direction){dive=swoop;facing=direction<0?-1:1;}
    void Update(){if(!body)return;float flap=Mathf.Sin(Time.time*9.5f)*34f;body.localScale=new Vector3(facing,1,1);body.localRotation=Quaternion.Euler(0,0,-dive*7f*facing);
        if(farWing)farWing.localRotation=farHome*Quaternion.Euler(0,0,-flap-dive*12f);if(nearWing)nearWing.localRotation=nearHome*Quaternion.Euler(0,0,flap+dive*12f);}
}
