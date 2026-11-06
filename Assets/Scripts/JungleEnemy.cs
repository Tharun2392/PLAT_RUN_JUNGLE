using UnityEngine;

public class JungleEnemy:MonoBehaviour{
    void OnTriggerEnter2D(Collider2D other){Bite(other);}
    void OnTriggerStay2D(Collider2D other){Bite(other);}
    void Bite(Collider2D other){var player=other.GetComponent<JunglePlayerVisual>()??other.GetComponentInParent<JunglePlayerVisual>();if(player&&JungleQuestGame.I)JungleQuestGame.I.Die();}
}
