using System;
using System.Collections.Generic;
using UnityEngine;

namespace AnimatorTest
{
    /// <summary>
    /// Animator 测试结果数据类
    /// </summary>
    [Serializable]
    public class AnimatorEventData
    {
        public string eventName;
        public string stateName;
        public float time;
        public int frame;
        public float normalizedTime;
        public string additionalInfo;

        public AnimatorEventData(string eventName, string stateName, float time, int frame, float normalizedTime, string additionalInfo = "")
        {
            this.eventName = eventName;
            this.stateName = stateName;
            this.time = time;
            this.frame = frame;
            this.normalizedTime = normalizedTime;
            this.additionalInfo = additionalInfo;
        }
    }

    /// <summary>
    /// Animator 测试结果容器
    /// </summary>
    [Serializable]
    public class AnimatorTestResult
    {
        public string testName;
        public string startTime;
        public List<AnimatorEventData> events = new List<AnimatorEventData>();
        [System.NonSerialized]
        public Dictionary<string, List<AnimatorEventData>> eventsByType = new Dictionary<string, List<AnimatorEventData>>();
        [System.NonSerialized]
        public Dictionary<string, List<AnimatorEventData>> eventsByState = new Dictionary<string, List<AnimatorEventData>>();

        public void AddEvent(AnimatorEventData eventData)
        {
            events.Add(eventData);

            if (!eventsByType.ContainsKey(eventData.eventName))
            {
                eventsByType[eventData.eventName] = new List<AnimatorEventData>();
            }
            eventsByType[eventData.eventName].Add(eventData);

            if (!eventsByState.ContainsKey(eventData.stateName))
            {
                eventsByState[eventData.stateName] = new List<AnimatorEventData>();
            }
            eventsByState[eventData.stateName].Add(eventData);
        }

        public string ToJson()
        {
            return JsonUtility.ToJson(this, true);
        }

        public string ToSummary()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine($"=== Animator 测试结果总结 ===");
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

            sb.AppendLine("=== 状态统计 ===");
            foreach (var kvp in eventsByState)
            {
                sb.AppendLine($"{kvp.Key}: {kvp.Value.Count} 次事件");
            }
            sb.AppendLine();

            sb.AppendLine("=== 执行顺序（前30个事件）===");
            int count = Mathf.Min(30, events.Count);
            for (int i = 0; i < count; i++)
            {
                var evt = events[i];
                sb.AppendLine($"{i + 1}. [{evt.frame}] {evt.eventName} - 状态: {evt.stateName}, 时间: {evt.time:F6}, normalizedTime: {evt.normalizedTime:F6}");
            }

            return sb.ToString();
        }
    }
}
