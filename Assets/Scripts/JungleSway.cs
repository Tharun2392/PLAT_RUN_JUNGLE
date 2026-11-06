using UnityEngine;

public class JungleSway:MonoBehaviour{
    [SerializeField] float speed,angle,phase;Quaternion home;
    public void Configure(float rate,float degrees){speed=rate;angle=degrees;phase=Random.value*Mathf.PI*2;}
    void Start(){home=transform.localRotation;}
    void Update(){transform.localRotation=home*Quaternion.Euler(0,0,Mathf.Sin(Time.time*speed+phase)*angle);}
}
