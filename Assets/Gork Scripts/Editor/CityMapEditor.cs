using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CityMap))]
public class CityMapEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var map = (CityMap)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"Harita: {map.TilesX}x{map.TilesZ} tile ({map.MapSize.x}m x {map.MapSize.z}m)");

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Generate Map", GUILayout.Height(28)))
            {
                map.Generate();
                EditorUtility.SetDirty(map);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(map.gameObject.scene);
            }
            if (GUILayout.Button("Clear", GUILayout.Height(28), GUILayout.Width(80)))
            {
                map.Clear();
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(map.gameObject.scene);
            }
        }
    }
}
