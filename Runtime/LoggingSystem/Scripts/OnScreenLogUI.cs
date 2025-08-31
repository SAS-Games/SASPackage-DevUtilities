using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace SAS
{
    public class OnScreenLogUI : MonoBehaviour
    {
        [SerializeField] private GameObject m_LogEntryPrefab;
        [SerializeField] private Transform m_ContentParent;

        private float _logEntryLifetime = 5f;
        private readonly Queue<GameObject> _pool = new();
        private readonly List<LogEntry> _normalLogs = new();
        private readonly Dictionary<int, LogEntry> _slotLogs = new();

        private class LogEntry
        {
            public GameObject GameObject;
            public TMP_Text Text;
            public float CreationTime;
            public int SlotIndex = -1;
        }

        private void Awake()
        {
            PrewarmPool();
            Debug.InitializeOnScreenLogger(this);
        }

        private void Update()
        {
            float now = Time.realtimeSinceStartup;
            // --- Normal logs ---
            for (int i = _normalLogs.Count - 1; i >= 0; i--)
            {
                float age = now - _normalLogs[i].CreationTime;
                if (_logEntryLifetime > 0 && age > _logEntryLifetime)
                {
                    ReturnToPool(_normalLogs[i]);
                    _normalLogs.RemoveAt(i);
                }
                else if (_logEntryLifetime > 0 && age > _logEntryLifetime * 0.7f)
                {
                    float fadeRatio = 1f - (age - _logEntryLifetime * 0.7f) / (_logEntryLifetime * 0.3f);
                    var color = _normalLogs[i].Text.color;
                    color.a = Mathf.Clamp01(fadeRatio);
                    _normalLogs[i].Text.color = color;
                }
            }

            // --- Slot logs ---
            var expiredSlots = new List<int>();
            foreach (var kvp in _slotLogs)
            {
                var entry = kvp.Value;
                float age = now - entry.CreationTime;

                if (_logEntryLifetime > 0 && age > _logEntryLifetime)
                {
                    ReturnToPool(entry);
                    expiredSlots.Add(kvp.Key); // mark for removal
                }
                else if (_logEntryLifetime > 0 && age > _logEntryLifetime * 0.7f)
                {
                    float fadeRatio = 1f - (age - _logEntryLifetime * 0.7f) / (_logEntryLifetime * 0.3f);
                    var color = entry.Text.color;
                    color.a = Mathf.Clamp01(fadeRatio);
                    entry.Text.color = color;
                }
            }

            // Remove expired slot logs from dictionary
            foreach (var key in expiredSlots)
                _slotLogs.Remove(key);

        }


        public void AddLog(string message, LogLevel level, string tag = "", int slotIndex = -1)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            string tagDisplay = string.IsNullOrEmpty(tag) ? "" : $"[{tag}] ";
            string finalMessage = $"[{timestamp}] {tagDisplay}{message}";
            Color baseColor = level switch
            {
                LogLevel.Warning => Color.yellow,
                LogLevel.Error => Color.red,
                _ => Color.white
            };

            // --- Slot log handling ---
            if (slotIndex >= 0)
            {
                if (_slotLogs.TryGetValue(slotIndex, out var existingEntry))
                {
                    existingEntry.Text.text = finalMessage;
                    existingEntry.Text.color = baseColor;
                    existingEntry.CreationTime = Time.realtimeSinceStartup;
                }
                else
                {
                    var entryGO = GetFromPool();
                    entryGO.transform.SetParent(m_ContentParent, false);
                    TMP_Text tmp = entryGO.GetComponent<TMP_Text>();
                    tmp.text = finalMessage;
                    tmp.color = baseColor;

                    var newEntry = new LogEntry
                    {
                        GameObject = entryGO,
                        Text = tmp,
                        CreationTime = Time.realtimeSinceStartup,
                        SlotIndex = slotIndex
                    };

                    _slotLogs[slotIndex] = newEntry;
                }

                RebuildVisualOrder();
                return;
            }

            if (_normalLogs.Count >= 50)
            {
                var oldest = _normalLogs[^1];
                ReturnToPool(oldest);
                _normalLogs.RemoveAt(_normalLogs.Count - 1);
            }

            var obj = GetFromPool();
            obj.transform.SetParent(m_ContentParent, false);
            TMP_Text text = obj.GetComponent<TMP_Text>();
            text.text = finalMessage;
            text.color = baseColor;

            _normalLogs.Insert(0, new LogEntry
            {
                GameObject = obj,
                Text = text,
                CreationTime = Time.realtimeSinceStartup
            });

            RebuildVisualOrder();
        }

        /// <summary>
        /// Rebuilds hierarchy so slot logs (sorted by index) appear on top, then normal logs (newest first).
        /// </summary>
        private void RebuildVisualOrder()
        {
            // Slot logs (sorted by slotIndex)
            foreach (var kvp in _slotLogs.OrderBy(k => k.Key))
            {
                kvp.Value.GameObject.transform.SetAsLastSibling();
            }

            //Normal logs (newest first → already stored in that order)
            foreach (var log in _normalLogs)
            {
                log.GameObject.transform.SetAsLastSibling();
            }
        }


        public void ClearLogs()
        {
            foreach (var log in _normalLogs)
                ReturnToPool(log);
            _normalLogs.Clear();

            foreach (var kvp in _slotLogs)
                ReturnToPool(kvp.Value);
            _slotLogs.Clear();
        }



        public void SetLifetime(float value)
        {
            _logEntryLifetime = value;
        }

        private void PrewarmPool()
        {
            for (int i = 0; i < 30; i++)
            {
                var obj = Instantiate(m_LogEntryPrefab, transform);
                obj.SetActive(false);
                _pool.Enqueue(obj);
            }
        }

        private GameObject GetFromPool()
        {
            if (_pool.Count > 0)
            {
                var obj = _pool.Dequeue();
                obj.SetActive(true);
                return obj;
            }

            return Instantiate(m_LogEntryPrefab, transform);
        }

        private void ReturnToPool(LogEntry entry)
        {
            entry.GameObject.SetActive(false);
            entry.GameObject.transform.SetParent(transform);
            _pool.Enqueue(entry.GameObject);
        }
    }
}