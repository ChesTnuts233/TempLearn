using UnityEngine;

namespace BezierCurveEditor
{
    /// <summary>
    /// 贝塞尔曲线编辑器设置
    /// </summary>
    [System.Serializable]
    public class BezierCurveSettings
    {
        [Header("显示设置")]
        public bool showGrid = true;
        public bool showControlPoints = true;
        public bool showTangentLines = true;
        public bool showCurve = true;

        [Header("网格设置")]
        public int gridSubdivisions = 10;
        public Color gridColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        public Color gridSubdivisionColor = new Color(0.3f, 0.3f, 0.3f, 0.3f);

        [Header("曲线设置")]
        public Color curveColor = Color.white;
        public float curveWidth = 2f;
        public int curveResolution = 100;

        [Header("控制点设置")]
        public Color pointColor = Color.yellow;
        public Color selectedPointColor = Color.red;
        public float pointSize = 8f;
        public float selectedPointSize = 10f;

        [Header("切线设置")]
        public Color tangentColor = Color.cyan;
        public float tangentLineWidth = 1f;
        public float tangentHandleSize = 6f;

        [Header("交互设置")]
        public float pickDistance = 10f;
        public bool snapToGrid = false;
        public float snapDistance = 0.1f;
        public bool autoTangents = false;
        public bool mirrorTangents = false;

        [Header("视图设置")]
        public Rect viewBounds = new Rect(0, 0, 1, 1);
        public Vector2 panOffset = Vector2.zero;
        public float zoom = 1f;
        public float minZoom = 0.1f;
        public float maxZoom = 10f;
        public float zoomSpeed = 0.1f;

        /// <summary>
        /// 默认设置
        /// </summary>
        public static BezierCurveSettings Default => new BezierCurveSettings
        {
            showGrid = true,
            showControlPoints = true,
            showTangentLines = true,
            showCurve = true,
            gridSubdivisions = 10,
            gridColor = new Color(0.5f, 0.5f, 0.5f, 0.5f),
            gridSubdivisionColor = new Color(0.3f, 0.3f, 0.3f, 0.3f),
            curveColor = Color.white,
            curveWidth = 2f,
            curveResolution = 100,
            pointColor = Color.yellow,
            selectedPointColor = Color.red,
            pointSize = 8f,
            selectedPointSize = 10f,
            tangentColor = Color.cyan,
            tangentLineWidth = 1f,
            tangentHandleSize = 6f,
            pickDistance = 10f,
            snapToGrid = false,
            snapDistance = 0.1f,
            autoTangents = false,
            mirrorTangents = false,
            viewBounds = new Rect(0, 0, 1, 1),
            panOffset = Vector2.zero,
            zoom = 1f,
            minZoom = 0.1f,
            maxZoom = 10f,
            zoomSpeed = 0.1f
        };
    }
}

