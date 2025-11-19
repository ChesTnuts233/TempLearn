using UnityEngine;
using UnityEditor;

namespace BezierCurveEditor
{
    /// <summary>
    /// 可缩放网格绘制系统
    /// 基于曲线空间坐标，支持缩放和平移操作
    /// </summary>
    public static class BezierCurveDrawer
    {
        // 网格基础步长（曲线空间单位），决定网格密度
        private const float BASE_GRID_STEP = 0.05f;

        // 坐标轴（X=0 和 Y=0）的透明度，值越大越明显
        private const float ZERO_LINE_ALPHA = 0.6f;

        // 网格线裁剪容差（像素），用于判断网格线是否在可见区域内
        private const float GRID_CLIP_TOLERANCE = 1f;

        // 网格密度优化参数
        private const float MIN_GRID_STEP = 0.005f;     // 最小网格步长（缩放很大时，更精细）
        private const float MAX_GRID_STEP = 0.5f;       // 最大网格步长（缩放很小时）
        private const int MAX_GRID_LINES = 50;          // 每个方向最大网格线数量
        private const int MIN_GRID_LINES = 5;           // 每个方向最小网格线数量（避免过疏）

        /// <summary>
        /// 将曲线空间坐标转换为屏幕坐标
        /// </summary>
        /// <param name="curvePoint">曲线空间中的点坐标</param>
        /// <param name="screenRect">屏幕绘制区域</param>
        /// <param name="settings">视图设置（包含 viewBounds、zoom、panOffset）</param>
        /// <returns>屏幕坐标（像素）</returns>
        public static Vector2 CurveToScreen(Vector2 curvePoint, Rect screenRect, BezierCurveSettings settings)
        {
            // 步骤1：归一化到视图空间 [0,1]
            Vector2 viewPoint = (curvePoint - settings.viewBounds.min) / settings.viewBounds.size;

            // 步骤2：应用缩放（以中心为基准）
            viewPoint = (viewPoint - Vector2.one * 0.5f) * settings.zoom + Vector2.one * 0.5f;

            // 步骤3：应用平移偏移
            viewPoint += settings.panOffset;

            // 步骤4：映射到屏幕坐标（Y轴翻转，因为屏幕Y向下）
            float x = Mathf.Lerp(screenRect.xMin, screenRect.xMax, viewPoint.x);
            float y = Mathf.Lerp(screenRect.yMax, screenRect.yMin, viewPoint.y);
            return new Vector2(x, y);
        }

        /// <summary>
        /// 将屏幕坐标转换为曲线空间坐标
        /// </summary>
        /// <param name="screenPoint">屏幕坐标（像素）</param>
        /// <param name="screenRect">屏幕绘制区域</param>
        /// <param name="settings">视图设置（包含 viewBounds、zoom、panOffset）</param>
        /// <returns>曲线空间坐标</returns>
        public static Vector2 ScreenToCurve(Vector2 screenPoint, Rect screenRect, BezierCurveSettings settings)
        {
            // 步骤1：转换为视图空间 [0,1]（Y轴翻转）
            float viewX = Mathf.InverseLerp(screenRect.xMin, screenRect.xMax, screenPoint.x);
            float viewY = Mathf.InverseLerp(screenRect.yMax, screenRect.yMin, screenPoint.y);
            Vector2 viewPoint = new Vector2(viewX, viewY);

            // 步骤2：应用平移偏移的逆变换
            viewPoint -= settings.panOffset;

            // 步骤3：应用缩放的逆变换
            viewPoint = (viewPoint - Vector2.one * 0.5f) / settings.zoom + Vector2.one * 0.5f;

            // 步骤4：映射到曲线空间
            return viewPoint * settings.viewBounds.size + settings.viewBounds.min;
        }

        /// <summary>
        /// 绘制可缩放网格背景
        /// </summary>
        /// <param name="area">屏幕绘制区域</param>
        /// <param name="settings">网格和视图设置</param>
        public static void DrawGrid(Rect area, BezierCurveSettings settings)
        {
            // 只在显示网格且为 Repaint 事件时绘制（性能优化）
            if (!settings.showGrid || Event.current.type != EventType.Repaint)
                return;

            DrawGridLines(area, settings);
        }

