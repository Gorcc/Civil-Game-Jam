using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Vehicle or machine on a construction site. Drives along waypoints and,
/// while working, animates its optional parts (slewing body, boom, mixer drum).
/// Forward is +Z.
/// </summary>
public class ConstructionMachine : MonoBehaviour
{
    [Header("Driving")]
    public float speed = 14f;
    public float turnSpeed = 220f;

    [Header("Working parts (optional)")]
    public Transform slewPivot;
    public float slewAngle = 35f;
    public float slewSpeed = 0.6f;

    public Transform armPivot;
    public Vector3 armAxis = Vector3.forward;
    public float armAngleMin = -14f;
    public float armAngleMax = 12f;
    public float armSpeed = 1.6f;

    public Transform spinner;
    public Vector3 spinAxis = Vector3.right;
    public float spinSpeed = 120f;

    [Header("State")]
    public bool working;
    public bool driving;

    Quaternion slewBase = Quaternion.identity;
    Quaternion armBase = Quaternion.identity;
    float workTime;
    float phase;

    void Awake()
    {
        if (slewPivot != null) slewBase = slewPivot.localRotation;
        if (armPivot != null) armBase = armPivot.localRotation;
        phase = Random.Range(0f, 10f);
    }

    void Update()
    {
        float dt = Time.deltaTime;

        if (spinner != null && (working || driving))
            spinner.Rotate(spinAxis, spinSpeed * dt, Space.Self);

        if (!working)
        {
            // Ease parts back to rest.
            if (slewPivot != null) slewPivot.localRotation = Quaternion.Slerp(slewPivot.localRotation, slewBase, dt * 3f);
            if (armPivot != null) armPivot.localRotation = Quaternion.Slerp(armPivot.localRotation, armBase, dt * 3f);
            return;
        }

        workTime += dt;
        float t = workTime + phase;
        if (slewPivot != null)
            slewPivot.localRotation = slewBase * Quaternion.Euler(0f, Mathf.Sin(t * slewSpeed) * slewAngle, 0f);
        if (armPivot != null)
        {
            float k = (Mathf.Sin(t * armSpeed) + 1f) * 0.5f;
            armPivot.localRotation = armBase * Quaternion.AngleAxis(Mathf.Lerp(armAngleMin, armAngleMax, k), armAxis);
        }
    }

    public void PlaceAtStart(IList<Vector3> path)
    {
        if (path == null || path.Count == 0) return;
        transform.position = path[0];
        if (path.Count > 1)
        {
            Vector3 dir = path[1] - path[0];
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(dir);
        }
    }

    public IEnumerator Drive(IList<Vector3> path)
    {
        driving = true;
        for (int i = 1; i < path.Count; i++)
        {
            Vector3 target = path[i];
            while (true)
            {
                Vector3 to = target - transform.position;
                to.y = 0f;
                float d = to.magnitude;
                if (d < 0.05f) break;

                Vector3 dir = to / d;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), turnSpeed * Time.deltaTime);
                // Slow down while turning and when arriving.
                float facing = Mathf.Clamp01(Vector3.Dot(transform.forward, dir));
                float arrive = i == path.Count - 1 ? Mathf.Clamp01(d / 4f) + 0.15f : 1f;
                float step = speed * Mathf.Max(0.15f, facing) * Mathf.Min(1f, arrive) * Time.deltaTime;
                transform.position = Vector3.MoveTowards(transform.position, target, step);
                yield return null;
            }
        }
        driving = false;
    }

    /// <summary>Drives away along the path, then removes itself.</summary>
    public void Leave(IList<Vector3> path)
    {
        working = false;
        StartCoroutine(LeaveRoutine(path));
    }

    IEnumerator LeaveRoutine(IList<Vector3> path)
    {
        yield return new WaitForSeconds(0.4f);
        if (path != null && path.Count > 0)
        {
            var full = new List<Vector3> { transform.position };
            full.AddRange(path);
            yield return Drive(full);
        }
        yield return Tweens.PopOut(transform, 0.3f);
    }
}
