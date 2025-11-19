using UnityEngine;
using System.Collections;

namespace LifecycleTest
{
    /// <summary>
    /// Unity 生命周期完整测试脚本
    /// 用于研究和学习 Unity 所有生命周期的实际执行顺序
    /// </summary>
    public class UnityLifecycleTester : MonoBehaviour
    {
        private static int instanceCount = 0;
        private int instanceID;
        private Coroutine testCoroutine;

        #region 初始化阶段

        /// <summary>
        /// Awake: 对象创建时立即调用，在所有其他方法之前
        /// - 即使对象未激活也会调用
        /// - 在 OnEnable 之前调用
        /// - 用于初始化不依赖其他对象的变量
        /// </summary>
        void Awake()
        {
            instanceID = ++instanceCount;
            string logMsg = $"[{instanceID}] [Awake] 对象创建时立即调用，时间: {Time.time:F6}, 帧: {Time.frameCount}, deltaTime: {Time.deltaTime:F6}";
            Debug.Log(logMsg);
            TestResultManager.Instance?.RecordLifecycleEvent("Awake", Time.time, Time.frameCount, Time.deltaTime, 0, $"InstanceID: {instanceID}");
        }

        /// <summary>
        /// OnEnable: 对象激活时调用（每次激活都会调用）
        /// - 在 Awake 之后，Start 之前（首次激活时）
        /// - 对象从禁用状态变为激活状态时也会调用
        /// - 用于订阅事件、启用组件等
        /// </summary>
        void OnEnable()
        {
            string logMsg = $"[{instanceID}] [OnEnable] 对象激活时调用，时间: {Time.time:F6}, 帧: {Time.frameCount}, deltaTime: {Time.deltaTime:F6}";
            Debug.Log(logMsg);
            TestResultManager.Instance?.RecordLifecycleEvent("OnEnable", Time.time, Time.frameCount, Time.deltaTime, 0, $"InstanceID: {instanceID}");
        }

        /// <summary>
        /// Start: 首次激活后的第一帧调用，在所有 Update 之前
        /// - 只在对象首次激活时调用一次
        /// - 在 Awake 和 OnEnable 之后
        /// - 用于初始化依赖其他对象的变量
        /// </summary>
        void Start()
        {
            string logMsg = $"[{instanceID}] [Start] 首次激活后第一帧调用，时间: {Time.time:F6}, 帧: {Time.frameCount}, deltaTime: {Time.deltaTime:F6}";
            Debug.Log(logMsg);
            TestResultManager.Instance?.RecordLifecycleEvent("Start", Time.time, Time.frameCount, Time.deltaTime, 0, $"InstanceID: {instanceID}");

            // 启动协程测试
            testCoroutine = StartCoroutine(TestCoroutine());
        }

        #endregion

        #region 物理更新阶段

        /// <summary>
        /// FixedUpdate: 固定时间间隔调用（默认 0.02 秒，50Hz）
        /// - 与物理系统同步，不受帧率影响
        /// - 在 Update 之前或之后都可能调用（取决于物理时间步长）
        /// - 用于物理相关的计算（Rigidbody 操作等）
        /// </summary>
        void FixedUpdate()
        {
            // 只在第一帧打印，避免日志过多
            if (Time.fixedTime < 0.1f)
            {
                string logMsg = $"[{instanceID}] [FixedUpdate] 固定物理更新，时间: {Time.time:F6}, fixedTime: {Time.fixedTime:F6}, 帧: {Time.frameCount}, fixedDeltaTime: {Time.fixedDeltaTime:F6}";
                Debug.Log(logMsg);
                TestResultManager.Instance?.RecordLifecycleEvent("FixedUpdate", Time.time, Time.frameCount, Time.fixedDeltaTime, 0, $"fixedTime: {Time.fixedTime:F6}");
            }
        }

        #endregion

        #region 常规更新阶段