        /// <summary>
        /// 绘制网格线（包括主网格线和坐标轴）
        /// </summary>
        private static void DrawGridLines(Rect area, BezierCurveSettings settings)
        {
            // 计算可见区域在曲线空间中的范围
            Rect visibleBounds = GetVisibleBounds(area, settings);

            // 计算网格步长（确保网格在屏幕上显示为正方形）
            Vector2 gridSteps = CalculateGridSteps(area, settings);

            // 将可见区域边界对齐到网格步长的整数倍
            Rect gridBounds = AlignToGrid(visibleBounds, gridSteps);

            // 绘制主网格线
            Handles.color = settings.gridColor;
            DrawVerticalLines(area, settings, gridBounds, gridSteps.x);
            DrawHorizontalLines(area, settings, gridBounds, gridSteps.y);

            // 绘制坐标轴（X=0 和 Y=0）
            DrawZeroLines(area, settings, visibleBounds, gridSteps);
        }

        /// <summary>
        /// 计算屏幕区域在曲线空间中的可见范围
        /// 考虑缩放和平移后，屏幕四个角在曲线空间中的位置
        /// </summary>
        private static Rect GetVisibleBounds(Rect area, BezierCurveSettings settings)
        {
            // 将屏幕区域的四个角转换为曲线空间坐标
            Vector2 topLeft = ScreenToCurve(new Vector2(area.xMin, area.yMin), area, settings);
            Vector2 topRight = ScreenToCurve(new Vector2(area.xMax, area.yMin), area, settings);
            Vector2 bottomLeft = ScreenToCurve(new Vector2(area.xMin, area.yMax), area, settings);
            Vector2 bottomRight = ScreenToCurve(new Vector2(area.xMax, area.yMax), area, settings);

            // 计算可见区域的边界（考虑旋转和缩放可能导致角点位置变化）
            float minX = Mathf.Min(topLeft.x, topRight.x, bottomLeft.x, bottomRight.x);
            float maxX = Mathf.Max(topLeft.x, topRight.x, bottomLeft.x, bottomRight.x);
            float minY = Mathf.Min(topLeft.y, topRight.y, bottomLeft.y, bottomRight.y);
            float maxY = Mathf.Max(topLeft.y, topRight.y, bottomLeft.y, bottomRight.y);

            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }

        /// <summary>
        /// 计算网格步长，确保网格在屏幕上显示为正方形（1:1）
        /// 根据缩放级别动态调整网格密度，避免缩放很小时网格过于密集
        /// </summary>
        /// <param name="area">屏幕绘制区域</param>
        /// <param name="settings">视图设置</param>
        /// <returns>X 和 Y 方向的网格步长（曲线空间单位）</returns>
        private static Vector2 CalculateGridSteps(Rect area, BezierCurveSettings settings)
        {
            // 计算视图和屏幕的宽高比
            float viewAspectRatio = settings.viewBounds.width / settings.viewBounds.height;
            float screenAspectRatio = area.width / area.height;

            // 计算步长比例：如果视图比屏幕更宽，需要调整垂直步长
            float stepRatio = viewAspectRatio / screenAspectRatio;

            // 根据缩放级别动态调整基础网格步长
            // 缩放越小（zoom < 1），可见区域越大，需要增大网格步长
            // 缩放越大（zoom > 1），可见区域越小，可以减小网格步长
            float adaptiveStep = CalculateAdaptiveGridStep(settings.zoom, area, settings);

            // 水平步长使用自适应值，垂直步长根据比例调整
            float stepX = adaptiveStep;
            float stepY = adaptiveStep / stepRatio;

            // 限制网格步长范围，避免过小或过大
            stepX = Mathf.Clamp(stepX, MIN_GRID_STEP, MAX_GRID_STEP);
            stepY = Mathf.Clamp(stepY, MIN_GRID_STEP, MAX_GRID_STEP);

            return new Vector2(stepX, stepY);
        }

