using UnityEngine;

public class JungleHazard:MonoBehaviour{void OnTriggerEnter2D(Collider2D c){Damage(c);}void OnTriggerStay2D(Collider2D c){Damage(c);}void Damage(Collider2D c){var player=c.GetComponent<JunglePlayerVisual>()??c.GetComponentInParent<JunglePlayerVisual>();if(player&&JungleQuestGame.I)JungleQuestGame.I.Die();}}
