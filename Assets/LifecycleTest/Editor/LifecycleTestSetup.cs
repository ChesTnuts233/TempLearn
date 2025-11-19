using UnityEngine;
using UnityEditor;

namespace LifecycleTest.Editor
{
    /// <summary>
    /// Unity 生命周期测试单元创建工具
    /// </summary>
    public class LifecycleTestSetup
    {
        [MenuItem("Tools/LifecycleTest/创建生命周期测试单元")]
        public static void CreateLifecycleTestUnit()
        {
            // 创建主测试对象
            GameObject mainObj = GameObject.Find("LifecycleTestObject");
            if (mainObj == null)
            {
                mainObj = new GameObject("LifecycleTestObject");
            }

            // 添加生命周期测试组件
            UnityLifecycleTester tester = mainObj.GetComponent<UnityLifecycleTester>();
            if (tester == null)
            {
                tester = mainObj.AddComponent<UnityLifecycleTester>();
            }

            // 创建带相机的对象（用于测试 OnPreRender 和 OnPostRender）
            GameObject cameraObj = GameObject.Find("LifecycleTestCamera");
            if (cameraObj == null)
            {
                cameraObj = new GameObject("LifecycleTestCamera");
                Camera cam = cameraObj.AddComponent<Camera>();
                cam.tag = "MainCamera";
                
                // 添加生命周期测试组件到相机对象
                UnityLifecycleTester cameraTester = cameraObj.AddComponent<UnityLifecycleTester>();
            }

            Debug.Log("生命周期测试单元创建完成！");
            Debug.Log("包含对象：");
            Debug.Log("1. LifecycleTestObject - 主测试对象");
            Debug.Log("2. LifecycleTestCamera - 带相机的测试对象（用于测试渲染相关生命周期）");
            Debug.Log("请运行场景查看所有生命周期的执行顺序");
        }

        [MenuItem("Tools/LifecycleTest/创建完整测试场景（包含 Animator）")]
        public static void CreateFullTestScene()
        {
            // 先创建基础测试单元
            CreateLifecycleTestUnit();

            // 检查是否已有 Animator 测试对象
            GameObject animatorObj = GameObject.Find("AnimatorTestObject");
            if (animatorObj != null)
            {
                // 如果存在，也给它添加生命周期测试组件
                UnityLifecycleTester animatorTester = animatorObj.GetComponent<UnityLifecycleTester>();
                if (animatorTester == null)
                {
                    animatorTester = animatorObj.AddComponent<UnityLifecycleTester>();
                    Debug.Log("已为 AnimatorTestObject 添加生命周期测试组件");
                }
            }
            else
            {
                Debug.LogWarning("未找到 AnimatorTestObject，请先运行 Tools/AnimatorTest/创建测试单元");
            }

            Debug.Log("完整测试场景创建完成！现在可以同时观察生命周期和 Animator 事件的执行顺序");
        }
    }
}
