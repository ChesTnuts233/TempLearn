using UnityEngine;
using LifecycleTest;

namespace AnimatorTest
{
    public class AnimationEventReceiver : MonoBehaviour
    {
        private int frameCount = 0;
        private bool hasLoggedFirstUpdate = false;
        private bool hasLoggedFirstLateUpdate = false;

        void Update()
        {
            frameCount++;
            // 只在第一帧打印
            if (!hasLoggedFirstUpdate && frameCount == 1)
            {
                Debug.Log($"[MonoBehaviour.Update] 第一帧, 时间: {Time.time:F4}, 帧: {Time.frameCount}");
                hasLoggedFirstUpdate = true;
            }
        }

        void LateUpdate()
        {
            // 只在第一帧打印
            if (!hasLoggedFirstLateUpdate && frameCount == 1)
            {
                Debug.Log($"[MonoBehaviour.LateUpdate] 第一帧, 时间: {Time.time:F4}, 帧: {Time.frameCount}");
                hasLoggedFirstLateUpdate = true;
            }
        }

        public void OnAnimationEvent(string eventName)
        {
            Debug.Log($"[AnimationEvent] 事件名: {eventName}, 时间: {Time.time:F4}, 帧: {Time.frameCount}");
        }

        public void OnFirstFrame(string param = "")
        {
            string animName = string.IsNullOrEmpty(param) ? "未知动画" : param.Replace("_FirstFrame", "");
            string logMsg = $"[AnimationEvent] 第一帧事件 - 动画: {animName}, 时间: {Time.time:F4}, 帧: {Time.frameCount}";
            Debug.Log(logMsg);
            TestResultManager.Instance?.RecordAnimatorEvent("AnimationEvent_FirstFrame", animName, Time.time, Time.frameCount, 0, $"param: {param}");
        }

        public void OnLastFrame(string param = "")
        {
            string animName = string.IsNullOrEmpty(param) ? "未知动画" : param.Replace("_LastFrame", "");
            string logMsg = $"[AnimationEvent] 最后一帧事件 - 动画: {animName}, 时间: {Time.time:F4}, 帧: {Time.frameCount}";
            Debug.Log(logMsg);
            TestResultManager.Instance?.RecordAnimatorEvent("AnimationEvent_LastFrame", animName, Time.time, Time.frameCount, 1.0f, $"param: {param}");
        }
    }
}