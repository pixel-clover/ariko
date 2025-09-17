using UnityEngine;
using UnityEngine.UIElements;

public class SplitterDragManipulator : Manipulator
{
    private readonly VisualElement m_Parent;
    private readonly VisualElement m_LeftPanel;
    private readonly VisualElement m_RightPanel;
    private bool m_Active;
    private float m_Start;

    public SplitterDragManipulator(VisualElement parent, VisualElement leftPanel, VisualElement rightPanel)
    {
        m_Parent = parent;
        m_LeftPanel = leftPanel;
        m_RightPanel = rightPanel;
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
            m_Start = e.localMousePosition.x;
            m_Active = true;
            target.CaptureMouse();
            e.StopPropagation();
        }
    }

    private void OnMouseMove(MouseMoveEvent e)
    {
        if (m_Active)
        {
            var delta = e.localMousePosition.x - m_Start;
            var leftWidth = m_LeftPanel.resolvedStyle.width + delta;
            var rightWidth = m_RightPanel.resolvedStyle.width - delta;

            if (leftWidth >= m_LeftPanel.resolvedStyle.minWidth.value && rightWidth >= m_RightPanel.resolvedStyle.minWidth.value)
            {
                m_LeftPanel.style.width = leftWidth;
                m_RightPanel.style.width = rightWidth;
            }

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
