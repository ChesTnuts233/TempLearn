using System;
using System.Collections.Generic;
using UnityEngine;

namespace BezierCurveEditor
{
    /// <summary>
    /// 贝塞尔曲线数据类
    /// </summary>
    [Serializable]
    public class BezierCurve
    {
        [SerializeField] private List<BezierPoint> m_Points = new List<BezierPoint>();
        [SerializeField] private bool m_Loop;
        [SerializeField] private int m_Resolution = 100;

        /// <summary>
        /// 控制点列表
        /// </summary>
        public List<BezierPoint> points => m_Points;

        /// <summary>
        /// 控制点数量
        /// </summary>
        public int pointCount => m_Points.Count;

        /// <summary>
        /// 是否循环（首尾相连）
        /// </summary>
        public bool loop
        {
            get => m_Loop;
            set => m_Loop = value;
        }

        /// <summary>
        /// 曲线采样分辨率（用于绘制和求值）
        /// </summary>
        public int resolution
        {
            get => m_Resolution;
            set => m_Resolution = Mathf.Max(2, value);
        }

        /// <summary>
        /// 获取指定索引的控制点
        /// </summary>
        public BezierPoint GetPoint(int index)
        {
            if (index < 0 || index >= m_Points.Count)
                return null;
            return m_Points[index];
        }

        /// <summary>
        /// 在指定位置添加控制点
        /// </summary>
        public int AddPoint(Vector2 position)
        {
            var point = new BezierPoint(position);
            m_Points.Add(point);
            return m_Points.Count - 1;
        }

        /// <summary>
        /// 在指定索引插入控制点
        /// </summary>
        public void InsertPoint(int index, Vector2 position)
        {
            if (index < 0 || index > m_Points.Count)
                return;
            var point = new BezierPoint(position);
            m_Points.Insert(index, point);
        }

        /// <summary>
        /// 移除指定索引的控制点
        /// </summary>
        public void RemovePoint(int index)
        {
            if (index < 0 || index >= m_Points.Count)
                return;
            m_Points.RemoveAt(index);
        }

        /// <summary>
        /// 移除所有控制点
        /// </summary>
        public void ClearPoints()
        {
            m_Points.Clear();
        }

        /// <summary>
        /// 移动控制点
        /// </summary>
        public void MovePoint(int index, Vector2 newPosition)
        {
            if (index < 0 || index >= m_Points.Count)
                return;
            m_Points[index].position = newPosition;
        }

        /// <summary>
        /// 计算曲线在参数 t 处的值
        /// </summary>
        /// <param name="t">参数值，范围 [0, 1]</param>
        /// <returns>曲线上的点</returns>
        public Vector2 Evaluate(float t)
        {
            if (m_Points.Count == 0)
                return Vector2.zero;

            if (m_Points.Count == 1)
                return m_Points[0].position;

            // 限制 t 在 [0, 1] 范围内
            t = Mathf.Clamp01(t);

            // 计算分段索引
            int segmentCount = m_Loop ? m_Points.Count : m_Points.Count - 1;
            if (segmentCount <= 0)
                return m_Points[0].position;

            float segmentT = t * segmentCount;
            int segmentIndex = Mathf.FloorToInt(segmentT);
            float localT = segmentT - segmentIndex;

            // 处理循环和边界
            if (m_Loop)
            {
                segmentIndex %= m_Points.Count;
            }
            else
            {
                segmentIndex = Mathf.Clamp(segmentIndex, 0, segmentCount - 1);
                // 如果是最后一段，确保 localT 不超过 1
                if (segmentIndex == segmentCount - 1)
                    localT = Mathf.Clamp01(localT);
            }

            // 获取当前段的两个控制点
            BezierPoint p0 = m_Points[segmentIndex];
            int nextIndex = (segmentIndex + 1) % m_Points.Count;
            BezierPoint p1 = m_Points[nextIndex];

            // 计算贝塞尔曲线的四个控制点
            Vector2 startPoint = p0.position;
            Vector2 startControl = p0.GetControlOutWorld();
            Vector2 endControl = p1.GetControlInWorld();
            Vector2 endPoint = p1.position;

            // 使用三次贝塞尔曲线公式计算
            return BezierMath.EvaluateCubicBezier(startPoint, startControl, endControl, endPoint, localT);
        }

        /// <summary>
        /// 获取曲线的边界框
        /// </summary>
        public Rect GetBounds()
        {
            if (m_Points.Count == 0)
                return new Rect(0, 0, 1, 1);

            Vector2 min = m_Points[0].position;
            Vector2 max = m_Points[0].position;

            // 检查所有控制点
            foreach (var point in m_Points)
            {
                min = Vector2.Min(min, point.position);
                max = Vector2.Max(max, point.position);
                min = Vector2.Min(min, point.GetControlInWorld());
                max = Vector2.Max(max, point.GetControlInWorld());
                min = Vector2.Min(min, point.GetControlOutWorld());
                max = Vector2.Max(max, point.GetControlOutWorld());
            }

            // 添加一些边距
            Vector2 size = max - min;
            if (size.x < 0.1f) size.x = 0.1f;
            if (size.y < 0.1f) size.y = 0.1f;

            Vector2 center = (min + max) * 0.5f;
            Vector2 margin = size * 0.1f;

            return new Rect(center - size * 0.5f - margin, size + margin * 2f);
        }

        /// <summary>
        /// 创建默认曲线（线性）
        /// </summary>
        public static BezierCurve CreateLinear(Vector2 start, Vector2 end)
        {
            var curve = new BezierCurve();
            curve.AddPoint(start);
            curve.AddPoint(end);
            return curve;
        }

        /// <summary>
        /// 创建默认曲线（平滑）
        /// </summary>
        public static BezierCurve CreateSmooth(Vector2 start, Vector2 end)
        {
            var curve = new BezierCurve();
            var p0 = new BezierPoint(start);
            var p1 = new BezierPoint(end);

            // 设置平滑的切线
            Vector2 dir = (end - start).normalized;
            float dist = Vector2.Distance(start, end) * 0.33f;
            p0.controlOut = dir * dist;
            p1.controlIn = -dir * dist;

            curve.points.Add(p0);
            curve.points.Add(p1);
            return curve;
        }
    }
}

