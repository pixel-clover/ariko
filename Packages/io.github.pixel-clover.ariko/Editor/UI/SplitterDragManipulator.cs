using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// A UI Toolkit manipulator that allows a user to drag a visual element to resize two adjacent panels.
/// </summary>
public class SplitterDragManipulator : Manipulator
{
    /// <summary>
    /// Specifies the orientation of the splitter.
    /// </summary>
    public enum Orientation
    {
        /// <summary>
        /// The splitter adjusts the width of two horizontally adjacent panels.
        /// </summary>
        Horizontal,
        /// <summary>
        /// The splitter adjusts the height of two vertically adjacent panels.
        /// </summary>
        Vertical
    }

    private readonly VisualElement m_FirstPanel;
    private readonly Orientation m_Orientation;

    private readonly VisualElement m_Parent;
    private readonly VisualElement m_SecondPanel;
    private bool m_Active;
    private float m_Start;
    private float m_StartSizeFirst;
    private float m_StartSizeSecond;

    /// <summary>
    /// Initializes a new instance of the <see cref="SplitterDragManipulator"/> class.
    /// </summary>
    /// <param name="parent">The parent container of the panels being resized.</param>
    /// <param name="firstPanel">The first panel (left or top).</param>
    /// <param name="secondPanel">The second panel (right or bottom).</param>
    /// <param name="orientation">The orientation of the splitter.</param>
    public SplitterDragManipulator(VisualElement parent, VisualElement firstPanel, VisualElement secondPanel,
        Orientation orientation)
    {
        m_Parent = parent;
        m_FirstPanel = firstPanel;
        m_SecondPanel = secondPanel;
        m_Orientation = orientation;
    }

    protected override void RegisterCallbacksOnTarget()
    {
        target.RegisterCallback<MouseDownEvent>(OnMouseDown);
        target.RegisterCallback<MouseMoveEvent>(OnMouseMove);
        target.RegisterCallback<MouseUpEvent>(OnMouseUp);
    }

    protected override void UnregisterCallbacksFromTarget()
    {
        target.UnregisterCallback<MouseDownEvent>(OnMouseDown);
        target.UnregisterCallback<MouseMoveEvent>(OnMouseMove);
        target.UnregisterCallback<MouseUpEvent>(OnMouseUp);
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
    }
}
