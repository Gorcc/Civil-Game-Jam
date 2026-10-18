using UnityEngine;

/// <summary>
/// Walk cycle for static (unrigged) low-poly characters: the body hops a little with
/// each step, rocks side to side and leans into the walk. Animates the model child,
/// so the root stays free for movement.
/// </summary>
public class BobWalk : WalkAnimator
{
    public Transform model;
    public float stepLength = 0.7f;   // meters per step
    public float bobHeight = 0.07f;
    public float rockAngle = 5f;
    public float leanAngle = 5f;
    public float squash = 0.04f;

    float phase;
    Vector3 baseScale = Vector3.one;

    void Start()
    {
        if (model == null && transform.childCount > 0) model = transform.GetChild(0);
        if (model != null) baseScale = model.localScale;
        phase = Random.Range(0f, Mathf.PI * 2f);
    }

    void LateUpdate()
    {
        if (model == null) return;

        // One full sine period = two steps (left + right).
        phase += Time.deltaTime * (speed / (stepLength * 2f)) * Mathf.PI * 2f * Mathf.Max(amount, 0.0001f);
        float step = Mathf.Abs(Mathf.Sin(phase));       // 0 at foot contact, 1 mid-stride
        float side = Mathf.Sin(phase);                   // -1 left .. 1 right

        model.localPosition = new Vector3(0f, step * bobHeight * amount, 0f);
        model.localRotation = Quaternion.Euler(leanAngle * amount, 0f, side * rockAngle * amount);
        float sq = (1f - step) * squash * amount;        // squash a bit on contact
        model.localScale = new Vector3(baseScale.x * (1f + sq), baseScale.y * (1f - sq), baseScale.z * (1f + sq));
    }
}
