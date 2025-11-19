using UnityEngine;
using System.Collections;

namespace LifecycleTest
{
    /// <summary>
    /// 测试运行器 - 自动运行测试并保存结果
    /// </summary>
    public class TestRunner : MonoBehaviour
    {
        [Header("测试配置")]
        public float testDuration = 5f; // 测试持续时间（秒）
        public bool autoStart = true;

        void Start()
        {
            if (autoStart)
            {
                StartCoroutine(RunTests());
            }
        }

        IEnumerator RunTests()
        {
            Debug.Log("=== 开始运行生命周期和 Animator 测试 ===");
            
            // 初始化测试结果管理器
            TestResultManager.Instance.StartLifecycleTest("Unity生命周期完整测试");
            TestResultManager.Instance.StartAnimatorTest("Animator状态机和动画事件测试");
            
            // 等待测试完成
            yield return new WaitForSeconds(testDuration);
            
            // 保存结果
            TestResultManager.Instance.SaveResults();
            
            Debug.Log("=== 测试完成，结果已保存到 TestResults 文件夹 ===");
            
            // 在编辑器中自动停止播放
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #endif
        }

        [ContextMenu("手动运行测试")]
        public void ManualRunTests()
        {
            StartCoroutine(RunTests());
        }
    }
}
