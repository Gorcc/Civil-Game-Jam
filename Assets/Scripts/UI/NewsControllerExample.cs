using UnityEngine;

namespace CivilGameJam.UI
{
    /// <summary>
    /// Başka bir koddan (örneğin Oyun Yöneticisi, Olay Yöneticisi veya Görev Sistemi)
    /// NewsTicker'a nasıl haber gönderileceğini gösteren örnek kod.
    /// Bu scripti istediğiniz bir GameObject'e ekleyip test edebilirsiniz.
    /// </summary>
    public class NewsControllerExample : MonoBehaviour
    {
        [Header("--- Test Tuşları ---")]
        [Tooltip("Hemen yeni bir son dakika haberi basmak için tuş.")]
        [SerializeField] private KeyCode breakingNewsKey = KeyCode.Alpha1;

        [Tooltip("Kuyruğa yeni bir haber eklemek için tuş.")]
        [SerializeField] private KeyCode enqueueNewsKey = KeyCode.Alpha2;

        private int newsCounter = 1;

        private void Update()
        {
            // 1 Tuşuna basıldığında acil son dakika haberi geçer
            if (Input.GetKeyDown(breakingNewsKey))
            {
                TriggerBreakingNews($"| ACİL HABER #{newsCounter++}: Şehir genelinde su kesintisi yaşanacağı bildirildi! |");
            }

            // 2 Tuşuna basıldığında mevcut haber bitince oynamak üzere sıraya ekler
            if (Input.GetKeyDown(enqueueNewsKey))
            {
                QueueNewsItem($"| EKONOMİ #{newsCounter++}: Yeni sanayi yatırımları için bütçe onaylandı. |");
            }
        }

        /// <summary>
        /// Herhangi bir scriptinizden bu şekilde doğrudan çağırabilirsiniz:
        /// </summary>
        public void TriggerBreakingNews(string message)
        {
            if (NewsTicker.Instance != null)
            {
                NewsTicker.Instance.SetBadgeText("SON DAKİKA");
                NewsTicker.Instance.SetText(message, restartImmediately: true);
                Debug.Log($"[NewsControllerExample] Acil haber gönderildi: {message}");
            }
            else
            {
                Debug.LogWarning("[NewsControllerExample] Sahnede NewsTicker bulunamadı!");
            }
        }

        /// <summary>
        /// Sıradaki haberlere eklemek için:
        /// </summary>
        public void QueueNewsItem(string message)
        {
            if (NewsTicker.Instance != null)
            {
                NewsTicker.Instance.EnqueueNews(message);
                Debug.Log($"[NewsControllerExample] Haber sıraya eklendi: {message}");
            }
        }
    }
}
