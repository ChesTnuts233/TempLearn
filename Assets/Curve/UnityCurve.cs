using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BezierCurveEditor;

/// <summary>
/// 贝塞尔曲线使用样例
/// 展示如何在 Unity 中使用自定义的贝塞尔曲线编辑器
/// </summary>
public class UnityCurve : MonoBehaviour
{
    [Header("Unity 原生曲线（参考）")]
    public AnimationCurve curve;

    [Header("自定义贝塞尔曲线")]
    [Tooltip("在 Inspector 中点击 'Open Bezier Curve Editor' 按钮来编辑曲线")]
    public BezierCurve bezierCurve = new BezierCurve();

    [Header("运行时参数")]
    [Tooltip("用于求值曲线的参数 t，范围 [0, 1]")]
    [Range(0f, 1f)]
    public float curveParameter = 0f;

    [Header("可视化设置")]
    [Tooltip("是否在 Scene 视图中绘制曲线")]
    public bool drawInSceneView = true;

    [Tooltip("曲线绘制分辨率")]
    public int drawResolution = 50;

    [Tooltip("曲线颜色")]
    public Color curveColor = Color.green;

    [Tooltip("控制点颜色")]
    public Color pointColor = Color.yellow;

    [Header("应用示例")]
    [Tooltip("使用曲线控制物体移动")]
    public bool useForMovement = false;

    [Tooltip("移动目标（如果启用 useForMovement）")]
    public Transform moveTarget;

    [Tooltip("移动速度")]
    public float moveSpeed = 1f;

    private float m_CurrentTime = 0f;

    private void Start()
    {
        // 初始化贝塞尔曲线（如果没有设置）
        if (bezierCurve == null || bezierCurve.pointCount == 0)
        {
            bezierCurve = BezierCurve.CreateSmooth(
                new Vector2(0f, 0f),  // 起点
                new Vector2(1f, 1f)   // 终点
            );
        }

        // 如果启用移动，初始化目标位置
        if (useForMovement && moveTarget != null)
        {
            Vector2 startPos = bezierCurve.Evaluate(0f);
            moveTarget.position = new Vector3(startPos.x, startPos.y, moveTarget.position.z);
        }
    }

    private void Update()
    {
        // 示例 1: 使用曲线参数控制物体移动
        if (useForMovement && moveTarget != null)
        {
            m_CurrentTime += Time.deltaTime * moveSpeed;
            m_CurrentTime = Mathf.Repeat(m_CurrentTime, 1f); // 循环

            Vector2 curveValue = bezierCurve.Evaluate(m_CurrentTime);
            moveTarget.position = new Vector3(curveValue.x, curveValue.y, moveTarget.position.z);
        }

        // 示例 2: 使用曲线参数控制其他属性
        // float scale = bezierCurve.Evaluate(curveParameter).y;
        // transform.localScale = Vector3.one * scale;
    }

    private void OnDrawGizmos()
    {
        if (!drawInSceneView || bezierCurve == null || bezierCurve.pointCount < 2)
            return;

        // 绘制贝塞尔曲线
        DrawBezierCurveGizmo();

        // 绘制控制点
        DrawControlPointsGizmo();
    }

    /// <summary>
    /// 在 Scene 视图中绘制贝塞尔曲线
    /// </summary>
    private void DrawBezierCurveGizmo()
    {
        Gizmos.color = curveColor;
        int segmentCount = bezierCurve.loop ? bezierCurve.pointCount : bezierCurve.pointCount - 1;

        for (int seg = 0; seg < segmentCount; seg++)
        {
            BezierPoint p0 = bezierCurve.GetPoint(seg);
            int nextIndex = bezierCurve.loop ? (seg + 1) % bezierCurve.pointCount : seg + 1;
            BezierPoint p1 = bezierCurve.GetPoint(nextIndex);

            if (p0 == null || p1 == null)
                continue;

            // 采样曲线段
            Vector3 prevPoint = Vector3.zero;
            for (int i = 0; i <= drawResolution; i++)
            {
                float t = i / (float)drawResolution;
                Vector2 curvePoint = BezierMath.EvaluateCubicBezier(
                    p0.position,
                    p0.GetControlOutWorld(),
                    p1.GetControlInWorld(),
                    p1.position,
                    t
                );

                Vector3 worldPoint = new Vector3(curvePoint.x, curvePoint.y, transform.position.z);

                if (i > 0)
                {
                    Gizmos.DrawLine(prevPoint, worldPoint);
                }

                prevPoint = worldPoint;
            }
        }
    }

