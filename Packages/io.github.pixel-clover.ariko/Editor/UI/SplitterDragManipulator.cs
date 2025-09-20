using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
///     A UI Toolkit manipulator that allows a user to drag a visual element to resize two adjacent panels.
///     Now persists the splitter position via EditorPrefs.
/// </summary>
public class SplitterDragManipulator : Manipulator
{
    /// <summary>
    ///     Specifies the orientation of the splitter.
    /// </summary>
    public enum Orientation
    {
        /// <summary>
        ///     The splitter adjusts the width of two horizontally adjacent panels.
        /// </summary>
        Horizontal,

        /// <summary>
        ///     The splitter adjusts the height of two vertically adjacent panels.
        /// </summary>
        Vertical
    }

    private readonly VisualElement m_FirstPanel;
    private readonly Orientation m_Orientation;

    private readonly VisualElement m_Parent;

    private readonly string m_PrefKey; // stores ratio relative to parent
    private readonly VisualElement m_SecondPanel;
    private bool m_Active;
    private bool m_InitialApplied;
    private float m_Start;
    private float m_StartSizeFirst;
    private float m_StartSizeSecond;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SplitterDragManipulator" /> class.
    /// </summary>
    /// <param name="parent">The parent container of the panels being resized.</param>
    /// <param name="firstPanel">The first panel (left or top).</param>
    /// <param name="secondPanel">The second panel (right or bottom).</param>
    /// <param name="orientation">The orientation of the splitter.</param>
    /// <param name="prefKey">Optional EditorPrefs key to persist splitter position.</param>
    public SplitterDragManipulator(VisualElement parent, VisualElement firstPanel, VisualElement secondPanel,
        Orientation orientation, string prefKey = null)
    {
        m_Parent = parent;
        m_FirstPanel = firstPanel;
        m_SecondPanel = secondPanel;
        m_Orientation = orientation;
        m_PrefKey = prefKey;
    }

    protected override void RegisterCallbacksOnTarget()
    {
        target.RegisterCallback<MouseDownEvent>(OnMouseDown);
        target.RegisterCallback<MouseMoveEvent>(OnMouseMove);
        target.RegisterCallback<MouseUpEvent>(OnMouseUp);

        if (!string.IsNullOrEmpty(m_PrefKey))
            // Apply saved ratio on first valid layout of parent
            m_Parent.RegisterCallback<GeometryChangedEvent>(OnParentGeometryChanged);
    }

    protected override void UnregisterCallbacksFromTarget()
    {
        target.UnregisterCallback<MouseDownEvent>(OnMouseDown);
        target.UnregisterCallback<MouseMoveEvent>(OnMouseMove);
        target.UnregisterCallback<MouseUpEvent>(OnMouseUp);
        m_Parent.UnregisterCallback<GeometryChangedEvent>(OnParentGeometryChanged);
    }

    private void OnParentGeometryChanged(GeometryChangedEvent evt)
    {
        if (m_InitialApplied) return;
        if (evt.newRect.width <= 0 || evt.newRect.height <= 0) return;
        ApplyInitialFromPrefs();
        m_InitialApplied = true;
        // No longer need to listen
        m_Parent.UnregisterCallback<GeometryChangedEvent>(OnParentGeometryChanged);
    }

    private void OnMouseDown(MouseDownEvent e)
    {
        if (e.button != 0) return;

        if (m_Orientation == Orientation.Horizontal)
        {
            m_Start = e.mousePosition.x;
            m_StartSizeFirst = m_FirstPanel.resolvedStyle.width;
        }
        else
        {
            m_Start = e.mousePosition.y;
            m_StartSizeSecond = m_SecondPanel.resolvedStyle.height;
        }

        m_Active = true;
        target.CaptureMouse();
        e.StopPropagation();
    }

    private void OnMouseMove(MouseMoveEvent e)
    {
        if (!m_Active) return;

        if (m_Orientation == Orientation.Horizontal)
        {
            var delta = e.mousePosition.x - m_Start;
            var newSize = m_StartSizeFirst + delta;
            var minSizeFirst = m_FirstPanel.resolvedStyle.minWidth.value;
            var minSizeSecond = m_SecondPanel.resolvedStyle.minWidth.value;
            var parentSize = m_Parent.resolvedStyle.width;

            newSize = Mathf.Max(minSizeFirst, newSize);
            newSize = Mathf.Min(parentSize - minSizeSecond, newSize);

            m_FirstPanel.style.flexBasis = newSize;
        }
        else
        {
            var delta = e.mousePosition.y - m_Start;
            var newSize = m_StartSizeSecond - delta;
            var minSizeFirst = m_FirstPanel.resolvedStyle.minHeight.value;
            var minSizeSecond = m_SecondPanel.resolvedStyle.minHeight.value;
            var parentSize = m_Parent.resolvedStyle.height;

            newSize = Mathf.Max(minSizeSecond, newSize);
            newSize = Mathf.Min(parentSize - minSizeFirst, newSize);

            m_SecondPanel.style.flexBasis = newSize;
        }

        e.StopPropagation();
    }

    private void OnMouseUp(MouseUpEvent e)
    {
        if (!m_Active || e.button != 0) return;

        m_Active = false;
        target.ReleaseMouse();
        e.StopPropagation();

        SaveToPrefs();
    }

    private void ApplyInitialFromPrefs()
    {
        if (string.IsNullOrEmpty(m_PrefKey)) return;
        if (!EditorPrefs.HasKey(m_PrefKey)) return;
        var ratio = EditorPrefs.GetFloat(m_PrefKey, -1f);
        if (ratio <= 0f || ratio >= 1f) return;

        if (m_Orientation == Orientation.Horizontal)
        {
            var parentSize = Mathf.Max(1f, m_Parent.resolvedStyle.width);
            m_FirstPanel.style.flexBasis = ratio * parentSize;
        }
        else
        {
            var parentSize = Mathf.Max(1f, m_Parent.resolvedStyle.height);
            m_SecondPanel.style.flexBasis = ratio * parentSize;
        }
    }

    private void SaveToPrefs()
    {
        if (string.IsNullOrEmpty(m_PrefKey)) return;
        if (m_Orientation == Orientation.Horizontal)
        {
            var parentSize = Mathf.Max(1f, m_Parent.resolvedStyle.width);
            var size = m_FirstPanel.resolvedStyle.width; // flexBasis reflected
            var ratio = Mathf.Clamp01(size / parentSize);
            EditorPrefs.SetFloat(m_PrefKey, ratio);
        }
        else
        {
            var parentSize = Mathf.Max(1f, m_Parent.resolvedStyle.height);
            var size = m_SecondPanel.resolvedStyle.height;
            var ratio = Mathf.Clamp01(size / parentSize);
            EditorPrefs.SetFloat(m_PrefKey, ratio);
        }
    }
}
