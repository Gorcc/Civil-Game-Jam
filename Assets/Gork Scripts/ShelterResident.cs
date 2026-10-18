using System.Collections.Generic;
using UnityEngine;

/// <summary>Runs to a reserved shelter slot, waits indefinitely, then walks into replacement housing.</summary>
public class ShelterResident : MonoBehaviour
{
    public WalkAnimator walk;
    List<Vector3> path;
    int waypoint;
    float delay;
    Building housing;
    bool goingHome;
    float idle;

    public void WaitAt(Vector3 point)
    {
        path = null; transform.position = point;
        transform.rotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
        if (walk != null) walk.amount = 0;
    }
    public void Travel(List<Vector3> route, float stagger, Building destinationHome)
    {
        path = route; waypoint = 1; delay = stagger; housing = destinationHome; goingHome = housing != null;
        if (path != null && path.Count > 0) transform.position = path[0];
    }
    void Update()
    {
        if (goingHome && (housing == null || housing.Plot == null || housing.Plot.building != housing))
        { Destroy(gameObject); return; }
        if (delay > 0) { delay -= Time.deltaTime; return; }
        if (path == null)
        {
            idle -= Time.deltaTime;
            if (idle <= 0) { idle = Random.Range(3f, 8f); transform.Rotate(0, Random.Range(-35f, 35f), 0); }
            return;
        }
        if (waypoint >= path.Count)
        {
            if (goingHome) Destroy(gameObject);
            else WaitAt(transform.position);
            return;
        }
        float speed = goingHome ? 3.2f : 5.8f;
        var target = path[waypoint]; Vector3 delta = target - transform.position; delta.y = 0;
        if (walk != null) { walk.speed = speed; walk.amount = 1; }
        if (delta.sqrMagnitude > .001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(delta), 480f * Time.deltaTime);
        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
        if ((transform.position - target).sqrMagnitude < .025f) waypoint++;
    }
}
