using UnityEngine;

namespace AnimatorTest
{
    /// <summary>
    /// 控制状态转换的脚本，用于测试
    /// </summary>
    public class AnimatorStateController : MonoBehaviour
    {
        private Animator animator;
        private int state1Hash;
        private int state2Hash;

        void Start()
        {
            animator = GetComponent<Animator>();
            if (animator != null)
            {
                // 获取状态哈希值
                state1Hash = Animator.StringToHash("Base Layer.TestState1");
                state2Hash = Animator.StringToHash("Base Layer.TestState2");

                Debug.Log("AnimatorStateController 已启动，将在 2 秒后自动切换到状态2，4 秒后切换到状态3");
                Invoke(nameof(SwitchToState2), 2f);
                Invoke(nameof(SwitchToState3), 4f);
            }
        }

        void SwitchToState2()
        {
            if (animator != null)
            {
                Debug.Log("切换到状态2");
                animator.SetTrigger("ToState2");
            }
        }

        void SwitchToState3()
        {
            if (animator != null)
            {
                Debug.Log("切换到状态3");
                animator.SetTrigger("ToState3");
            }
        }
    }
}