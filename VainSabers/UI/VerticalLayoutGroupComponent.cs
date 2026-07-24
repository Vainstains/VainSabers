using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VainSabers.UI;

public class VerticalLayoutGroupComponent : UIComponent
{
    private VerticalLayoutGroup m_layoutGroup = null!;

    public bool ChildControlWidth
    {
        get => m_layoutGroup.childControlWidth;
        set => m_layoutGroup.childControlWidth = value;
    }
    
    public bool ChildControlHeight
    {
        get => m_layoutGroup.childControlHeight;
        set => m_layoutGroup.childControlHeight = value;
    }
    
    public bool ChildForceExpandWidth
    {
        get => m_layoutGroup.childForceExpandWidth;
        set => m_layoutGroup.childForceExpandWidth = value;
    }
    
    public bool ChildForceExpandHeight
    {
        get => m_layoutGroup.childForceExpandHeight;
        set => m_layoutGroup.childForceExpandHeight = value;
    }
    
    protected override void Init()
    {
        base.Init();
        m_layoutGroup = gameObject.AddComponent<VerticalLayoutGroup>();
    }

    public VerticalLayoutGroupComponent WithChildControlWidth(bool value)
    {
        ChildControlWidth = value;
        return this;
    }
    
    public VerticalLayoutGroupComponent WithChildControlHeight(bool value)
    {
        ChildControlHeight = value;
        return this;
    }
    
    public VerticalLayoutGroupComponent WithChildForceExpandWidth(bool value)
    {
        ChildForceExpandWidth = value;
        return this;
    }
    
    public VerticalLayoutGroupComponent WithChildForceExpandHeight(bool value)
    {
        ChildForceExpandHeight = value;
        return this;
    }

    public VerticalLayoutGroupComponent WithSpacing(float value)
    {
        m_layoutGroup.spacing = value;
        return this;
    }

    public VerticalLayoutGroupComponent WithTopPadding(int value)
    {
        var padding = m_layoutGroup.padding;
        padding.top = value;
        m_layoutGroup.padding = padding;
        return this;
    }

    public VerticalLayoutGroupComponent WithBottomPadding(int value)
    {
        var padding = m_layoutGroup.padding;
        padding.bottom = value;
        m_layoutGroup.padding = padding;
        return this;
    }

    public VerticalLayoutGroupComponent WithLeftPadding(int value)
    {
        var padding = m_layoutGroup.padding;
        padding.left = value;
        m_layoutGroup.padding = padding;
        return this;
    }

    public VerticalLayoutGroupComponent WithRightPadding(int value)
    {
        var padding = m_layoutGroup.padding;
        padding.right = value;
        m_layoutGroup.padding = padding;
        return this;
    }

    public VerticalLayoutGroupComponent WithPadding(int value) =>
        WithTopPadding(value).WithBottomPadding(value).WithLeftPadding(value).WithRightPadding(value);
    
    public UIComponent AddSpace(float value)
    {
        var child = AddChild<UIComponent>();
        child.LayoutElement.preferredHeight = value;
        return child;
    }

    public UIComponent AddSubHeader(string text)
    {
        var child = AddChild<UIComponent>();
        child.LayoutElement.preferredHeight = 4;
        var textElement = child.AddChild<TextComponent>().InsetTop(1);
        textElement.Alignment = TextAlignmentOptions.Center;
        textElement.FontSize = 3.0f;
        textElement.Color = new Color(0.7f, 0.7f, 0.7f, 0.8f);
        textElement.Text = text;
        return child;
    }
}
