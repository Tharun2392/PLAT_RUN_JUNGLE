using UnityEngine;

public class JungleFinish:MonoBehaviour{void OnTriggerEnter2D(Collider2D c){if(c.GetComponent<JunglePlayerVisual>())JungleQuestGame.I.Finish();}}
