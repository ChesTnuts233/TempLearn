using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace BezierCurveEditor
{
    /// <summary>
    /// 贝塞尔曲线编辑器主窗口
    /// </summary>
    public class BezierCurveEditorWindow : EditorWindow
    {
        private BezierCurve m_Curve;
        private BezierCurveSettings m_Settings;
        private BezierCurveInteraction m_Interaction;

        private Rect m_CurveArea;

        // 视图交互状态
        private bool m_IsPanning = false;
        private Vector2 m_PanStartMousePos;
        private Vector2 m_PanStartOffset;

        [MenuItem("Tools/Bezier Curve Editor")]
        public static void OpenWindow()
        {
            BezierCurveEditorWindow window = GetWindow<BezierCurveEditorWindow>("Bezier Curve Editor");
            window.minSize = new Vector2(600, 400);
            window.Show();
        }

        private void OnEnable()
        {
            // 初始化默认曲线
            if (m_Curve == null)
            {
                m_Curve = BezierCurve.CreateSmooth(new Vector2(0.2f, 0.2f), new Vector2(0.8f, 0.8f));
            }

            // 初始化设置
            if (m_Settings == null)
            {
                m_Settings = BezierCurveSettings.Default;
            }

            // 初始化交互系统
            if (m_Interaction == null)
            {
                m_Interaction = new BezierCurveInteraction();
            }

            // 更新视图边界
            UpdateViewBounds();
        }

        private void OnGUI()
        {
            if (m_Curve == null || m_Settings == null || m_Interaction == null)
            {
                OnEnable();
                return;
            }

            // 绘制菜单栏（只包含网格相关）
            DrawMenuBar();

            // 曲线绘制区域（先计算区域，以便后续事件处理使用）
            DrawCurveArea();

            // 底部：状态栏
            DrawStatusBar();

            // 处理视图交互（拖动和缩放）- 必须在所有 GUI 绘制之后
            HandleViewInteraction();

            // 处理输入（只有在没有进行视图操作时）
            if (!m_IsPanning && m_CurveArea.width > 0 && m_CurveArea.height > 0 && m_CurveArea.Contains(Event.current.mousePosition))
            {
                if (m_Interaction.HandleInput(m_Curve, m_CurveArea, m_Settings, Event.current))
                {
                    Repaint();
                    EditorUtility.SetDirty(this);
                }
            }

            // 处理键盘输入
            HandleKeyboard();
        }

        private void DrawMenuBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // View 菜单
            if (GUILayout.Button("View", EditorStyles.toolbarDropDown, GUILayout.Width(50)))
            {
                GenericMenu menu = new GenericMenu();
                menu.AddItem(new GUIContent("Show Grid"), m_Settings.showGrid, () => { m_Settings.showGrid = !m_Settings.showGrid; Repaint(); });
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Reset View"), false, ResetView);
                menu.ShowAsContext();
            }

            GUILayout.FlexibleSpace();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawCurveArea()
        {
            // 曲线绘制区域
            Rect totalRect = GUILayoutUtility.GetRect(0, 0, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            m_CurveArea = new Rect(totalRect.x + 5, totalRect.y + 5, totalRect.width - 10, totalRect.height - 10);

            // 只在 Repaint 事件时绘制
            if (Event.current.type == EventType.Repaint)
            {
                // 绘制背景（Unity 风格：深灰色）
                EditorGUI.DrawRect(m_CurveArea, new Color(0.15f, 0.15f, 0.15f, 1f));

                // 绘制可缩放网格（基于曲线空间，响应缩放和平移）
                BezierCurveDrawer.DrawGrid(m_CurveArea, m_Settings);
            }

            // 绘制边框（只在 Repaint 事件时）
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(new Rect(m_CurveArea.x - 1, m_CurveArea.y - 1, m_CurveArea.width + 2, 1), Color.black);
                EditorGUI.DrawRect(new Rect(m_CurveArea.x - 1, m_CurveArea.y - 1, 1, m_CurveArea.height + 2), Color.black);
                EditorGUI.DrawRect(new Rect(m_CurveArea.xMax, m_CurveArea.y - 1, 1, m_CurveArea.height + 2), Color.black);
                EditorGUI.DrawRect(new Rect(m_CurveArea.x - 1, m_CurveArea.yMax, m_CurveArea.width + 2, 1), Color.black);
            }
        }


        private void DrawStatusBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // 显示鼠标位置（曲线空间）
            // 必须确保 Layout 和 Repaint 事件时控件数量一致
            string mousePosText = "";
            if (m_CurveArea.width > 0 && m_CurveArea.height > 0 && m_CurveArea.Contains(Event.current.mousePosition))
            {
                Vector2 curvePos = BezierCurveDrawer.ScreenToCurve(Event.current.mousePosition, m_CurveArea, m_Settings);
                mousePosText = $"X: {curvePos.x:F2}  Y: {curvePos.y:F2}";
            }
            // 始终创建 Label，即使内容为空，确保控件数量一致
            GUILayout.Label(mousePosText, EditorStyles.miniLabel, GUILayout.Width(120));

            GUILayout.FlexibleSpace();

            // 显示视图信息（调试用）
            GUILayout.Label($"Zoom: {m_Settings.zoom:F2}", EditorStyles.miniLabel, GUILayout.Width(80));
            GUILayout.Label($"Pan: ({m_Settings.panOffset.x:F2}, {m_Settings.panOffset.y:F2})", EditorStyles.miniLabel, GUILayout.Width(120));

            // 显示点数量
            GUILayout.Label($"Points: {m_Curve.pointCount}", EditorStyles.miniLabel, GUILayout.Width(80));

            EditorGUILayout.EndHorizontal();
        }

        private void HandleKeyboard()
        {
            Event e = Event.current;
            if (e.type == EventType.KeyDown)
            {
                switch (e.keyCode)
                {
                    case KeyCode.Delete:
                    case KeyCode.Backspace:
                        if (m_Interaction.selectedPointIndex >= 0)
                        {
                            DeleteSelectedPoint();
                            e.Use();
                        }
                        break;

                    case KeyCode.A:
                        if (e.control || e.command)
                        {
                            // Ctrl+A: 全选（可以扩展）
                            e.Use();
                        }
                        break;

                    case KeyCode.F:
                        FitToView();
                        e.Use();
                        break;
                }
            }
        }


        private void DeleteSelectedPoint()
        {
            int index = m_Interaction.selectedPointIndex;
            if (index >= 0 && m_Curve.pointCount > 2)
            {
                m_Curve.RemovePoint(index);
                m_Interaction.ClearSelection();
                UpdateViewBounds();
                EditorUtility.SetDirty(this);
            }
        }

        private void FitToView()
        {
            if (m_Curve.pointCount > 0)
            {
                Rect bounds = m_Curve.GetBounds();
                m_Settings.viewBounds = bounds;
                m_Settings.panOffset = Vector2.zero;
                m_Settings.zoom = 1f;
                Repaint();
            }
        }

        private void ResetView()
        {
            m_Settings.viewBounds = new Rect(0, 0, 1, 1);
            m_Settings.panOffset = Vector2.zero;
            m_Settings.zoom = 1f;
            Repaint();
        }

        /// <summary>
        /// 处理视图交互（拖动和缩放）
        /// </summary>
        private void HandleViewInteraction()
        {
            Event e = Event.current;

            if (!IsValidArea())
                return;

            bool isInArea = m_CurveArea.Contains(e.mousePosition);

            if (e.type == EventType.ScrollWheel && isInArea)
            {
                HandleZoom(e);
                e.Use();
                return;
            }

            if (e.type == EventType.MouseDown && isInArea && IsPanButton(e))
            {
                StartPanning(e);
                e.Use();
                return;
            }

            if (e.type == EventType.MouseDrag && m_IsPanning)
            {
                HandlePan(e);
                e.Use();
                return;
            }

            if (e.type == EventType.MouseUp && m_IsPanning)
            {
                m_IsPanning = false;
                e.Use();
            }
        }

        private bool IsValidArea()
        {
            return m_CurveArea.width > 0 && m_CurveArea.height > 0;
        }

        private bool IsPanButton(Event e)
        {
            return e.button == 2 || (e.button == 0 && e.alt);
        }

        private void StartPanning(Event e)
        {
            m_IsPanning = true;
            m_PanStartMousePos = e.mousePosition;
            m_PanStartOffset = m_Settings.panOffset;
        }

        /// <summary>
        /// 处理缩放（以鼠标位置为中心）
        /// </summary>
        private void HandleZoom(Event e)
        {
            float newZoom = CalculateNewZoom(e.delta.y);
            if (Mathf.Approximately(newZoom, m_Settings.zoom))
                return;

            Vector2 mouseInCurveSpace = BezierCurveDrawer.ScreenToCurve(e.mousePosition, m_CurveArea, m_Settings);
            float oldZoom = m_Settings.zoom;

            m_Settings.zoom = newZoom;
            AdjustPanOffsetForZoom(mouseInCurveSpace, oldZoom, newZoom);

            Repaint();
        }

        private float CalculateNewZoom(float scrollDelta)
        {
            float zoomFactor = 1f - scrollDelta * m_Settings.zoomSpeed;
            float newZoom = m_Settings.zoom * zoomFactor;
            return Mathf.Clamp(newZoom, m_Settings.minZoom, m_Settings.maxZoom);
        }

        private void AdjustPanOffsetForZoom(Vector2 mouseInCurveSpace, float oldZoom, float newZoom)
        {
            Vector2 normalizedPos = (mouseInCurveSpace - m_Settings.viewBounds.min) / m_Settings.viewBounds.size;
            Vector2 centerOffset = normalizedPos - Vector2.one * 0.5f;
            Vector2 panAdjustment = centerOffset * (oldZoom - newZoom);
            m_Settings.panOffset += panAdjustment;
        }

        /// <summary>
        /// 处理平移（拖动视图）
        /// </summary>
        private void HandlePan(Event e)
        {
            Vector2 mouseDelta = e.mousePosition - m_PanStartMousePos;
            Vector2 viewDelta = CalculateViewDelta(mouseDelta);
            m_Settings.panOffset = m_PanStartOffset + viewDelta;
            Repaint();
        }

        private Vector2 CalculateViewDelta(Vector2 mouseDelta)
        {
            return new Vector2(
                mouseDelta.x / m_CurveArea.width,
                -mouseDelta.y / m_CurveArea.height
            );
        }

        private void UpdateViewBounds()
        {
            if (m_Curve.pointCount > 0)
            {
                Rect bounds = m_Curve.GetBounds();
                if (bounds.size.magnitude > 0)
                {
                    m_Settings.viewBounds = bounds;
                }
            }
        }

    }
}

