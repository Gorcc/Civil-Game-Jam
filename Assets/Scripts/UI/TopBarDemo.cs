using UnityEngine;

namespace CivilGameJam.UI
{
    /// <summary>
    /// Üst menü değerlerini oyun içinde veya klavye tuşlarıyla test etmek için örnek script.
    /// B: Bütçeyi artırır
    /// O: Oy oranını değiştirir
    /// N: Nüfusu artırır
    /// G: Gün atlatır
    /// </summary>
    public class TopBarDemo : MonoBehaviour
    {
        private float demoBudget = 412000f;
        private float demoApproval = 54f;
        private int demoPopulation = 186;
        private int demoDay = 7;

        private void Update()
        {
            // B Tuşu: Bütçe simülasyonu
            if (Input.GetKeyDown(KeyCode.B))
            {
                demoBudget += 25000f;
                TopBarUI.Instance?.SetBudget(demoBudget, 19000f);
                Debug.Log($"[TopBarDemo] Yeni Bütçe: {demoBudget}");
            }

            // O Tuşu: Oy oranı simülasyonu (dalgalanma)
            if (Input.GetKeyDown(KeyCode.O))
            {
                demoApproval = (demoApproval >= 80f) ? 30f : demoApproval + 10f;
                TopBarUI.Instance?.SetApproval(demoApproval, 35f);
                Debug.Log($"[TopBarDemo] Yeni Oy Oranı: %{demoApproval}");
            }

            // N Tuşu: Nüfus artışı
            if (Input.GetKeyDown(KeyCode.N))
            {
                demoPopulation += 12;
                TopBarUI.Instance?.SetPopulation(demoPopulation);
                Debug.Log($"[TopBarDemo] Yeni Nüfus: {demoPopulation}");
            }

            // G Tuşu: Gün atlama
            if (Input.GetKeyDown(KeyCode.G))
            {
                demoDay = (demoDay >= 30) ? 1 : demoDay + 1;
                TopBarUI.Instance?.SetDay(demoDay, 30);
                Debug.Log($"[TopBarDemo] Yeni Gün: {demoDay} / 30");
            }
        }
    }
}
