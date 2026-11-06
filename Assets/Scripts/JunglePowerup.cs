using UnityEngine;

public enum JunglePowerupKind
{
    Speed,
    Magnet
}

public sealed class JunglePowerup : MonoBehaviour
{
    [SerializeField] JunglePowerupKind kind;
    Vector3 home;

    public void Configure(JunglePowerupKind value)
    {
        kind = value;
    }

    void Start()
    {
        home = transform.position;
    }

    void Update()
    {
        float phase = Time.time * 2.4f + (int)kind * 1.8f;
        transform.position = home + Vector3.up * (Mathf.Sin(phase) * .085f);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        var player = other.GetComponent<JunglePlayerVisual>() ?? other.GetComponentInParent<JunglePlayerVisual>();
        if (!player || !JungleQuestGame.I) return;
        JungleQuestGame.I.ActivatePowerup(kind);
        gameObject.SetActive(false);
    }
}
