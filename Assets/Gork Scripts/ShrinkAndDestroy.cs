using UnityEngine;

/// <summary>Waits, shrinks to zero, then destroys the GameObject. Used for rubble.</summary>
public class ShrinkAndDestroy : MonoBehaviour
{
    public float delay = 3f;
    public float duration = 0.6f;

    float timer;
    Vector3 startScale;

    void Start() => startScale = transform.localScale;

    void Update()
    {
        timer += Time.deltaTime;
        if (timer < delay) return;
        float k = Mathf.Clamp01((timer - delay) / duration);
        transform.localScale = startScale * (1f - Ease.InCubic(k));
        if (k >= 1f) Destroy(gameObject);
    }
}