        /// <summary>
        /// Update: 每帧调用一次（帧率相关）
        /// - 在 FixedUpdate 之后（如果该帧有 FixedUpdate）
        /// - 在 LateUpdate 之前
        /// - 用于游戏逻辑、输入处理等
        /// </summary>
        void Update()
        {
            // 只在第一帧打印
            if (Time.frameCount <= 3)
            {
                string logMsg = $"[{instanceID}] [Update] 每帧更新，时间: {Time.time:F6}, 帧: {Time.frameCount}, deltaTime: {Time.deltaTime:F6}";
                Debug.Log(logMsg);
                TestResultManager.Instance?.RecordLifecycleEvent("Update", Time.time, Time.frameCount, Time.deltaTime, 0, $"InstanceID: {instanceID}");
            }
        }

        /// <summary>
        /// LateUpdate: 每帧在 Update 之后调用
        /// - 在所有 Update 执行完毕后调用
        /// - 在 OnAnimatorMove 之前
        /// - 用于相机跟随、UI 更新等需要等待其他对象更新完成的操作
        /// </summary>
        void LateUpdate()
        {
            // 只在第一帧打印
            if (Time.frameCount <= 3)
            {
                string logMsg = $"[{instanceID}] [LateUpdate] 延迟更新，时间: {Time.time:F6}, 帧: {Time.frameCount}, deltaTime: {Time.deltaTime:F6}";
                Debug.Log(logMsg);
                TestResultManager.Instance?.RecordLifecycleEvent("LateUpdate", Time.time, Time.frameCount, Time.deltaTime, 0, $"InstanceID: {instanceID}");
            }
        }

        #endregion

        #region 动画系统阶段

        /// <summary>
        /// OnAnimatorMove: Animator 处理完根运动后调用
        /// - 在 LateUpdate 之后
        /// - 在 OnAnimatorIK 之前
        /// - 用于处理根运动（Root Motion）
        /// - 注意：如果实现了此方法，Unity 不会自动应用根运动，需要手动处理
        /// </summary>
        void OnAnimatorMove()
        {
            // 只在第一帧打印
            if (Time.frameCount <= 3)
            {
                Debug.Log($"[{instanceID}] [OnAnimatorMove] 动画根运动处理，时间: {Time.time:F6}, 帧: {Time.frameCount}, deltaTime: {Time.deltaTime:F6}");
            }
        }

        /// <summary>
        /// OnAnimatorIK: Animator 的 IK（反向运动学）计算后调用
        /// - 在 OnAnimatorMove 之后
        /// - 用于 IK 相关的处理
        /// </summary>
        void OnAnimatorIK(int layerIndex)
        {
            // 只在第一帧打印
            if (Time.frameCount <= 3)
            {
                Debug.Log($"[{instanceID}] [OnAnimatorIK] 动画 IK 处理，layerIndex: {layerIndex}, 时间: {Time.time:F6}, 帧: {Time.frameCount}");
            }
        }

        #endregion

        #region 渲染阶段

        /// <summary>
        /// OnGUI: 每帧调用多次（用于渲染 GUI）
        /// - 在渲染阶段调用
        /// - 可能在同一帧内调用多次
        /// - 用于 IMGUI 系统（OnGUI）
        /// </summary>
        void OnGUI()
        {
            // 只在第一帧打印一次
            if (Time.frameCount == 1 && Event.current.type == EventType.Repaint)
            {
                Debug.Log($"[{instanceID}] [OnGUI] GUI 渲染，时间: {Time.time:F6}, 帧: {Time.frameCount}");
            }
        }

        /// <summary>
        /// OnPreRender: 相机开始渲染场景前调用
        /// - 需要挂载到有 Camera 组件的对象上
        /// </summary>
        void OnPreRender()
        {
            if (Time.frameCount <= 3)
            {
                Debug.Log($"[{instanceID}] [OnPreRender] 相机预渲染，时间: {Time.time:F6}, 帧: {Time.frameCount}");
            }
        }

        /// <summary>
        /// OnPostRender: 相机完成场景渲染后调用
        /// - 需要挂载到有 Camera 组件的对象上
        /// </summary>
        void OnPostRender()
        {
            if (Time.frameCount <= 3)
            {
                Debug.Log($"[{instanceID}] [OnPostRender] 相机后渲染，时间: {Time.time:F6}, 帧: {Time.frameCount}");
            }
        }

