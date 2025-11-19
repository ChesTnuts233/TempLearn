using UnityEngine;
using LifecycleTest;

namespace AnimatorTest
{
    /// <summary>
    /// Animator 状态机和状态事件监听器
    /// 监听所有 StateMachineBehaviour 回调事件
    /// </summary>
    public class AnimatorStateListener : StateMachineBehaviour
    {
        #region 状态机级别回调

        /// <summary>
        /// OnStateMachineEnter: 当进入状态机时调用
        /// - 在进入状态机的任何状态之前调用
        /// - 无论进入状态机的哪个状态，都会先调用此方法
        /// - 用于状态机级别的初始化
        /// </summary>
        public override void OnStateMachineEnter(Animator animator, int stateMachinePathHash)
        {
            string stateMachineName = GetStateMachineName(stateMachinePathHash);
            string logMsg = $"[OnStateMachineEnter] 状态机: {stateMachineName}, 路径哈希: {stateMachinePathHash}, 时间: {Time.time:F6}, 帧: {Time.frameCount}";
            Debug.Log(logMsg);
            TestResultManager.Instance?.RecordAnimatorEvent("OnStateMachineEnter", stateMachineName, Time.time, Time.frameCount, 0, $"pathHash: {stateMachinePathHash}");
        }

        /// <summary>
        /// OnStateMachineExit: 当退出状态机时调用
        /// - 在退出状态机的所有状态之后调用
        /// - 用于状态机级别的清理
        /// </summary>
        public override void OnStateMachineExit(Animator animator, int stateMachinePathHash)
        {
            string stateMachineName = GetStateMachineName(stateMachinePathHash);
            string logMsg = $"[OnStateMachineExit] 状态机: {stateMachineName}, 路径哈希: {stateMachinePathHash}, 时间: {Time.time:F6}, 帧: {Time.frameCount}";
            Debug.Log(logMsg);
            TestResultManager.Instance?.RecordAnimatorEvent("OnStateMachineExit", stateMachineName, Time.time, Time.frameCount, 0, $"pathHash: {stateMachinePathHash}");
        }

        #endregion

        #region 状态级别回调

        /// <summary>
        /// OnStateEnter: 当进入状态时调用
        /// - 在进入具体状态时调用
        /// - 在 OnStateMachineEnter 之后调用（如果是从外部进入状态机）
        /// - 用于状态级别的初始化
        /// </summary>
        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            string stateName = GetStateName(animator, stateInfo, layerIndex);
            string logMsg = $"[OnStateEnter] 状态: {stateName}, 时间: {Time.time:F6}, 帧: {Time.frameCount}, normalizedTime: {stateInfo.normalizedTime:F6}, layerIndex: {layerIndex}";
            Debug.Log(logMsg);
            TestResultManager.Instance?.RecordAnimatorEvent("OnStateEnter", stateName, Time.time, Time.frameCount, stateInfo.normalizedTime, $"layerIndex: {layerIndex}");
        }