        /// <summary>
        /// 根据缩放级别和可见区域大小计算自适应的网格步长
        /// 使用对数尺度，使网格在不同缩放级别下保持合理的密度
        /// </summary>
        private static float CalculateAdaptiveGridStep(float zoom, Rect area, BezierCurveSettings settings)
        {
            // 计算可见区域在曲线空间中的大小（近似值）
            // 可见区域大小 ≈ viewBounds.size / zoom
            float visibleSize = Mathf.Max(settings.viewBounds.width, settings.viewBounds.height) / Mathf.Max(zoom, 0.01f);

            // 根据可见区域大小和网格线数量要求计算合适的步长
            // 优先考虑网格线数量，确保在合理范围内
            float stepForMaxLines = visibleSize / MAX_GRID_LINES;  // 最大网格线数量对应的步长
            float stepForMinLines = visibleSize / MIN_GRID_LINES;  // 最小网格线数量对应的步长

            // 使用对数尺度计算基础步长
            // 当可见区域很大时（缩放很小），步长应该增大
            // 当可见区域很小时（缩放很大），步长应该减小
            float logScale = Mathf.Log10(Mathf.Max(visibleSize, 0.001f));

            // 将对数尺度映射到网格步长范围
            // 扩大范围以支持更大的缩放范围：log10(0.001) 到 log10(10) 映射到 [0, 1]
            // log10(0.001) = -3, log10(10) = 1, 范围 = 4
            float normalizedLog = (logScale + 3f) / 4f; // 将 [-3, 1] 映射到 [0, 1]
            normalizedLog = Mathf.Clamp01(normalizedLog);
            float adaptiveStep = Mathf.Lerp(MIN_GRID_STEP, MAX_GRID_STEP, normalizedLog);

            // 结合网格线数量限制和自适应步长
            // 取两者中的较大值，确保网格线数量在合理范围内
            adaptiveStep = Mathf.Max(adaptiveStep, stepForMaxLines);

            // 同时限制最大步长，避免网格过疏
            adaptiveStep = Mathf.Min(adaptiveStep, stepForMinLines);

            // 将步长对齐到 BASE_GRID_STEP 的整数倍，使网格更整齐
            // 但允许更小的步长（当缩放很大时）
            float stepMultiplier = Mathf.Round(adaptiveStep / BASE_GRID_STEP);
            float alignedStep = BASE_GRID_STEP * stepMultiplier;

            // 如果对齐后的步长太大，使用更小的步长
            if (alignedStep > adaptiveStep * 1.5f && stepMultiplier > 0.5f)
            {
                stepMultiplier = Mathf.Max(0.5f, stepMultiplier - 1f);
                alignedStep = BASE_GRID_STEP * stepMultiplier;
            }

            // 确保步长不小于最小值
            adaptiveStep = Mathf.Max(alignedStep, MIN_GRID_STEP);

            return adaptiveStep;
        }

        /// <summary>
        /// 将边界对齐到网格步长的整数倍
        /// 确保网格线从整数倍步长位置开始绘制
        /// </summary>
        /// <param name="bounds">可见区域边界（曲线空间）</param>
        /// <param name="gridSteps">网格步长</param>
        /// <returns>对齐后的网格绘制边界</returns>
        private static Rect AlignToGrid(Rect bounds, Vector2 gridSteps)
        {
            // 向下取整到最近的网格线位置
            float startX = Mathf.Floor(bounds.xMin / gridSteps.x) * gridSteps.x;
            float startY = Mathf.Floor(bounds.yMin / gridSteps.y) * gridSteps.y;

            // 向上取整到最近的网格线位置
            float endX = Mathf.Ceil(bounds.xMax / gridSteps.x) * gridSteps.x;
            float endY = Mathf.Ceil(bounds.yMax / gridSteps.y) * gridSteps.y;

            return new Rect(startX, startY, endX - startX, endY - startY);
        }

