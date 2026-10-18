using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Bright faceted flame silhouettes on the actual roof and facades, without adding lights.</summary>
public class BuildingFireVisual : MonoBehaviour
{
    static Mesh flameMesh;
    static Material orange, yellow, red;
    readonly List<Transform> tongues = new List<Transform>();
    readonly List<Vector3> sizes = new List<Vector3>();

    public void Build(Bounds bounds, float size)
    {
        EnsureResources();
        Vector3 c = bounds.center; Vector3 e = bounds.extents;
        Vector3[] anchors = {
            new Vector3(c.x-e.x-.18f, c.y, c.z), new Vector3(c.x+e.x+.18f, c.y, c.z),
            new Vector3(c.x, c.y, c.z-e.z-.18f), new Vector3(c.x, c.y, c.z+e.z+.18f),
            new Vector3(c.x-e.x*.45f,bounds.max.y+.1f,c.z), new Vector3(c.x+e.x*.45f,bounds.max.y+.1f,c.z)
        };
        for (int i = 0; i < anchors.Length; i++)
            for (int j = 0; j < 3; j++)
            {
                var go = new GameObject("VisibleFlame", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(transform, false); go.transform.position = anchors[i] + new Vector3((j-1)*size*.46f,0,(j%2)*size*.2f);
                go.transform.rotation = Quaternion.Euler(0, i*61+j*47, 0);
                go.GetComponent<MeshFilter>().sharedMesh = flameMesh;
                var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = j==1 ? yellow : j==0 ? orange : red;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                Vector3 scale = new Vector3(size*.70f, size*(j==1 ? 1.55f : 2.15f), size*.70f);
                go.transform.localScale = scale; tongues.Add(go.transform); sizes.Add(scale); go.layer=2;
            }
    }
    void Update()
    {
        for (int i = 0; i < tongues.Count; i++)
        {
            float pulse = 1f + Mathf.Sin(Time.time*10+i*1.7f)*.18f;
            Vector3 scale = sizes[i]; scale.y *= pulse; scale.x *= 2f-pulse;
            tongues[i].localScale = scale;
            tongues[i].localRotation = Quaternion.Euler(Mathf.Sin(Time.time*7+i)*6, i*47, Mathf.Cos(Time.time*8+i)*8);
        }
    }
    static void EnsureResources()
    {
        if (flameMesh == null)
        {
            flameMesh = new Mesh { name="LowPolyFlame" };
            flameMesh.vertices = new[] { new Vector3(-.48f,0,-.40f),new Vector3(.48f,0,-.40f),new Vector3(.48f,0,.40f),new Vector3(-.48f,0,.40f),new Vector3(-.25f,.55f,-.23f),new Vector3(.30f,.55f,-.23f),new Vector3(.30f,.55f,.23f),new Vector3(-.25f,.55f,.23f),new Vector3(.14f,1.4f,0) };
            flameMesh.triangles = new[] {0,3,2,0,2,1,0,1,5,0,5,4,1,2,6,1,6,5,2,3,7,2,7,6,3,0,4,3,4,7,4,5,8,5,6,8,6,7,8,7,4,8};
            var triangles = flameMesh.triangles;
            for (int i=0; i<triangles.Length; i+=3) { int a=triangles[i]; triangles[i]=triangles[i+2]; triangles[i+2]=a; }
            flameMesh.triangles = triangles;
            flameMesh.RecalculateNormals(); flameMesh.RecalculateBounds();
        }
        if (orange == null) orange = MakeMaterial(new Color(1,.26f,.015f));
        if (yellow == null) yellow = MakeMaterial(new Color(1,.78f,.045f));
        if (red == null) red = MakeMaterial(new Color(.94f,.095f,.01f));
    }
    static Material MakeMaterial(Color color)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Unlit")); m.SetColor("_BaseColor",color); return m;
    }
}