        /// <summary>
        /// OnStateUpdate: 当状态更新时调用（每帧）
        /// - 在状态运行期间每帧调用
        /// - 在 OnStateEnter 之后调用
        /// - 用于状态运行时的逻辑更新
        /// </summary>
        public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            // 只在第一帧或 normalizedTime 很小时打印，避免日志过多
            if (stateInfo.normalizedTime < 0.01f)
            {
                string stateName = GetStateName(animator, stateInfo, layerIndex);
                string logMsg = $"[OnStateUpdate] 状态: {stateName}, 时间: {Time.time:F6}, 帧: {Time.frameCount}, normalizedTime: {stateInfo.normalizedTime:F6}, layerIndex: {layerIndex}";
                Debug.Log(logMsg);
                TestResultManager.Instance?.RecordAnimatorEvent("OnStateUpdate", stateName, Time.time, Time.frameCount, stateInfo.normalizedTime, $"layerIndex: {layerIndex}");
            }
        }

        /// <summary>
        /// OnStateMove: 当状态移动时调用（每帧，在 OnStateUpdate 之后）
        /// - 在 OnStateUpdate 之后调用
        /// - 用于处理根运动（Root Motion）
        /// - 在动画系统处理完根运动后调用
        /// </summary>
        public override void OnStateMove(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            // 只在第一帧或 normalizedTime 很小时打印，避免日志过多
            if (stateInfo.normalizedTime < 0.01f)
            {
                string stateName = GetStateName(animator, stateInfo, layerIndex);
                string logMsg = $"[OnStateMove] 状态: {stateName}, 时间: {Time.time:F6}, 帧: {Time.frameCount}, normalizedTime: {stateInfo.normalizedTime:F6}, layerIndex: {layerIndex}";
                Debug.Log(logMsg);
                TestResultManager.Instance?.RecordAnimatorEvent("OnStateMove", stateName, Time.time, Time.frameCount, stateInfo.normalizedTime, $"layerIndex: {layerIndex}");
            }
        }

        /// <summary>
        /// OnStateIK: 当状态 IK 计算时调用（每帧，在 OnStateMove 之后）
        /// - 在 OnStateMove 之后调用
        /// - 用于 IK（反向运动学）处理
        /// - 需要启用 IK 才会调用
        /// </summary>
        public override void OnStateIK(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            // 只在第一帧或 normalizedTime 很小时打印，避免日志过多
            if (stateInfo.normalizedTime < 0.01f)
            {
                string stateName = GetStateName(animator, stateInfo, layerIndex);
                string logMsg = $"[OnStateIK] 状态: {stateName}, 时间: {Time.time:F6}, 帧: {Time.frameCount}, normalizedTime: {stateInfo.normalizedTime:F6}, layerIndex: {layerIndex}";
                Debug.Log(logMsg);
                TestResultManager.Instance?.RecordAnimatorEvent("OnStateIK", stateName, Time.time, Time.frameCount, stateInfo.normalizedTime, $"layerIndex: {layerIndex}");
            }
        }

        /// <summary>
        /// OnStateExit: 当退出状态时调用
        /// - 在退出状态时调用
        /// - 在 OnStateMachineExit 之前调用（如果是退出状态机）
        /// - 用于状态级别的清理
        /// </summary>
        public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            string stateName = GetStateName(animator, stateInfo, layerIndex);
            string logMsg = $"[OnStateExit] 状态: {stateName}, 时间: {Time.time:F6}, 帧: {Time.frameCount}, normalizedTime: {stateInfo.normalizedTime:F6}, layerIndex: {layerIndex}";
            Debug.Log(logMsg);
            TestResultManager.Instance?.RecordAnimatorEvent("OnStateExit", stateName, Time.time, Time.frameCount, stateInfo.normalizedTime, $"layerIndex: {layerIndex}");
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 获取状态名称
        /// </summary>
        private string GetStateName(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            if (stateInfo.IsName("TestState1"))
                return "TestState1";
            if (stateInfo.IsName("TestState2"))
                return "TestState2";
            if (stateInfo.IsName("TestState3"))
                return "TestState3";
            return $"Hash_{stateInfo.fullPathHash}";
        }

        /// <summary>
        /// 获取状态机名称（通过路径哈希值）
        /// </summary>
        private string GetStateMachineName(int stateMachinePathHash)
        {
            // 尝试匹配常见的状态机路径
            if (stateMachinePathHash == Animator.StringToHash("Base Layer"))
                return "Base Layer";
            if (stateMachinePathHash == Animator.StringToHash("Base Layer.Combat"))
                return "Combat";
            if (stateMachinePathHash == Animator.StringToHash("Base Layer.Movement"))
                return "Movement";

            // 如果无法匹配，返回哈希值
            return $"StateMachine_{stateMachinePathHash}";
        }

        #endregion
    }
}