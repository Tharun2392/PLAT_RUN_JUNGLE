using UnityEngine;

public class JungleBeetleVisual:MonoBehaviour{
    [SerializeField] Transform body;[SerializeField] Transform[] legs;[SerializeField] Transform antennaA,antennaB;[SerializeField] float phase;Vector3 bodyHome;Quaternion[] legHome;Quaternion antennaAHome,antennaBHome;int facing=1;
    void Awake(){CacheRestPose();}
    void CacheRestPose(){if(body)bodyHome=body.localPosition;legHome=new Quaternion[legs==null?0:legs.Length];for(int i=0;i<legHome.Length;i++)if(legs[i])legHome[i]=legs[i].localRotation;if(antennaA)antennaAHome=antennaA.localRotation;if(antennaB)antennaBHome=antennaB.localRotation;}
    public void Configure(Transform art,Transform[] walkingLegs,Transform nearAntenna,Transform farAntenna){body=art;legs=walkingLegs;antennaA=nearAntenna;antennaB=farAntenna;phase=Random.value*Mathf.PI*2;CacheRestPose();}
    public void SetFacing(int direction){facing=direction<0?-1:1;}
    void Update(){if(!body)return;float gait=Time.time*12f+phase;body.localScale=new Vector3(facing,1,1);body.localPosition=bodyHome+new Vector3(0,Mathf.Abs(Mathf.Sin(gait))*.025f,0);body.localRotation=Quaternion.Euler(0,0,Mathf.Sin(gait*.5f)*1.2f);
        if(legs!=null)for(int i=0;i<legs.Length;i++){float stride=Mathf.Sin(gait+(i%2==0?0:Mathf.PI))*17f;legs[i].localRotation=legHome[i]*Quaternion.Euler(0,0,stride);}
        if(antennaA)antennaA.localRotation=antennaAHome*Quaternion.Euler(0,0,Mathf.Sin(gait*.35f)*4f);if(antennaB)antennaB.localRotation=antennaBHome*Quaternion.Euler(0,0,-Mathf.Sin(gait*.35f+.5f)*4f);}
}
