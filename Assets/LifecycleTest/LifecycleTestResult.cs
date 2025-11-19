using System;
using System.Collections.Generic;
using UnityEngine;

namespace LifecycleTest
{
    /// <summary>
    /// 生命周期测试结果数据类
    /// </summary>
    [Serializable]
    public class LifecycleEventData
    {
        public string eventName;
        public float time;
        public int frame;
        public float deltaTime;
        public float normalizedTime;
        public string additionalInfo;

        public LifecycleEventData(string eventName, float time, int frame, float deltaTime, float normalizedTime = 0, string additionalInfo = "")
        {
            this.eventName = eventName;
            this.time = time;
            this.frame = frame;
            this.deltaTime = deltaTime;
            this.normalizedTime = normalizedTime;
            this.additionalInfo = additionalInfo;
        }
    }

    /// <summary>
    /// 测试结果容器
    /// </summary>
    [Serializable]
    public class LifecycleTestResult
    {
        public string testName;
        public string startTime;
        public List<LifecycleEventData> events = new List<LifecycleEventData>();
        [System.NonSerialized]
        public Dictionary<string, List<LifecycleEventData>> eventsByType = new Dictionary<string, List<LifecycleEventData>>();

        public void AddEvent(LifecycleEventData eventData)
        {
            events.Add(eventData);

            if (!eventsByType.ContainsKey(eventData.eventName))
            {
                eventsByType[eventData.eventName] = new List<LifecycleEventData>();
            }
            eventsByType[eventData.eventName].Add(eventData);
        }

        public string ToJson()
        {
            return JsonUtility.ToJson(this, true);
        }

        public string ToSummary()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine($"=== 生命周期测试结果总结 ===");
            sb.AppendLine($"测试名称: {testName}");
            sb.AppendLine($"开始时间: {startTime}");
            sb.AppendLine($"总事件数: {events.Count}");
            sb.AppendLine();

            sb.AppendLine("=== 事件类型统计 ===");
            foreach (var kvp in eventsByType)
            {
                sb.AppendLine($"{kvp.Key}: {kvp.Value.Count} 次");
            }
            sb.AppendLine();

            sb.AppendLine("=== 执行顺序（前20个事件）===");
            int count = Mathf.Min(20, events.Count);
            for (int i = 0; i < count; i++)
            {
                var evt = events[i];
                sb.AppendLine($"{i + 1}. [{evt.frame}] {evt.eventName} - 时间: {evt.time:F6}, deltaTime: {evt.deltaTime:F6}");
            }

            return sb.ToString();
        }
    }
}
