using UnityEngine;

public sealed class JungleRollingLog : MonoBehaviour
{
    [SerializeField] float leftEdge;
    [SerializeField] float rightEdge;
    [SerializeField] float rollSpeed = 1.2f;
    Vector3 home;
    float rotation;

    public void Configure(float left, float right, float speed)
    {
        leftEdge = Mathf.Min(left, right);
        rightEdge = Mathf.Max(left, right);
        rollSpeed = Mathf.Max(.1f, speed);
        home = transform.position;
    }

    void Start()
    {
        home = transform.position;
    }

    void Update()
    {
        if (rightEdge <= leftEdge) return;
        float phase = Time.time * rollSpeed;
        float t = Mathf.PingPong(phase, rightEdge - leftEdge);
        transform.position = new Vector3(leftEdge + t, home.y, home.z);
        rotation += rollSpeed * 70f * Time.deltaTime * (Mathf.Cos(phase) >= 0 ? 1f : -1f);
        transform.rotation = Quaternion.Euler(0, 0, rotation);
    }
}
