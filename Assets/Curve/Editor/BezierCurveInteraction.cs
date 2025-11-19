using UnityEngine;
using UnityEditor;

namespace BezierCurveEditor
{
    /// <summary>
    /// 交互处理系统
    /// </summary>
    public class BezierCurveInteraction
    {
        public enum InteractionState
        {
            Idle,
            HoverPoint,
            HoverControlIn,
            HoverControlOut,
            DraggingPoint,
            DraggingControlIn,
            DraggingControlOut,
            Panning,
            Selecting
        }

        private InteractionState m_State = InteractionState.Idle;
        private int m_HoveredPointIndex = -1;
        private int m_SelectedPointIndex = -1;
        private bool m_IsHoveringControlIn = false;
        private bool m_IsHoveringControlOut = false;
        private Vector2 m_DragStartPosition;
        private Vector2 m_DragStartMousePosition;

        public InteractionState state => m_State;
        public int selectedPointIndex => m_SelectedPointIndex;
        public int hoveredPointIndex => m_HoveredPointIndex;

        /// <summary>
        /// 处理输入事件
        /// </summary>
        public bool HandleInput(BezierCurve curve, Rect curveArea, BezierCurveSettings settings, Event currentEvent)
        {
            bool changed = false;
            Vector2 mousePos = currentEvent.mousePosition;

            switch (currentEvent.type)
            {
                case EventType.MouseDown:
                    changed = HandleMouseDown(curve, curveArea, settings, mousePos, currentEvent.button);
                    break;

                case EventType.MouseDrag:
                    changed = HandleMouseDrag(curve, curveArea, settings, mousePos);
                    break;

                case EventType.MouseUp:
                    changed = HandleMouseUp();
                    break;

                case EventType.MouseMove:
                    HandleMouseMove(curve, curveArea, settings, mousePos);
                    break;
            }

            return changed;
        }

        private bool HandleMouseDown(BezierCurve curve, Rect curveArea, BezierCurveSettings settings, Vector2 mousePos, int button)
        {
            if (button == 0) // 左键
            {
                // 检查是否点击了控制点
                int pointIndex = GetPointAtPosition(curve, curveArea, settings, mousePos, out bool isControlIn, out bool isControlOut);

                if (pointIndex >= 0)
                {
                    m_SelectedPointIndex = pointIndex;
                    m_HoveredPointIndex = pointIndex;
                    m_IsHoveringControlIn = isControlIn;
                    m_IsHoveringControlOut = isControlOut;

                    if (isControlIn)
                    {
                        m_State = InteractionState.DraggingControlIn;
                    }
                    else if (isControlOut)
                    {
                        m_State = InteractionState.DraggingControlOut;
                    }
                    else
                    {
                        m_State = InteractionState.DraggingPoint;
                    }

                    m_DragStartPosition = curve.GetPoint(pointIndex).position;
                    m_DragStartMousePosition = mousePos;
                    return true;
                }
                else
                {
                    // 开始平移
                    if (Event.current.alt || Event.current.button == 2)
                    {
                        m_State = InteractionState.Panning;
                        m_DragStartMousePosition = mousePos;
                        return true;
                    }
                }
            }
            else if (button == 1) // 右键
            {
                // 可以添加右键菜单
            }

            return false;
        }

        private bool HandleMouseDrag(BezierCurve curve, Rect curveArea, BezierCurveSettings settings, Vector2 mousePos)
        {
            if (m_State == InteractionState.DraggingPoint && m_SelectedPointIndex >= 0)
            {
                Vector2 curvePos = BezierCurveDrawer.ScreenToCurve(mousePos, curveArea, settings);

                if (settings.snapToGrid)
                {
                    curvePos = SnapToGrid(curvePos, settings);
                }

                curve.MovePoint(m_SelectedPointIndex, curvePos);
                return true;
            }
            else if (m_State == InteractionState.DraggingControlIn && m_SelectedPointIndex >= 0)
            {
                BezierPoint point = curve.GetPoint(m_SelectedPointIndex);
                Vector2 curvePos = BezierCurveDrawer.ScreenToCurve(mousePos, curveArea, settings);
                point.SetControlInWorld(curvePos);
                return true;
            }
            else if (m_State == InteractionState.DraggingControlOut && m_SelectedPointIndex >= 0)
            {
                BezierPoint point = curve.GetPoint(m_SelectedPointIndex);
                Vector2 curvePos = BezierCurveDrawer.ScreenToCurve(mousePos, curveArea, settings);
                point.SetControlOutWorld(curvePos);
                return true;
            }
            else if (m_State == InteractionState.Panning)
            {
                Vector2 delta = mousePos - m_DragStartMousePosition;
                Vector2 curveDelta = BezierCurveDrawer.ScreenToCurve(delta, curveArea, settings) -
                                    BezierCurveDrawer.ScreenToCurve(Vector2.zero, curveArea, settings);
                settings.panOffset -= curveDelta;
                m_DragStartMousePosition = mousePos;
                return true;
            }

            return false;
        }