        /// <summary>
        /// 绘制垂直网格线（平行于 Y 轴）
        /// </summary>
        /// <param name="area">屏幕绘制区域</param>
        /// <param name="settings">视图设置</param>
        /// <param name="gridBounds">网格绘制边界（曲线空间）</param>
        /// <param name="stepX">X 方向的网格步长</param>
        private static void DrawVerticalLines(Rect area, BezierCurveSettings settings, Rect gridBounds, float stepX)
        {
            // 按步长遍历所有垂直网格线
            for (float curveX = gridBounds.xMin; curveX <= gridBounds.xMax; curveX += stepX)
            {
                // 将曲线空间坐标转换为屏幕坐标
                Vector2 screenStart = CurveToScreen(new Vector2(curveX, gridBounds.yMin), area, settings);
                Vector2 screenEnd = CurveToScreen(new Vector2(curveX, gridBounds.yMax), area, settings);

                // 只绘制在可见区域内的线（添加容差以避免边界问题）
                if (screenStart.x >= area.xMin - GRID_CLIP_TOLERANCE && screenStart.x <= area.xMax + GRID_CLIP_TOLERANCE)
                {
                    Handles.DrawLine(screenStart, screenEnd);
                }
            }
        }

        /// <summary>
        /// 绘制水平网格线（平行于 X 轴）
        /// </summary>
        /// <param name="area">屏幕绘制区域</param>
        /// <param name="settings">视图设置</param>
        /// <param name="gridBounds">网格绘制边界（曲线空间）</param>
        /// <param name="stepY">Y 方向的网格步长</param>
        private static void DrawHorizontalLines(Rect area, BezierCurveSettings settings, Rect gridBounds, float stepY)
        {
            // 按步长遍历所有水平网格线
            for (float curveY = gridBounds.yMin; curveY <= gridBounds.yMax; curveY += stepY)
            {
                // 将曲线空间坐标转换为屏幕坐标
                Vector2 screenStart = CurveToScreen(new Vector2(gridBounds.xMin, curveY), area, settings);
                Vector2 screenEnd = CurveToScreen(new Vector2(gridBounds.xMax, curveY), area, settings);

                // 只绘制在可见区域内的线（添加容差以避免边界问题）
                if (screenStart.y >= area.yMin - GRID_CLIP_TOLERANCE && screenStart.y <= area.yMax + GRID_CLIP_TOLERANCE)
                {
                    Handles.DrawLine(screenStart, screenEnd);
                }
            }
        }

        /// <summary>
        /// 绘制坐标轴（X=0 和 Y=0 的线）
        /// 如果坐标轴在可见区域内，使用高亮颜色绘制
        /// </summary>
        /// <param name="area">屏幕绘制区域</param>
        /// <param name="settings">视图设置</param>
        /// <param name="visibleBounds">可见区域边界（曲线空间）</param>
        /// <param name="gridSteps">网格步长，用于对齐坐标轴到网格</param>
        private static void DrawZeroLines(Rect area, BezierCurveSettings settings, Rect visibleBounds, Vector2 gridSteps)
        {
            // 使用半透明白色绘制坐标轴，使其更明显
            Color zeroLineColor = new Color(1f, 1f, 1f, ZERO_LINE_ALPHA);
            Color originalColor = Handles.color;

            // 绘制 Y 轴（X=0 的垂直线）
            if (visibleBounds.xMin <= 0f && 0f <= visibleBounds.xMax)
            {
                // 对齐到最近的网格线，确保坐标轴与网格对齐
                float alignedX = Mathf.Round(0f / gridSteps.x) * gridSteps.x;
                Vector2 screenStart = CurveToScreen(new Vector2(alignedX, visibleBounds.yMin), area, settings);
                Vector2 screenEnd = CurveToScreen(new Vector2(alignedX, visibleBounds.yMax), area, settings);

                Handles.color = zeroLineColor;
                Handles.DrawLine(screenStart, screenEnd);
            }

            // 绘制 X 轴（Y=0 的水平线）
            if (visibleBounds.yMin <= 0f && 0f <= visibleBounds.yMax)
            {
                // 对齐到最近的网格线，确保坐标轴与网格对齐
                float alignedY = Mathf.Round(0f / gridSteps.y) * gridSteps.y;
                Vector2 screenStart = CurveToScreen(new Vector2(visibleBounds.xMin, alignedY), area, settings);
                Vector2 screenEnd = CurveToScreen(new Vector2(visibleBounds.xMax, alignedY), area, settings);

                Handles.color = zeroLineColor;
                Handles.DrawLine(screenStart, screenEnd);
            }

            // 恢复原始颜色
            Handles.color = originalColor;
        }
    }
}

