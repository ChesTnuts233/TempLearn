using UnityEngine;

namespace LifecycleTest
{
    /// <summary>
    /// 物理系统生命周期测试脚本
    /// 需要配合 Rigidbody 和 Collider 使用
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(Collider))]
    public class PhysicsLifecycleTester : MonoBehaviour
    {
        private int instanceID;
        private static int physicsInstanceCount = 0;

        void Awake()
        {
            instanceID = ++physicsInstanceCount;
            Debug.Log($"[Physics-{instanceID}] [Awake] 物理对象创建，时间: {Time.time:F6}, 帧: {Time.frameCount}");
        }

        /// <summary>
        /// OnTriggerEnter: 触发器进入时调用
        /// - 当另一个 Collider 进入触发器区域时调用
        /// - 需要至少一个对象有 isTrigger = true
        /// </summary>
        void OnTriggerEnter(Collider other)
        {
            Debug.Log($"[Physics-{instanceID}] [OnTriggerEnter] 触发器进入，对象: {other.name}, 时间: {Time.time:F6}, 帧: {Time.frameCount}");
        }

        /// <summary>
        /// OnTriggerStay: 触发器持续接触时每帧调用
        /// - 当另一个 Collider 在触发器区域内时持续调用
        /// </summary>
        void OnTriggerStay(Collider other)
        {
            // 只在第一次打印
            if (Time.frameCount <= 3)
            {
                Debug.Log($"[Physics-{instanceID}] [OnTriggerStay] 触发器持续接触，对象: {other.name}, 时间: {Time.time:F6}, 帧: {Time.frameCount}");
            }
        }

        /// <summary>
        /// OnTriggerExit: 触发器退出时调用
        /// - 当另一个 Collider 离开触发器区域时调用
        /// </summary>
        void OnTriggerExit(Collider other)
        {
            Debug.Log($"[Physics-{instanceID}] [OnTriggerExit] 触发器退出，对象: {other.name}, 时间: {Time.time:F6}, 帧: {Time.frameCount}");
        }

        /// <summary>
        /// OnCollisionEnter: 碰撞开始时调用
        /// - 当两个非触发器的 Collider 发生碰撞时调用
        /// - 在 FixedUpdate 之后调用
        /// </summary>
        void OnCollisionEnter(Collision collision)
        {
            Debug.Log($"[Physics-{instanceID}] [OnCollisionEnter] 碰撞开始，对象: {collision.gameObject.name}, 时间: {Time.time:F6}, 帧: {Time.frameCount}");
        }

        /// <summary>
        /// OnCollisionStay: 碰撞持续时每帧调用
        /// - 当两个非触发器的 Collider 持续接触时调用
        /// - 在 FixedUpdate 之后调用
        /// </summary>
        void OnCollisionStay(Collision collision)
        {
            // 只在第一次打印
            if (Time.frameCount <= 3)
            {
                Debug.Log($"[Physics-{instanceID}] [OnCollisionStay] 碰撞持续，对象: {collision.gameObject.name}, 时间: {Time.time:F6}, 帧: {Time.frameCount}");
            }
        }

        /// <summary>
        /// OnCollisionExit: 碰撞结束时调用
        /// - 当两个非触发器的 Collider 分离时调用
        /// - 在 FixedUpdate 之后调用
        /// </summary>
        void OnCollisionExit(Collision collision)
        {
            Debug.Log($"[Physics-{instanceID}] [OnCollisionExit] 碰撞结束，对象: {collision.gameObject.name}, 时间: {Time.time:F6}, 帧: {Time.frameCount}");
        }

        /// <summary>
        /// OnControllerColliderHit: CharacterController 碰撞时调用
        /// - 当 CharacterController 与其他 Collider 碰撞时调用
        /// </summary>
        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            Debug.Log($"[Physics-{instanceID}] [OnControllerColliderHit] CharacterController 碰撞，对象: {hit.gameObject.name}, 时间: {Time.time:F6}, 帧: {Time.frameCount}");
        }
    }
}
