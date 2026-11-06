using UnityEngine;

public class JunglePlayerVisual : MonoBehaviour
{
    Transform leg, arm, art;
    Rigidbody2D rb;
    SpriteRenderer spriteRenderer;
    Sprite[] runFrames;
    Sprite idleFrame, jumpFrame, fallFrame, landingFrame, deathFrame;
    Vector3 legHome, armHome, artHomePos, artHomeScale;
    float phase, runFrameClock, landingTimer, deathClock;
    bool dead, wasFalling;

    public void SetAnimatedParts(Transform animatedLeg, Transform animatedArm) { leg = animatedLeg; arm = animatedArm; }

    public void SetSpriteAnimation(SpriteRenderer renderer, Sprite[] run, Sprite idle, Sprite jump, Sprite fall, Sprite death)
    {
        SetSpriteAnimation(renderer, run, idle, jump, fall, idle, death);
    }

    public void SetSpriteAnimation(SpriteRenderer renderer, Sprite[] run, Sprite idle, Sprite jump, Sprite fall, Sprite landing, Sprite death)
    {
        spriteRenderer = renderer;
        runFrames = run;
        idleFrame = idle;
        jumpFrame = jump;
        fallFrame = fall;
        landingFrame = landing;
        deathFrame = death;
        art = renderer ? renderer.transform : null;
        if (art) { artHomePos = art.localPosition; artHomeScale = art.localScale; }
    }

    public void SetCharacterFrames(Sprite[] frames)
    {
        if (frames == null || frames.Length < 9 || !spriteRenderer) return;
        runFrames = new[] { frames[0], frames[1], frames[2], frames[3] };
        idleFrame = frames[4];
        jumpFrame = frames[5];
        fallFrame = frames[6];
        landingFrame = frames[7];
        deathFrame = frames[8];
        spriteRenderer.sprite = idleFrame;
        wasFalling = false;
        landingTimer = 0;
    }

    public void SetDead(bool value)
    {
        if (value && !dead) deathClock = 0;
        dead = value;
        if (!value) { deathClock = 0; wasFalling = false; landingTimer = 0; }
    }

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        if (leg) legHome = leg.localPosition;
        if (arm) armHome = arm.localPosition;
        if (art) { artHomePos = art.localPosition; artHomeScale = art.localScale; }
    }

    void Update()
    {
        if (!rb || !spriteRenderer || runFrames == null || runFrames.Length == 0) return;
        float speed = Mathf.Abs(rb.linearVelocity.x);
        float vertical = rb.linearVelocity.y;
        bool running = speed > .55f && Mathf.Abs(vertical) < 1.15f;
        float speedBlend = Mathf.Clamp01(speed / 11.5f);
        phase += Time.deltaTime * (running ? Mathf.Lerp(9, 14, speedBlend) : 2.7f);
        if (running) runFrameClock += Time.deltaTime * Mathf.Lerp(7.5f, 11.5f, speedBlend);

        if (vertical < -.9f) wasFalling = true;
        else if (wasFalling && Mathf.Abs(vertical) < .25f) { landingTimer = .16f; wasFalling = false; }
        landingTimer = Mathf.Max(0, landingTimer - Time.deltaTime);

        Sprite run = runFrames[Mathf.FloorToInt(runFrameClock) % runFrames.Length];
        Sprite next = dead ? deathFrame : (landingTimer > 0 && landingFrame ? landingFrame :
            (vertical > .9f ? jumpFrame : (vertical < -.9f ? fallFrame : (running ? run : idleFrame))));
        if (next && spriteRenderer.sprite != next) spriteRenderer.sprite = next;

        float bounce = dead ? 0 : (running ? Mathf.Abs(Mathf.Sin(phase * 1.35f)) * .045f : Mathf.Sin(phase * 2.4f) * .018f);
        float lean = dead ? Mathf.Sin(deathClock * 5f) * 1.5f :
            (running ? Mathf.Sin(phase * 1.35f) * 1.3f : (vertical > .9f ? -2.5f : (vertical < -.9f ? 2.2f : Mathf.Sin(phase * 1.2f) * .45f)));
        if (art)
        {
            art.localPosition = artHomePos + new Vector3(0, dead ? -.2f : bounce, 0);
            art.localRotation = Quaternion.Euler(0, 0, lean);
            art.localScale = new Vector3(artHomeScale.x,
                artHomeScale.y * (vertical > 1.2f ? 1.025f : (vertical < -1.2f ? .985f : 1f)), artHomeScale.z);
        }
        if (dead) deathClock += Time.deltaTime;
        if (leg)
        {
            leg.localPosition = legHome + (running ? new Vector3(0, Mathf.Abs(Mathf.Sin(phase)) * .16f, 0) : Vector3.zero);
            leg.localRotation = Quaternion.Euler(0, 0, running ? Mathf.Sin(phase) * 25 : 0);
        }
        if (arm)
        {
            arm.localPosition = armHome + (running ? new Vector3(0, -Mathf.Abs(Mathf.Sin(phase)) * .1f, 0) : Vector3.zero);
            arm.localRotation = Quaternion.Euler(0, 0, running ? -Mathf.Sin(phase) * 28 : Mathf.Sin(phase) * 3);
        }
    }
}
