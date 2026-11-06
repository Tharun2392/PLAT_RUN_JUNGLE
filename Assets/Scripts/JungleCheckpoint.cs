using UnityEngine;

public class JungleCheckpoint:MonoBehaviour{void OnTriggerEnter2D(Collider2D c){if(c.GetComponent<JunglePlayerVisual>()&&JungleQuestGame.I)JungleQuestGame.I.Checkpoint(transform.position.x-.7f);}}
