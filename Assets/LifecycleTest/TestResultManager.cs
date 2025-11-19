using UnityEngine;
using System.IO;
using System;

namespace LifecycleTest
{
    /// <summary>
    /// 测试结果管理器 - 单例模式
    /// </summary>
    public class TestResultManager : MonoBehaviour
    {
        private static TestResultManager instance;
        private LifecycleTestResult lifecycleResult;
        // private AnimatorTest.AnimatorTestResult animatorResult; // 暂时注释，稍后修复
        private bool isRecording = false;

        public static TestResultManager Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject go = new GameObject("TestResultManager");
                    instance = go.AddComponent<TestResultManager>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }

        void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

        public void StartLifecycleTest(string testName)
        {
            lifecycleResult = new LifecycleTestResult
            {
                testName = testName,
                startTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
            };
            isRecording = true;
            Debug.Log($"[TestResultManager] 开始记录生命周期测试: {testName}");
        }

        public void StartAnimatorTest(string testName)
        {
            // animatorResult = new AnimatorTest.AnimatorTestResult
            // {
            //     testName = testName,
            //     startTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
            // };
            Debug.Log($"[TestResultManager] 开始记录 Animator 测试: {testName} (暂时禁用)");
        }

        public void RecordLifecycleEvent(string eventName, float time, int frame, float deltaTime, float normalizedTime = 0, string additionalInfo = "")
        {
            if (!isRecording || lifecycleResult == null) return;

            var eventData = new LifecycleEventData(eventName, time, frame, deltaTime, normalizedTime, additionalInfo);
            lifecycleResult.AddEvent(eventData);
        }

        public void RecordAnimatorEvent(string eventName, string stateName, float time, int frame, float normalizedTime, string additionalInfo = "")
        {
            // if (animatorResult == null) return;
            // var eventData = new AnimatorTest.AnimatorEventData(eventName, stateName, time, frame, normalizedTime, additionalInfo);
            // animatorResult.AddEvent(eventData);
            Debug.Log($"[TestResultManager] Animator 事件记录暂时禁用: {eventName} - {stateName}");
        }

        public void SaveResults()
        {
            string outputDir = Path.Combine(Application.dataPath, "..", "TestResults");
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            if (lifecycleResult != null)
            {
                string jsonPath = Path.Combine(outputDir, $"LifecycleTest_{DateTime.Now:yyyyMMdd_HHmmss}.json");
                string summaryPath = Path.Combine(outputDir, $"LifecycleTest_{DateTime.Now:yyyyMMdd_HHmmss}_Summary.txt");

                File.WriteAllText(jsonPath, lifecycleResult.ToJson());
                File.WriteAllText(summaryPath, lifecycleResult.ToSummary());

                Debug.Log($"[TestResultManager] 生命周期测试结果已保存:");
                Debug.Log($"  JSON: {jsonPath}");
                Debug.Log($"  总结: {summaryPath}");
            }

            // if (animatorResult != null)
            // {
            //     string jsonPath = Path.Combine(outputDir, $"AnimatorTest_{DateTime.Now:yyyyMMdd_HHmmss}.json");
            //     string summaryPath = Path.Combine(outputDir, $"AnimatorTest_{DateTime.Now:yyyyMMdd_HHmmss}_Summary.txt");
            //     
            //     File.WriteAllText(jsonPath, animatorResult.ToJson());
            //     File.WriteAllText(summaryPath, animatorResult.ToSummary());
            //     
            //     Debug.Log($"[TestResultManager] Animator 测试结果已保存:");
            //     Debug.Log($"  JSON: {jsonPath}");
            //     Debug.Log($"  总结: {summaryPath}");
            // }
        }

        void OnApplicationQuit()
        {
            SaveResults();
        }
    }
}
