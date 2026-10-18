using UnityEditor;
using UnityEngine;

namespace CivilGameJam.UI
{
    [CustomEditor(typeof(NewsTicker))]
    public class NewsTickerEditor : Editor
    {
        private string testNewsInput = "| FLAŞ GELİŞME: Şehirde beklenmeyen bir olay gerçekleşti! |";

        [MenuItem("Tools/CivilGameJam/Haber Bandını Kur (Setup News Ticker)")]
        public static void SetupNewsTickerFromMenu()
        {
            GameObject panelObj = GameObject.Find("SubtitlePanel");
            if (panelObj == null)
            {
                GameObject canvasObj = GameObject.Find("SubtitleCanvas");
                if (canvasObj != null)
                {
                    Transform pTrans = canvasObj.transform.Find("SubtitlePanel");
                    if (pTrans != null) panelObj = pTrans.gameObject;
                }
            }

            if (panelObj == null)
            {
                EditorUtility.DisplayDialog("Uyarı", "Sahnede 'SubtitlePanel' adında bir UI nesnesi bulunamadı!", "Tamam");
                return;
            }

            NewsTicker ticker = panelObj.GetComponent<NewsTicker>();
            if (ticker == null)
            {
                ticker = Undo.AddComponent<NewsTicker>(panelObj);
            }

            Undo.RegisterFullObjectHierarchyUndo(panelObj, "Setup Subtitle News Ticker");
            ticker.BuildOrFixUIHierarchy();
            EditorUtility.SetDirty(panelObj);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(panelObj.scene);

            Selection.activeGameObject = panelObj;
            EditorUtility.DisplayDialog("Başarılı", "Haber Bandı (SubtitlePanel) TV haber şeridi olarak başarıyla kuruldu!", "Harika");
        }

        public override void OnInspectorGUI()
        {
            NewsTicker ticker = (NewsTicker)target;

            // Üst bilgi kutusu
            EditorGUILayout.HelpBox(
                "Haber Bandı (TV Kayan Yazı) Sistemi:\n" +
                "• Başka kodlardan çağırmak için: NewsTicker.Instance.SetText(\"Haber Metni\");\n" +
                "• Sıraya eklemek için: NewsTicker.Instance.EnqueueNews(\"Sıradaki Haber\");",
                MessageType.Info
            );

            EditorGUILayout.Space(5);

            // UI Kurulum Butonu
            GUI.backgroundColor = new Color(0.2f, 0.7f, 0.3f);
            if (GUILayout.Button("🛠️ Haber Bandı UI Hiyerarşisini Kur / Onar", GUILayout.Height(36)))
            {
                Undo.RegisterFullObjectHierarchyUndo(ticker.gameObject, "Setup News Ticker UI");
                ticker.BuildOrFixUIHierarchy();
                EditorUtility.SetDirty(ticker.gameObject);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(ticker.gameObject.scene);
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(10);

            // Normal Inspector elemanları
            DrawDefaultInspector();

            EditorGUILayout.Space(10);

            // Çalışma anı (Play Mode) canlı test araçları
            if (Application.isPlaying)
            {
                EditorGUILayout.LabelField("--- Canlı Test Araçları (Play Mode) ---", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Şu anki haber: {ticker.CurrentText}");
                EditorGUILayout.LabelField($"Kuyruktaki haber adedi: {ticker.QueuedCount}");

                EditorGUILayout.Space(5);
                testNewsInput = EditorGUILayout.TextField("Test Haber Metni:", testNewsInput);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Hemen Gönder (SetText)"))
                {
                    ticker.SetText(testNewsInput, restartImmediately: true);
                }
                if (GUILayout.Button("Kuyruğa Ekle (Enqueue)"))
                {
                    ticker.EnqueueNews(testNewsInput);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(5);
                if (GUILayout.Button(ticker.IsPaused ? "▶ Devam Ettir (Resume)" : "⏸️ Duraklat (Pause)"))
                {
                    if (ticker.IsPaused) ticker.ResumeTicker();
                    else ticker.PauseTicker();
                }
            }
        }
    }
}
