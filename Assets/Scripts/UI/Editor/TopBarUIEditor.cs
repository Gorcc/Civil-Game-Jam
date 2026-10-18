using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CivilGameJam.UI
{
    [CustomEditor(typeof(TopBarUI))]
    public class TopBarUIEditor : Editor
    {
        [MenuItem("Tools/CivilGameJam/Üst Menüyü Kur (Setup Top Bar UI)")]
        public static void SetupTopBarFromMenu()
        {
            // Canvas'ı bul veya oluştur
            GameObject canvasObj = GameObject.Find("SubtitleCanvas");
            if (canvasObj == null)
            {
                canvasObj = GameObject.Find("Canvas");
            }

            if (canvasObj == null)
            {
                EditorUtility.DisplayDialog("Uyarı", "Sahnede Canvas bulunamadı!", "Tamam");
                return;
            }

            // CanvasScaler'ı 1920x1080 ölçeklemeye ayarla
            CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
            }

            // TopBarPanel'ı bul veya oluştur
            Transform topBarTrans = canvasObj.transform.Find("TopBarPanel");
            GameObject topBarObj;
            if (topBarTrans == null)
            {
                topBarObj = new GameObject("TopBarPanel", typeof(RectTransform));
                topBarObj.transform.SetParent(canvasObj.transform, false);
                // SubtitlePanel'ın üzerinde veya altında düzgün sıralansın
                topBarObj.transform.SetAsFirstSibling();
            }
            else
            {
                topBarObj = topBarTrans.gameObject;
            }

            TopBarUI topBar = topBarObj.GetComponent<TopBarUI>();
            if (topBar == null)
            {
                topBar = Undo.AddComponent<TopBarUI>(topBarObj);
            }

            Undo.RegisterFullObjectHierarchyUndo(topBarObj, "Setup Top Bar UI");
            topBar.BuildTopBarUI();
            EditorUtility.SetDirty(topBarObj);
            EditorUtility.SetDirty(canvasObj);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(topBarObj.scene);

            Selection.activeGameObject = topBarObj;
            EditorUtility.DisplayDialog("Başarılı", "Yuvarlak köşeli Üst Menü (TopBarPanel) başarıyla kuruldu!", "Harika");
        }

        public override void OnInspectorGUI()
        {
            TopBarUI topBar = (TopBarUI)target;

            EditorGUILayout.HelpBox(
                "Şehir Durumu ve Kaynak Göstergeleri (Top Bar):\n" +
                "• Başka kodlardan çağırmak için:\n" +
                "  TopBarUI.Instance.SetBudget(412000, 19000);\n" +
                "  TopBarUI.Instance.SetApproval(54f, 35f);\n" +
                "  TopBarUI.Instance.SetPopulation(186);\n" +
                "  TopBarUI.Instance.SetDay(7, 30);",
                MessageType.Info
            );

            EditorGUILayout.Space(5);

            GUI.backgroundColor = new Color(0.18f, 0.52f, 0.92f);
            if (GUILayout.Button("🛠️ Üst Menü Arayüzünü Oluştur / Yenile", GUILayout.Height(36)))
            {
                Undo.RegisterFullObjectHierarchyUndo(topBar.gameObject, "Rebuild Top Bar UI");
                topBar.BuildTopBarUI();
                EditorUtility.SetDirty(topBar.gameObject);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(topBar.gameObject.scene);
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(10);

            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            if (EditorGUI.EndChangeCheck())
            {
                topBar.UpdateAllDisplays();
                EditorUtility.SetDirty(topBar.gameObject);
            }
        }
    }
}
