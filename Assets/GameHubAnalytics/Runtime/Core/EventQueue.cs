using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameHub.Analytics
{
    public class EventQueue
    {
        private const string PREFS_KEY_COUNT = "GH_Analytics_QueueCount";
        private const string PREFS_KEY_ITEM  = "GH_Analytics_Queue_{0}";

        private readonly AnalyticsConfig _config;
        private readonly Queue<QueueItem> _queue;

        private struct QueueItem
        {
            public string missionId;
            public string firestoreJson;
        }

        public int  Count      => _queue.Count;
        public bool HasPending => _queue.Count > 0;

        public EventQueue(AnalyticsConfig config)
        {
            _config = config;
            _queue  = new Queue<QueueItem>();
            LoadFromPrefs();
        }

        public bool Enqueue(MissionEvent missionEvent)
        {
            if (_queue.Count >= _config.maxQueueSize)
            {
                Debug.LogWarning($"[Analytics] EventQueue full ({_config.maxQueueSize} items). Dropping event: [{missionEvent.eventType}]");
                return false;
            }

            _queue.Enqueue(new QueueItem
            {
                missionId     = missionEvent.missionId,
                firestoreJson = missionEvent.ToFirestoreJson()
            });

            SaveToPrefs();
            return true;
        }

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

        public void Dequeue()
        {
            if (_queue.Count > 0)
            {
                _queue.Dequeue();
                SaveToPrefs();
            }
        }

        public void Clear()
        {
            _queue.Clear();

            int count = PlayerPrefs.GetInt(PREFS_KEY_COUNT, 0);
            for (int i = 0; i < count; i++)
                PlayerPrefs.DeleteKey(string.Format(PREFS_KEY_ITEM, i));
            PlayerPrefs.DeleteKey(PREFS_KEY_COUNT);
            PlayerPrefs.Save();
        }

        private void SaveToPrefs()
        {
            var items = new List<QueueItem>(_queue);
            PlayerPrefs.SetInt(PREFS_KEY_COUNT, items.Count);

            for (int i = 0; i < items.Count; i++)
            {
                PlayerPrefs.SetString(string.Format(PREFS_KEY_ITEM, $"{i}_id"),   items[i].missionId);
                PlayerPrefs.SetString(string.Format(PREFS_KEY_ITEM, $"{i}_json"), items[i].firestoreJson);
            }

            PlayerPrefs.Save();
        }

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
        }
    }
}
