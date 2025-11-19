using UnityEngine;
using UnityEditor;

namespace BezierCurveEditor
{
    /// <summary>
    /// BezierCurve 的 Inspector 绘制器
    /// </summary>
    [CustomPropertyDrawer(typeof(BezierCurve))]
    public class BezierCurveInspector : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            // 绘制标签
            Rect labelRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(labelRect, label);

            // 绘制打开编辑器按钮
            Rect buttonRect = new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight + 2, position.width, EditorGUIUtility.singleLineHeight);
            if (GUI.Button(buttonRect, "Open Bezier Curve Editor"))
            {
                BezierCurveEditorWindow.OpenWindow();
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight * 2 + 4;
        }
    }

    /// <summary>
    /// 用于在 Inspector 中显示 BezierCurve 的 MonoBehaviour
    /// </summary>
    public class BezierCurveComponent : MonoBehaviour
    {
        public BezierCurve curve = new BezierCurve();

        private void OnValidate()
        {
            if (curve == null)
            {
                curve = BezierCurve.CreateSmooth(new Vector2(0.2f, 0.2f), new Vector2(0.8f, 0.8f));
            }
        }

        /// <summary>
        /// 在运行时求值曲线
        /// </summary>
        public Vector2 Evaluate(float t)
        {
            return curve != null ? curve.Evaluate(t) : Vector2.zero;
        }
    }
}

