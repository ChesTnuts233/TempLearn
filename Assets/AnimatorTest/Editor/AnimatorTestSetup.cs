using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

namespace AnimatorTest.Editor
{
    public class AnimatorTestSetup : EditorWindow
    {
        [MenuItem("Tools/AnimatorTest/创建测试单元")]
        public static void CreateTestUnit()
        {
            // 创建第一个 AnimationClip
            AnimationClip clip1 = CreateAnimationClip("TestAnimation1", 0f, 360f);
            string clip1Path = "Assets/AnimatorTest/TestAnimation1.anim";
            AssetDatabase.CreateAsset(clip1, clip1Path);

            // 创建第二个 AnimationClip
            AnimationClip clip2 = CreateAnimationClip("TestAnimation2", 360f, 0f);
            string clip2Path = "Assets/AnimatorTest/TestAnimation2.anim";
            AssetDatabase.CreateAsset(clip2, clip2Path);

            // 创建第三个 AnimationClip
            AnimationClip clip3 = CreateAnimationClip("TestAnimation3", 0f, 180f);
            string clip3Path = "Assets/AnimatorTest/TestAnimation3.anim";
            AssetDatabase.CreateAsset(clip3, clip3Path);

            AssetDatabase.SaveAssets();

            // 创建或获取 Animator Controller
            AnimatorController controller;
            string controllerPath = "Assets/AnimatorTest/TestAnimatorController.controller";
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) != null)
            {
                // 删除旧的 controller 并重新创建
                AssetDatabase.DeleteAsset(controllerPath);
            }
            controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

            // 获取根状态机
            AnimatorStateMachine rootStateMachine = controller.layers[0].stateMachine;

            // 创建状态1
            AnimatorState state1 = rootStateMachine.AddState("TestState1");
            state1.motion = clip1;
            state1.AddStateMachineBehaviour<AnimatorStateListener>();

            // 创建状态2
            AnimatorState state2 = rootStateMachine.AddState("TestState2");
            state2.motion = clip2;
            state2.AddStateMachineBehaviour<AnimatorStateListener>();

            // 创建状态3
            AnimatorState state3 = rootStateMachine.AddState("TestState3");
            state3.motion = clip3;
            state3.AddStateMachineBehaviour<AnimatorStateListener>();

            // 设置默认状态
            rootStateMachine.defaultState = state1;

            // 创建 Trigger 参数
            controller.AddParameter("ToState2", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("ToState3", AnimatorControllerParameterType.Trigger);

            // 创建从状态1到状态2的过渡
            AnimatorStateTransition transition1 = state1.AddTransition(state2);
            transition1.hasExitTime = false;
            transition1.exitTime = 0f;
            transition1.duration = 0.1f;
            transition1.AddCondition(AnimatorConditionMode.If, 0, "ToState2");

            // 创建从状态2到状态3的过渡
            AnimatorStateTransition transition2 = state2.AddTransition(state3);
            transition2.hasExitTime = false;
            transition2.exitTime = 0f;
            transition2.duration = 0.1f;
            transition2.AddCondition(AnimatorConditionMode.If, 0, "ToState3");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 在场景中创建或查找测试对象
            GameObject testObj = GameObject.Find("AnimatorTestObject");
            if (testObj == null)
            {
                testObj = new GameObject("AnimatorTestObject");
            }

            // 添加组件
            Animator animator = testObj.GetComponent<Animator>();
            if (animator == null)
            {
                animator = testObj.AddComponent<Animator>();
            }
            animator.runtimeAnimatorController = controller;

            AnimationEventReceiver receiver = testObj.GetComponent<AnimationEventReceiver>();
            if (receiver == null)
            {
                receiver = testObj.AddComponent<AnimationEventReceiver>();
            }

            AnimatorStateController controllerScript = testObj.GetComponent<AnimatorStateController>();
            if (controllerScript == null)
            {
                controllerScript = testObj.AddComponent<AnimatorStateController>();
            }

            Debug.Log("测试单元创建完成！");
            Debug.Log($"AnimationClip1: {clip1Path}");
            Debug.Log($"AnimationClip2: {clip2Path}");
            Debug.Log($"AnimationClip3: {clip3Path}");
            Debug.Log("Animator Controller: Assets/AnimatorTest/TestAnimatorController.controller");
            Debug.Log("请运行场景查看日志顺序，将在2秒后自动切换到状态2，4秒后切换到状态3");
        }

        private static AnimationClip CreateAnimationClip(string name, float startValue, float endValue)
        {
            AnimationClip clip = new AnimationClip();
            clip.name = name;
            clip.frameRate = 30f;
            clip.legacy = false;

            // 添加一个简单的旋转动画曲线（让动画有内容）
            AnimationCurve curve = AnimationCurve.Linear(0f, startValue, 1f, endValue);
            clip.SetCurve("", typeof(Transform), "localEulerAngles.y", curve);
            clip.EnsureQuaternionContinuity();

            // 在第一帧添加事件
            AnimationEvent firstFrameEvent = new AnimationEvent();
            firstFrameEvent.time = 0f;
            firstFrameEvent.functionName = "OnFirstFrame";
            firstFrameEvent.stringParameter = $"{name}_FirstFrame";

            // 在最后一帧添加事件
            AnimationEvent lastFrameEvent = new AnimationEvent();
            lastFrameEvent.time = clip.length;
            lastFrameEvent.functionName = "OnLastFrame";
            lastFrameEvent.stringParameter = $"{name}_LastFrame";

            AnimationEvent[] events = new AnimationEvent[] { firstFrameEvent, lastFrameEvent };
            AnimationUtility.SetAnimationEvents(clip, events);

            return clip;
        }
    }
}