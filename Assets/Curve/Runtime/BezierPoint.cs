using System;
using UnityEngine;

namespace BezierCurveEditor
{
    /// <summary>
    /// 贝塞尔曲线控制点数据结构
    /// </summary>
    [Serializable]
    public class BezierPoint
    {
        [SerializeField] private Vector2 m_Position;
        [SerializeField] private Vector2 m_ControlIn;
        [SerializeField] private Vector2 m_ControlOut;
        [SerializeField] private bool m_AutoTangents;
        [SerializeField] private bool m_MirrorTangents;

        /// <summary>
        /// 锚点位置（曲线上的点）
        /// </summary>
        public Vector2 position
        {
            get => m_Position;
            set => m_Position = value;
        }

        /// <summary>
        /// 进入控制点（相对于锚点的偏移）
        /// </summary>
        public Vector2 controlIn
        {
            get => m_ControlIn;
            set
            {
                m_ControlIn = value;
                if (m_MirrorTangents)
                {
                    m_ControlOut = -value;
                }
            }
        }

        /// <summary>
        /// 离开控制点（相对于锚点的偏移）
        /// </summary>
        public Vector2 controlOut
        {
            get => m_ControlOut;
            set
            {
                m_ControlOut = value;
                if (m_MirrorTangents)
                {
                    m_ControlIn = -value;
                }
            }
        }

        /// <summary>
        /// 是否自动计算切线
        /// </summary>
        public bool autoTangents
        {
            get => m_AutoTangents;
            set => m_AutoTangents = value;
        }

        /// <summary>
        /// 是否镜像切线（进入和离开切线对称）
        /// </summary>
        public bool mirrorTangents
        {
            get => m_MirrorTangents;
            set
            {
                m_MirrorTangents = value;
                if (value)
                {
                    m_ControlIn = -m_ControlOut;
                }
            }
        }

        /// <summary>
        /// 获取进入控制点的世界坐标
        /// </summary>
        public Vector2 GetControlInWorld()
        {
            return m_Position + m_ControlIn;
        }

        /// <summary>
        /// 获取离开控制点的世界坐标
        /// </summary>
        public Vector2 GetControlOutWorld()
        {
            return m_Position + m_ControlOut;
        }

        /// <summary>
        /// 设置进入控制点的世界坐标
        /// </summary>
        public void SetControlInWorld(Vector2 worldPos)
        {
            controlIn = worldPos - m_Position;
        }

        /// <summary>
        /// 设置离开控制点的世界坐标
        /// </summary>
        public void SetControlOutWorld(Vector2 worldPos)
        {
            controlOut = worldPos - m_Position;
        }

        public BezierPoint()
        {
            m_Position = Vector2.zero;
            m_ControlIn = Vector2.zero;
            m_ControlOut = Vector2.zero;
            m_AutoTangents = false;
            m_MirrorTangents = false;
        }

        public BezierPoint(Vector2 position)
        {
            m_Position = position;
            m_ControlIn = Vector2.zero;
            m_ControlOut = Vector2.zero;
            m_AutoTangents = false;
            m_MirrorTangents = false;
        }

        public BezierPoint(Vector2 position, Vector2 controlIn, Vector2 controlOut)
        {
            m_Position = position;
            m_ControlIn = controlIn;
            m_ControlOut = controlOut;
            m_AutoTangents = false;
            m_MirrorTangents = false;
        }

        /// <summary>
        /// 复制构造函数
        /// </summary>
        public BezierPoint(BezierPoint other)
        {
            m_Position = other.m_Position;
            m_ControlIn = other.m_ControlIn;
            m_ControlOut = other.m_ControlOut;
            m_AutoTangents = other.m_AutoTangents;
            m_MirrorTangents = other.m_MirrorTangents;
        }
    }
}

