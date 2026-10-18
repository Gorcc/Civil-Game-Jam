using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Road graph derived from the CityMap grid: one node per road intersection,
/// edges between neighbouring intersections. Traffic drives on the right.
/// </summary>
public class TrafficNetwork
{
    public readonly int nx, nz;
    readonly Vector3[,] nodes;

    /// <summary>Half the road width: lanes start/stop this far from an intersection center.</summary>
    public readonly float halfRoad;
    /// <summary>Distance of a lane center from the road center line.</summary>
    public readonly float laneOffset;

    public TrafficNetwork(CityMap map)
    {
        nx = map.districtsX + 1;
        nz = map.districtsZ + 1;
        nodes = new Vector3[nx, nz];
        float t = CityMap.TileSize;
        for (int i = 0; i < nx; i++)
            for (int j = 0; j < nz; j++)
                nodes[i, j] = map.transform.TransformPoint(new Vector3(
                    (i * map.PeriodX + map.roadTiles * 0.5f) * t, 0f,
                    (j * map.PeriodZ + map.roadTiles * 0.5f) * t));

        halfRoad = map.roadTiles * t * 0.5f;
        laneOffset = halfRoad * 0.5f;
    }

    public Vector3 Node(Vector2Int n) => nodes[n.x, n.y];

    public bool Valid(Vector2Int n) => n.x >= 0 && n.y >= 0 && n.x < nx && n.y < nz;

    static readonly Vector2Int[] Dirs = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };

    public void Neighbors(Vector2Int n, List<Vector2Int> result)
    {
        result.Clear();
        foreach (var d in Dirs)
            if (Valid(n + d)) result.Add(n + d);
    }

    public Vector2Int RandomNode() => new Vector2Int(Random.Range(0, nx), Random.Range(0, nz));

    /// <summary>Unit direction of travel from a to b.</summary>
    public Vector3 Dir(Vector2Int a, Vector2Int b) => (Node(b) - Node(a)).normalized;

    /// <summary>Right-hand vector for a travel direction (lanes are offset this way).</summary>
    public static Vector3 Right(Vector3 dir) => new Vector3(dir.z, 0f, -dir.x);

    /// <summary>Lane start (just after leaving a) and end (just before reaching b).</summary>
    public void Lane(Vector2Int a, Vector2Int b, out Vector3 start, out Vector3 end)
    {
        Vector3 d = Dir(a, b), r = Right(d);
        start = Node(a) + d * halfRoad + r * laneOffset;
        end = Node(b) - d * halfRoad + r * laneOffset;
    }
}
