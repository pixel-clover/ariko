using UnityEngine;
using UnityEngine.UIElements;

public class VerticalSplitterDragManipulator : Manipulator
{
    private readonly VisualElement m_Parent;
    private readonly VisualElement m_TopPanel;
    private readonly VisualElement m_BottomPanel;
    private bool m_Active;
    private float m_Start;

    public VerticalSplitterDragManipulator(VisualElement parent, VisualElement topPanel, VisualElement bottomPanel)
    {
        m_Parent = parent;
        m_TopPanel = topPanel;
        m_BottomPanel = bottomPanel;
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
        if (e.button == 0)
        {
            m_Start = e.localMousePosition.y;
            m_Active = true;
            target.CaptureMouse();
            e.StopPropagation();
        }
    }

    private void OnMouseMove(MouseMoveEvent e)
    {
        if (m_Active)
        {
            var delta = e.localMousePosition.y - m_Start;
            var newHeight = m_BottomPanel.resolvedStyle.height - delta;

            if (newHeight < m_BottomPanel.resolvedStyle.minHeight.value)
            {
                newHeight = m_BottomPanel.resolvedStyle.minHeight.value;
            }

            if (newHeight > m_Parent.resolvedStyle.height - m_TopPanel.resolvedStyle.minHeight.value)
            {
                newHeight = m_Parent.resolvedStyle.height - m_TopPanel.resolvedStyle.minHeight.value;
            }

            m_BottomPanel.style.flexBasis = newHeight;

            e.StopPropagation();
        }
    }

    private void OnMouseUp(MouseUpEvent e)
    {
        if (m_Active && e.button == 0)
        {
            m_Active = false;
            target.ReleaseMouse();
            e.StopPropagation();
        }
    }
}