        private bool HandleMouseUp()
        {
            if (m_State == InteractionState.DraggingPoint ||
                m_State == InteractionState.DraggingControlIn ||
                m_State == InteractionState.DraggingControlOut ||
                m_State == InteractionState.Panning)
            {
                m_State = InteractionState.Idle;
                return true;
            }

            return false;
        }

        private void HandleMouseMove(BezierCurve curve, Rect curveArea, BezierCurveSettings settings, Vector2 mousePos)
        {
            if (m_State == InteractionState.Idle)
            {
                int pointIndex = GetPointAtPosition(curve, curveArea, settings, mousePos, out bool isControlIn, out bool isControlOut);

                if (pointIndex >= 0)
                {
                    m_HoveredPointIndex = pointIndex;
                    m_IsHoveringControlIn = isControlIn;
                    m_IsHoveringControlOut = isControlOut;

                    if (isControlIn)
                        m_State = InteractionState.HoverControlIn;
                    else if (isControlOut)
                        m_State = InteractionState.HoverControlOut;
                    else
                        m_State = InteractionState.HoverPoint;
                }
                else
                {
                    m_HoveredPointIndex = -1;
                    m_State = InteractionState.Idle;
                }
            }
        }

        /// <summary>
        /// 获取指定位置的控制点索引
        /// </summary>
        private int GetPointAtPosition(BezierCurve curve, Rect curveArea, BezierCurveSettings settings, Vector2 screenPos, out bool isControlIn, out bool isControlOut)
        {
            isControlIn = false;
            isControlOut = false;

            float pickDistance = settings.pickDistance;

            // 检查控制点
            for (int i = 0; i < curve.pointCount; i++)
            {
                BezierPoint point = curve.GetPoint(i);
                if (point == null)
                    continue;

                Vector2 pointScreen = BezierCurveDrawer.CurveToScreen(point.position, curveArea, settings);
                float distToPoint = Vector2.Distance(screenPos, pointScreen);

                // 检查切线控制点
                if (point.controlIn != Vector2.zero)
                {
                    Vector2 controlInScreen = BezierCurveDrawer.CurveToScreen(point.GetControlInWorld(), curveArea, settings);
                    float distToControlIn = Vector2.Distance(screenPos, controlInScreen);
                    if (distToControlIn < pickDistance)
                    {
                        isControlIn = true;
                        return i;
                    }
                }

                if (point.controlOut != Vector2.zero)
                {
                    Vector2 controlOutScreen = BezierCurveDrawer.CurveToScreen(point.GetControlOutWorld(), curveArea, settings);
                    float distToControlOut = Vector2.Distance(screenPos, controlOutScreen);
                    if (distToControlOut < pickDistance)
                    {
                        isControlOut = true;
                        return i;
                    }
                }

                // 检查锚点
                if (distToPoint < pickDistance)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// 吸附到网格
        /// </summary>
        private Vector2 SnapToGrid(Vector2 position, BezierCurveSettings settings)
        {
            if (!settings.snapToGrid)
                return position;

            float snapDist = settings.snapDistance;
            float subdivisions = settings.gridSubdivisions;

            float snappedX = Mathf.Round(position.x * subdivisions) / subdivisions;
            float snappedY = Mathf.Round(position.y * subdivisions) / subdivisions;

            if (Mathf.Abs(position.x - snappedX) < snapDist)
                position.x = snappedX;
            if (Mathf.Abs(position.y - snappedY) < snapDist)
                position.y = snappedY;

            return position;
        }

        /// <summary>
        /// 清除选择
        /// </summary>
        public void ClearSelection()
        {
            m_SelectedPointIndex = -1;
            m_HoveredPointIndex = -1;
            m_State = InteractionState.Idle;
        }
    }
}

