using UnityEngine;

public sealed class JungleFallingBranch : MonoBehaviour
{
    [SerializeField] float restingY = -2.82f;
    [SerializeField] float warningDistance = 1.45f;
    [SerializeField] float fallDuration = .55f;
    Rigidbody2D body;
    Collider2D damageCollider;
    JungleHazard hazard;
    Vector2 startPosition;
    bool triggered;
    float progress, startRotation;

    public void Configure(float landingHeight)
    {
        restingY = landingHeight;
    }

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        damageCollider = GetComponent<Collider2D>();
        hazard = GetComponent<JungleHazard>();
    }

    void FixedUpdate()
    {
        var game = JungleQuestGame.I;
        var player = game ? game.PlayerTransform : null;
        if (!triggered)
        {
            float sway = Mathf.Sin(Time.time * 2.1f) * 1.6f;
            if (body) body.MoveRotation(sway);
            else transform.rotation = Quaternion.Euler(0, 0, sway);
            if (!game || !game.IsPlaying || !player || Mathf.Abs(player.position.x - transform.position.x) > warningDistance || player.position.y > transform.position.y - .9f) return;
            triggered = true;
            startPosition = body ? body.position : (Vector2)transform.position;
            startRotation = body ? body.rotation : transform.eulerAngles.z;
        }

        if (progress >= 1f) return;
        progress = Mathf.Min(1f, progress + Time.fixedDeltaTime / Mathf.Max(.1f, fallDuration));
        float eased = Mathf.SmoothStep(0f, 1f, progress);
        Vector2 landingPosition = new Vector2(startPosition.x, restingY);
        float angle = Mathf.LerpAngle(startRotation, -12f, eased);
        if (body)
        {
            body.MovePosition(Vector2.Lerp(startPosition, landingPosition, eased));
            body.MoveRotation(angle);
        }
        else
        {
            transform.position = Vector2.Lerp(startPosition, landingPosition, eased);
            transform.rotation = Quaternion.Euler(0, 0, angle);
        }
        if (progress >= 1f)
        {
            if (body) body.position = landingPosition;
            if (damageCollider) damageCollider.enabled = true;
            if (hazard) hazard.enabled = true;
        }
    }
}
