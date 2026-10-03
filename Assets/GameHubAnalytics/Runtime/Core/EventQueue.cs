using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameHub.Analytics
{
    /// <summary>
    /// Hàng đợi event cục bộ để đảm bảo không mất data khi mất kết nối.
    /// Tự động persist vào PlayerPrefs — tồn tại qua các lần restart game.
    /// </summary>
    public class EventQueue
    {
        // ─────────────────────────────────────────────────────────
        //  Constants
        // ─────────────────────────────────────────────────────────

        private const string PREFS_KEY_COUNT = "GH_Analytics_QueueCount";
        private const string PREFS_KEY_ITEM  = "GH_Analytics_Queue_{0}"; // format với index

        // ─────────────────────────────────────────────────────────
        //  Fields
        // ─────────────────────────────────────────────────────────

        private readonly AnalyticsConfig _config;
        private readonly Queue<QueueItem> _queue;

        // ─────────────────────────────────────────────────────────
        //  Nested Types
        // ─────────────────────────────────────────────────────────

        private struct QueueItem
        {
            public string missionId;
            public string firestoreJson;
        }

        // ─────────────────────────────────────────────────────────
        //  Properties
        // ─────────────────────────────────────────────────────────

        public int  Count      => _queue.Count;
        public bool HasPending => _queue.Count > 0;

        // ─────────────────────────────────────────────────────────
        //  Constructor
        // ─────────────────────────────────────────────────────────

        public EventQueue(AnalyticsConfig config)
        {
            _config = config;
            _queue  = new Queue<QueueItem>();
            LoadFromPrefs();
        }

        // ─────────────────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────────────────

        /// <summary>
        /// Thêm một MissionEvent vào queue để retry sau.
        /// </summary>
        /// <returns>true nếu enqueue thành công, false nếu queue đầy</returns>
        public bool Enqueue(MissionEvent missionEvent)
        {
            if (_queue.Count >= _config.maxQueueSize)
            {
                Debug.LogWarning($"[Analytics] EventQueue đầy ({_config.maxQueueSize} items). " +
                                 $"Bỏ qua event: [{missionEvent.eventType}]");
                return false;
            }

            _queue.Enqueue(new QueueItem
            {
                missionId     = missionEvent.missionId,
                firestoreJson = missionEvent.ToFirestoreJson()
            });

            SaveToPrefs();
            Debug.Log($"[Analytics] Queued event [{missionEvent.eventType}]. Queue size: {_queue.Count}");
            return true;
        }

        /// <summary>Xem event đầu queue mà không xóa.</summary>
        public bool TryPeek(out string missionId, out string firestoreJson)
        {
            if (_queue.Count == 0)
            {
                missionId     = null;
                firestoreJson = null;
                return false;
            }

            var item = _queue.Peek();
            missionId     = item.missionId;
            firestoreJson = item.firestoreJson;
            return true;
        }

        /// <summary>Xóa event đầu queue sau khi đã gửi thành công.</summary>
        public void Dequeue()
        {
            if (_queue.Count > 0)
            {
                _queue.Dequeue();
                SaveToPrefs();
            }
        }

        /// <summary>Xóa toàn bộ queue và PlayerPrefs cache.</summary>
        public void Clear()
        {
            _queue.Clear();

            // Xóa tất cả keys
            int count = PlayerPrefs.GetInt(PREFS_KEY_COUNT, 0);
            for (int i = 0; i < count; i++)
                PlayerPrefs.DeleteKey(string.Format(PREFS_KEY_ITEM, i));
            PlayerPrefs.DeleteKey(PREFS_KEY_COUNT);
            PlayerPrefs.Save();

            Debug.Log("[Analytics] EventQueue đã xóa.");
        }

        // ─────────────────────────────────────────────────────────
        //  Persistence
        // ─────────────────────────────────────────────────────────

        /// <summary>Lưu queue ra PlayerPrefs (mỗi item = 2 keys: missionId + json)</summary>
        private void SaveToPrefs()
        {
            var items = new List<QueueItem>(_queue);
            PlayerPrefs.SetInt(PREFS_KEY_COUNT, items.Count);

            for (int i = 0; i < items.Count; i++)
            {
                // Mỗi item lưu 2 key: missionId và json
                PlayerPrefs.SetString(string.Format(PREFS_KEY_ITEM, $"{i}_id"),   items[i].missionId);
                PlayerPrefs.SetString(string.Format(PREFS_KEY_ITEM, $"{i}_json"), items[i].firestoreJson);
            }

            PlayerPrefs.Save();
        }

        /// <summary>Load queue từ PlayerPrefs khi khởi tạo.</summary>
        private void LoadFromPrefs()
        {
            int count = PlayerPrefs.GetInt(PREFS_KEY_COUNT, 0);
            if (count == 0) return;

            int loaded = 0;
            for (int i = 0; i < count; i++)
            {
                string id   = PlayerPrefs.GetString(string.Format(PREFS_KEY_ITEM, $"{i}_id"), "");
                string json = PlayerPrefs.GetString(string.Format(PREFS_KEY_ITEM, $"{i}_json"), "");

                if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(json))
                {
                    _queue.Enqueue(new QueueItem { missionId = id, firestoreJson = json });
                    loaded++;
                }
            }

            if (loaded > 0)
                Debug.Log($"[Analytics] Khôi phục {loaded} event(s) từ local cache.");
        }
    }
}