        /// <summary>
        /// OnRenderObject: 每个可见对象渲染时调用
        /// - 在渲染管线中调用
        /// </summary>
        void OnRenderObject()
        {
            if (Time.frameCount <= 3)
            {
                Debug.Log($"[{instanceID}] [OnRenderObject] 对象渲染，时间: {Time.time:F6}, 帧: {Time.frameCount}");
            }
        }

        #endregion

        #region 协程

        /// <summary>
        /// 协程：在 Update 之后执行
        /// - 使用 yield return null 会在下一帧的 Update 之后继续执行
        /// - 使用 yield return new WaitForFixedUpdate() 会在 FixedUpdate 之后执行
        /// </summary>
        IEnumerator TestCoroutine()
        {
            yield return null; // 等待一帧
            string logMsg1 = $"[{instanceID}] [Coroutine] 协程第一帧，时间: {Time.time:F6}, 帧: {Time.frameCount}";
            Debug.Log(logMsg1);
            TestResultManager.Instance?.RecordLifecycleEvent("Coroutine_AfterUpdate", Time.time, Time.frameCount, Time.deltaTime, 0, "yield return null");

            yield return new WaitForFixedUpdate(); // 等待 FixedUpdate
            string logMsg2 = $"[{instanceID}] [Coroutine] 协程 FixedUpdate 后，时间: {Time.time:F6}, 帧: {Time.frameCount}";
            Debug.Log(logMsg2);
            TestResultManager.Instance?.RecordLifecycleEvent("Coroutine_AfterFixedUpdate", Time.time, Time.frameCount, Time.deltaTime, 0, "yield return WaitForFixedUpdate");

            yield return new WaitForEndOfFrame(); // 等待帧结束
            string logMsg3 = $"[{instanceID}] [Coroutine] 协程帧结束后，时间: {Time.time:F6}, 帧: {Time.frameCount}";
            Debug.Log(logMsg3);
            TestResultManager.Instance?.RecordLifecycleEvent("Coroutine_EndOfFrame", Time.time, Time.frameCount, Time.deltaTime, 0, "yield return WaitForEndOfFrame");
        }

        #endregion

        #region 清理阶段

        /// <summary>
        /// OnDisable: 对象禁用时调用（每次禁用都会调用）
        /// - 对象从激活状态变为禁用状态时调用
        /// - 在 OnDestroy 之前（如果对象被销毁）
        /// - 用于取消事件订阅、禁用组件等
        /// </summary>
        void OnDisable()
        {
            Debug.Log($"[{instanceID}] [OnDisable] 对象禁用时调用，时间: {Time.time:F6}, 帧: {Time.frameCount}");

            // 停止协程
            if (testCoroutine != null)
            {
                StopCoroutine(testCoroutine);
            }
        }

        /// <summary>
        /// OnDestroy: 对象销毁时调用
        /// - 在 OnDisable 之后
        /// - 用于清理资源、取消订阅等
        /// </summary>
        void OnDestroy()
        {
            Debug.Log($"[{instanceID}] [OnDestroy] 对象销毁时调用，时间: {Time.time:F6}, 帧: {Time.frameCount}");
        }

        /// <summary>
        /// OnApplicationPause: 应用暂停时调用（移动平台）
        /// - 当应用失去焦点时调用
        /// </summary>
        void OnApplicationPause(bool pauseStatus)
        {
            Debug.Log($"[{instanceID}] [OnApplicationPause] 应用暂停状态: {pauseStatus}, 时间: {Time.time:F6}, 帧: {Time.frameCount}");
        }

        /// <summary>
        /// OnApplicationFocus: 应用焦点变化时调用
        /// - 当应用获得或失去焦点时调用
        /// </summary>
        void OnApplicationFocus(bool hasFocus)
        {
            Debug.Log($"[{instanceID}] [OnApplicationFocus] 应用焦点: {hasFocus}, 时间: {Time.time:F6}, 帧: {Time.frameCount}");
        }

        /// <summary>
        /// OnApplicationQuit: 应用退出时调用
        /// - 在编辑器停止播放或应用退出时调用
        /// </summary>
        void OnApplicationQuit()
        {
            Debug.Log($"[{instanceID}] [OnApplicationQuit] 应用退出，时间: {Time.time:F6}, 帧: {Time.frameCount}");
        }

        #endregion
    }
}
