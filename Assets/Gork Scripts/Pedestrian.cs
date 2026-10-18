using UnityEngine;

/// <summary>
/// Ambient pedestrian walking laps around a district's sidewalk ring,
/// sometimes turning around or stopping for a moment.
/// </summary>
public class Pedestrian : MonoBehaviour
{
    public float walkSpeed = 1.4f;
    public float turnSpeed = 360f;

    Vector3[] corners;
    int target;
    int direction = 1;
    float pauseTimer;
    float decisionTimer;
    WalkAnimator walk;

    public bool Leaving { get; private set; }

    /// <summary>Walk the rectangle around center with the given half extents.</summary>
    public void Init(Vector3 center, float halfX, float halfZ)
    {
        corners = new[]
        {
            center + new Vector3(-halfX, 0f, -halfZ),
            center + new Vector3(halfX, 0f, -halfZ),
            center + new Vector3(halfX, 0f, halfZ),
            center + new Vector3(-halfX, 0f, halfZ),
        };
        walk = GetComponent<WalkAnimator>();
        walkSpeed = Random.Range(1.1f, 1.7f);
        direction = Random.value < 0.5f ? 1 : -1;

        // Start somewhere along a random side, heading for that side's next corner.
        int side = Random.Range(0, 4);
        int a = side, b = Next(side);
        transform.position = Vector3.Lerp(corners[a], corners[b], Random.value);
        target = b;
        decisionTimer = Random.Range(6f, 14f);
    }

    int Next(int i) => (i + direction + 4) % 4;

    void Update()
    {
        if (corners == null || Leaving) return;
        float dt = Time.deltaTime;

        decisionTimer -= dt;
        if (decisionTimer <= 0f)
        {
            decisionTimer = Random.Range(6f, 14f);
            float roll = Random.value;
            if (roll < 0.3f) pauseTimer = Random.Range(1.5f, 4f);   // stop and look around
            else if (roll < 0.5f) { direction = -direction; target = Next(target); }   // turn back to the previous corner
        }

        bool paused = pauseTimer > 0f;
        if (paused) pauseTimer -= dt;
        if (walk != null)
        {
            walk.amount = Mathf.MoveTowards(walk.amount, paused ? 0f : 1f, dt * 3f);
            walk.speed = walkSpeed;
        }
        if (paused) return;

        Vector3 to = corners[target] - transform.position;
        to.y = 0f;
        if (to.magnitude < 0.15f)
        {
            target = Next(target);
            return;
        }

        Vector3 dir = to.normalized;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), turnSpeed * dt);
        transform.position += dir * walkSpeed * dt * (walk != null ? walk.amount : 1f);
    }

    public void Despawn()
    {
        if (Leaving) return;
        Leaving = true;
        StartCoroutine(Tweens.PopOut(transform, 0.3f));
    }
}