    /// <summary>
    /// 在 Scene 视图中绘制控制点
    /// </summary>
    private void DrawControlPointsGizmo()
    {
        Gizmos.color = pointColor;

        for (int i = 0; i < bezierCurve.pointCount; i++)
        {
            BezierPoint point = bezierCurve.GetPoint(i);
            if (point == null)
                continue;

            Vector3 worldPos = new Vector3(point.position.x, point.position.y, transform.position.z);

            // 绘制锚点
            Gizmos.DrawSphere(worldPos, 0.1f);

            // 绘制切线
            Gizmos.color = Color.cyan;
            if (point.controlIn != Vector2.zero)
            {
                Vector3 controlInWorld = new Vector3(point.GetControlInWorld().x, point.GetControlInWorld().y, transform.position.z);
                Gizmos.DrawLine(worldPos, controlInWorld);
                Gizmos.DrawWireSphere(controlInWorld, 0.05f);
            }

            if (point.controlOut != Vector2.zero)
            {
                Vector3 controlOutWorld = new Vector3(point.GetControlOutWorld().x, point.GetControlOutWorld().y, transform.position.z);
                Gizmos.DrawLine(worldPos, controlOutWorld);
                Gizmos.DrawWireSphere(controlOutWorld, 0.05f);
            }

            Gizmos.color = pointColor;
        }
    }

    /// <summary>
    /// 示例：使用曲线控制物体沿路径移动
    /// </summary>
    [ContextMenu("示例：沿曲线移动")]
    public void ExampleMoveAlongCurve()
    {
        if (moveTarget == null)
        {
            Debug.LogWarning("请先设置 moveTarget");
            return;
        }

        StartCoroutine(MoveAlongCurveCoroutine());
    }

    private IEnumerator MoveAlongCurveCoroutine()
    {
        float duration = 5f; // 移动持续时间
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            Vector2 curveValue = bezierCurve.Evaluate(t);
            moveTarget.position = new Vector3(curveValue.x, curveValue.y, moveTarget.position.z);

            yield return null;
        }

        // 确保到达终点
        Vector2 endValue = bezierCurve.Evaluate(1f);
        moveTarget.position = new Vector3(endValue.x, endValue.y, moveTarget.position.z);
    }

    /// <summary>
    /// 示例：使用曲线控制缩放动画
    /// </summary>
    [ContextMenu("示例：缩放动画")]
    public void ExampleScaleAnimation()
    {
        StartCoroutine(ScaleAnimationCoroutine());
    }

    private IEnumerator ScaleAnimationCoroutine()
    {
        float duration = 2f;
        float elapsed = 0f;
        Vector3 originalScale = transform.localScale;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // 使用曲线的 Y 值作为缩放因子
            float scaleFactor = bezierCurve.Evaluate(t).y;
            transform.localScale = originalScale * scaleFactor;

            yield return null;
        }

        transform.localScale = originalScale;
    }

    /// <summary>
    /// 示例：使用曲线控制颜色过渡
    /// </summary>
    [ContextMenu("示例：颜色过渡")]
    public void ExampleColorTransition()
    {
        StartCoroutine(ColorTransitionCoroutine());
    }

    private IEnumerator ColorTransitionCoroutine()
    {
        float duration = 3f;
        float elapsed = 0f;
        Color startColor = Color.red;
        Color endColor = Color.blue;

        Renderer renderer = GetComponent<Renderer>();
        if (renderer == null)
        {
            Debug.LogWarning("需要 Renderer 组件");
            yield break;
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // 使用曲线的 Y 值作为插值因子
            float lerpFactor = bezierCurve.Evaluate(t).y;
            renderer.material.color = Color.Lerp(startColor, endColor, lerpFactor);

            yield return null;
        }

        renderer.material.color = endColor;
    }

    /// <summary>
    /// 在 Inspector 中显示当前曲线值
    /// </summary>
    private void OnValidate()
    {
        if (bezierCurve != null && bezierCurve.pointCount > 0)
        {
            Vector2 currentValue = bezierCurve.Evaluate(curveParameter);
            // 可以在 Inspector 中看到实时更新的值
        }
    }

    /// <summary>
    /// 获取曲线在指定参数处的值（公共接口）
    /// </summary>
    public Vector2 Evaluate(float t)
    {
        if (bezierCurve == null || bezierCurve.pointCount == 0)
            return Vector2.zero;

        return bezierCurve.Evaluate(t);
    }

    /// <summary>
    /// 获取曲线的 Y 值（常用于一维动画）
    /// </summary>
    public float EvaluateY(float t)
    {
        return Evaluate(t).y;
    }

    /// <summary>
    /// 获取曲线的 X 值
    /// </summary>
    public float EvaluateX(float t)
    {
        return Evaluate(t).x;
    }
}
