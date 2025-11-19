using UnityEngine;

namespace BezierCurveEditor
{
    /// <summary>
    /// 贝塞尔曲线数学计算工具类
    /// </summary>
    public static class BezierMath
    {
        /// <summary>
        /// 计算三次贝塞尔曲线在参数 t 处的值
        /// 使用公式: B(t) = (1-t)³P₀ + 3(1-t)²tP₁ + 3(1-t)t²P₂ + t³P₃
        /// </summary>
        /// <param name="p0">起点</param>
        /// <param name="p1">第一个控制点</param>
        /// <param name="p2">第二个控制点</param>
        /// <param name="p3">终点</param>
        /// <param name="t">参数值 [0, 1]</param>
        /// <returns>曲线上的点</returns>
        public static Vector2 EvaluateCubicBezier(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            t = Mathf.Clamp01(t);
            float u = 1f - t;
            float tt = t * t;
            float uu = u * u;
            float uuu = uu * u;
            float ttt = tt * t;

            Vector2 point = uuu * p0;                    // (1-t)³ * P₀
            point += 3f * uu * t * p1;                  // 3(1-t)²t * P₁
            point += 3f * u * tt * p2;                  // 3(1-t)t² * P₂
            point += ttt * p3;                          // t³ * P₃

            return point;
        }

        /// <summary>
        /// 计算三次贝塞尔曲线的切线方向
        /// 使用导数公式: B'(t) = 3(1-t)²(P₁-P₀) + 6(1-t)t(P₂-P₁) + 3t²(P₃-P₂)
        /// </summary>
        public static Vector2 EvaluateCubicBezierTangent(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            t = Mathf.Clamp01(t);
            float u = 1f - t;

            Vector2 tangent = 3f * u * u * (p1 - p0);           // 3(1-t)²(P₁-P₀)
            tangent += 6f * u * t * (p2 - p1);                  // 6(1-t)t(P₂-P₁)
            tangent += 3f * t * t * (p3 - p2);                  // 3t²(P₃-P₂)

            return tangent;
        }

        /// <summary>
        /// 使用 De Casteljau 算法递归计算贝塞尔曲线（用于理解，性能不如直接公式）
        /// </summary>
        public static Vector2 EvaluateCubicBezierDeCasteljau(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            if (t <= 0f) return p0;
            if (t >= 1f) return p3;

            Vector2 q0 = Vector2.Lerp(p0, p1, t);
            Vector2 q1 = Vector2.Lerp(p1, p2, t);
            Vector2 q2 = Vector2.Lerp(p2, p3, t);

            Vector2 r0 = Vector2.Lerp(q0, q1, t);
            Vector2 r1 = Vector2.Lerp(q1, q2, t);

            return Vector2.Lerp(r0, r1, t);
        }

        /// <summary>
        /// 计算点到贝塞尔曲线的最短距离（近似）
        /// </summary>
        public static float DistanceToCurve(Vector2 point, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, int samples = 100)
        {
            float minDist = float.MaxValue;

            for (int i = 0; i <= samples; i++)
            {
                float t = i / (float)samples;
                Vector2 curvePoint = EvaluateCubicBezier(p0, p1, p2, p3, t);
                float dist = Vector2.Distance(point, curvePoint);
                minDist = Mathf.Min(minDist, dist);
            }

            return minDist;
        }

        /// <summary>
        /// 计算贝塞尔曲线的近似长度（通过采样）
        /// </summary>
        public static float CalculateCurveLength(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, int samples = 100)
        {
            float length = 0f;
            Vector2 prevPoint = p0;

            for (int i = 1; i <= samples; i++)
            {
                float t = i / (float)samples;
                Vector2 currentPoint = EvaluateCubicBezier(p0, p1, p2, p3, t);
                length += Vector2.Distance(prevPoint, currentPoint);
                prevPoint = currentPoint;
            }

            return length;
        }

        /// <summary>
        /// 计算平滑切线（用于自动切线计算）
        /// </summary>
        public static Vector2 CalculateSmoothTangent(Vector2 prevPoint, Vector2 currentPoint, Vector2 nextPoint, float weight = 0.33f)
        {
            if (prevPoint == Vector2.zero && nextPoint == Vector2.zero)
                return Vector2.zero;

            Vector2 dir = Vector2.zero;
            if (prevPoint != Vector2.zero)
            {
                dir += (currentPoint - prevPoint).normalized;
            }
            if (nextPoint != Vector2.zero)
            {
                dir += (nextPoint - currentPoint).normalized;
            }

            dir.Normalize();
            float dist = Vector2.Distance(prevPoint != Vector2.zero ? prevPoint : currentPoint,
                                         nextPoint != Vector2.zero ? nextPoint : currentPoint) * weight;

            return dir * dist;
        }
    }
}

