using UnityEngine;

public sealed class JungleParallax : MonoBehaviour
{
    [SerializeField] Camera targetCamera;
    [SerializeField, Range(0f, .9f)] float parallaxFactor = .34f;
    Vector3 home;

    public void Configure(Camera camera, float factor)
    {
        targetCamera = camera;
        parallaxFactor = Mathf.Clamp(factor, 0f, .9f);
        home = transform.position;
    }

    void Start()
    {
        if (!targetCamera) targetCamera = Camera.main;
        home = transform.position;
    }

    void LateUpdate()
    {
        if (!targetCamera) return;
        Vector3 position = transform.position;
        position.x = home.x + targetCamera.transform.position.x * parallaxFactor;
        transform.position = position;
    }
}
