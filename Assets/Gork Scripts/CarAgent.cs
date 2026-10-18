using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ambient car: drives lane by lane through the TrafficNetwork, curving through
/// intersections, slowing behind the car in front, wheels spinning. Forward = +Z.
/// </summary>
public class CarAgent : MonoBehaviour
{
    public static readonly List<CarAgent> All = new List<CarAgent>();

    public float cruiseSpeed = 9f;
    public float acceleration = 6f;
    public float followDistance = 7f;
    public float wheelRadius = 0.35f;
    [Tooltip("Stopped this long (s): the car stops yielding for a moment so a jam at a crossing can't lock up for good.")]
    public float stuckTimeout = 2f;
    public float pushThroughTime = 1.5f;

    TrafficNetwork net;
    Vector2Int from, to, next;
    readonly List<Vector2Int> options = new List<Vector2Int>();
    readonly List<Transform> wheels = new List<Transform>();

    // Current segment: straight lane, or a quadratic curve through an intersection.
    bool turning;
    Vector3 p0, p1, p2;
    float segLength, segT;
    float speed;
    float stuckFor, pushThroughUntil;

    public bool Leaving { get; private set; }

    public void Init(TrafficNetwork network, Vector2Int a, Vector2Int b, float startFraction)
    {
        net = network;
        from = a;
        to = b;
        foreach (var t in GetComponentsInChildren<Transform>())
            if (t.name.Contains("Wheel")) wheels.Add(t);

        StartLane();
        segT = Mathf.Clamp01(startFraction);
        speed = cruiseSpeed * 0.6f;
        Place(Evaluate(segT), Tangent(segT));
    }

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    void StartLane()
    {
        turning = false;
        net.Lane(from, to, out p0, out p2);
        p1 = (p0 + p2) * 0.5f;
        segLength = Vector3.Distance(p0, p2);
        segT = 0f;
    }

    /// <summary>Picks the next road at the intersection ahead and curves into it.</summary>
    void StartTurn()
    {
        net.Neighbors(to, options);
        if (options.Count > 1) options.Remove(from);   // no U-turns unless at a dead end
        next = options[Random.Range(0, options.Count)];

        net.Lane(from, to, out _, out Vector3 inEnd);
        net.Lane(to, next, out Vector3 outStart, out _);
        Vector3 dIn = net.Dir(from, to), dOut = net.Dir(to, next);
        Vector3 node = net.Node(to);

        p0 = inEnd;
        p2 = outStart;
        // Control point where the two lane lines meet (center of the corner).
        if (Mathf.Abs(Vector3.Dot(dIn, dOut)) > 0.9f)
            p1 = (p0 + p2) * 0.5f;
        else
            p1 = node + TrafficNetwork.Right(dIn) * net.laneOffset + TrafficNetwork.Right(dOut) * net.laneOffset;
        segLength = Mathf.Max(1f, Vector3.Distance(p0, p1) + Vector3.Distance(p1, p2));
        segT = 0f;
        turning = true;
    }

    Vector3 Evaluate(float t)
    {
        float u = 1f - t;
        return u * u * p0 + 2f * u * t * p1 + t * t * p2;
    }

    Vector3 Tangent(float t)
    {
        Vector3 d = 2f * (1f - t) * (p1 - p0) + 2f * t * (p2 - p1);
        return d.sqrMagnitude > 0.0001f ? d.normalized : transform.forward;
    }

    void Update()
    {
        if (net == null || Leaving) return;
        float dt = Time.deltaTime;

        float target = cruiseSpeed * (turning ? 0.55f : 1f);
        // Deadlock breaker: cars that wait on each other in a crossing would stand still forever.
        stuckFor = speed < 0.3f ? stuckFor + dt : 0f;
        if (stuckFor > stuckTimeout)
        {
            pushThroughUntil = Time.time + pushThroughTime;
            stuckFor = 0f;
        }
        float gap = Time.time < pushThroughUntil ? float.MaxValue : GapAhead();
        if (gap < followDistance) target = Mathf.Min(target, cruiseSpeed * Mathf.Clamp01((gap - 3f) / (followDistance - 3f)));
        speed = Mathf.MoveTowards(speed, target, acceleration * dt);

        segT += speed * dt / segLength;
        while (segT >= 1f)
        {
            float overflow = (segT - 1f) * segLength;
            if (turning)
            {
                from = to;
                to = next;
                StartLane();
            }
            else
            {
                StartTurn();
            }
            segT = overflow / segLength;
        }

        Place(Evaluate(segT), Tangent(segT));

        float spin = speed * dt / wheelRadius * Mathf.Rad2Deg;
        foreach (var w in wheels) w.Rotate(Vector3.right, spin, Space.Self);
    }

    void Place(Vector3 pos, Vector3 dir)
    {
        transform.position = pos;
        if (dir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 0.35f);
    }

    /// <summary>Distance to the nearest car roughly in our lane ahead, driving the same way.</summary>
    float GapAhead()
    {
        float best = float.MaxValue;
        Vector3 fwd = transform.forward, pos = transform.position;
        foreach (var other in All)
        {
            if (other == this || other.Leaving) continue;
            // Only queue behind cars heading our way; crossing traffic would make cars wait on each other in a circle.
            if (Vector3.Dot(other.transform.forward, fwd) < 0.4f) continue;
            Vector3 d = other.transform.position - pos;
            float ahead = Vector3.Dot(d, fwd);
            if (ahead <= 0f || ahead > followDistance + 2f) continue;
            float side = Vector3.Dot(d, TrafficNetwork.Right(fwd));
            if (Mathf.Abs(side) > 1.4f) continue;
            if (ahead < best) best = ahead;
        }
        return best;
    }

    /// <summary>Shrinks away and removes the car.</summary>
    public void Despawn()
    {
        if (Leaving) return;
        Leaving = true;
        StartCoroutine(Tweens.PopOut(transform, 0.35f));
    }
}
