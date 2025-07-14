using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SAS
{
    public class OnScreenLogUI : MonoBehaviour
    {
        [SerializeField] private GameObject m_LogEntryPrefab;
        [SerializeField] private Transform m_ContentParent;

        private float _logEntryLifetime = 0f;
        private readonly Queue<GameObject> _pool = new();
        private readonly List<LogEntry> _activeLogs = new();

        private class LogEntry
        {
            public GameObject GameObject;
            public TMP_Text Text;
            public float CreationTime;
        }

        private void Awake()
        {
            PrewarmPool();
            Debug.InitializeOnScreenLogger(this);
        }

        private void Update()
        {
            float now = Time.realtimeSinceStartup;
            for (int i = _activeLogs.Count - 1; i >= 0; i--)
            {
                float age = now - _activeLogs[i].CreationTime;
                if (_logEntryLifetime > 0 && age > _logEntryLifetime)
                {
                    ReturnToPool(_activeLogs[i]);
                    _activeLogs.RemoveAt(i);
                }
                else if (_logEntryLifetime > 0 && age > _logEntryLifetime * 0.7f)
                {
                    float fadeRatio = 1f - (age - _logEntryLifetime * 0.7f) / (_logEntryLifetime * 0.3f);
                    var color = _activeLogs[i].Text.color;
                    color.a = Mathf.Clamp01(fadeRatio);
                    _activeLogs[i].Text.color = color;
                }
            }
        }

        public void AddLog(string message, LogLevel level, string tag = "")
        {
            if (_activeLogs.Count > 50)
            {
                ReturnToPool(_activeLogs[0]);
                _activeLogs.RemoveAt(0);
            }

            var entryGO = GetFromPool();
            entryGO.transform.SetParent(m_ContentParent, false);
            TMP_Text tmp = entryGO.GetComponent<TMP_Text>();
            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            string tagDisplay = string.IsNullOrEmpty(tag) ? "" : $"[{tag}] ";
            Color baseColor = level switch
            {
                LogLevel.Warning => Color.yellow,
                LogLevel.Error => Color.red,
                _ => Color.white
            };
            tmp.text = $"[{timestamp}] Tag: {tagDisplay} {message}";
            tmp.color = baseColor;
            _activeLogs.Add(new OnScreenLogUI.LogEntry
                { GameObject = entryGO, Text = tmp, CreationTime = Time.realtimeSinceStartup });
        }

        public void ClearLogs()
        {
            foreach (var log in _activeLogs)
                ReturnToPool(log);
            _activeLogs.Clear();
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