using TMPro;
using UnityEngine;

namespace VainSabers.UI;

public class SubPanelComponent : UIComponent
{
    private RoundRectComponent m_bg = null!;
    private TextComponent m_label = null!;
    private VerticalLayoutGroupComponent m_layout = null!;

    public string Title
    {
        get => m_label.Text;
        set => m_label.Text = value;
    }

    public UIComponent Content => m_layout;

    protected override void Init()
    {
        base.Init();
        m_bg = AddChild<RoundRectComponent>().ToFill();
        m_bg.Color = new Color(0.1f, 0.1f, 0.1f, 1.0f);
        m_bg.IsRaycastTarget = true;

        m_label = AddChild<TextComponent>().ToTopEdge().ExtendBottom(5);
        m_label.Alignment = TextAlignmentOptions.Center;
        m_label.FontSize = 4.5f;
        m_label.Color = new Color(0.7f, 0.7f, 0.7f, 1.0f);
        
        m_layout = AddChild<VerticalLayoutGroupComponent>().ToFill()
            .InsetTop(6).WithPadding(1).WithSpacing(0.5f);
        m_layout.ChildControlWidth = true;
        m_layout.ChildControlHeight = true;
        m_layout.ChildForceExpandWidth = true;
        m_layout.ChildForceExpandHeight = false;
    }

    public SubPanelComponent WithLabel(string text)
    {
        m_label.Text = text;
        return this;
    }
}