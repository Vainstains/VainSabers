using UnityEngine.UI;

namespace VainSabers.UI;

public class HorizontalLayoutGroupComponent : UIComponent
{
    private HorizontalLayoutGroup m_layoutGroup = null!;

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
        m_layoutGroup = gameObject.AddComponent<HorizontalLayoutGroup>();
    }
    
    public HorizontalLayoutGroupComponent WithChildControlWidth(bool value)
    {
        ChildControlWidth = value;
        return this;
    }
    
    public HorizontalLayoutGroupComponent WithChildControlHeight(bool value)
    {
        ChildControlHeight = value;
        return this;
    }
    
    public HorizontalLayoutGroupComponent WithChildForceExpandWidth(bool value)
    {
        ChildForceExpandWidth = value;
        return this;
    }
    
    public HorizontalLayoutGroupComponent WithChildForceExpandHeight(bool value)
    {
        ChildForceExpandHeight = value;
        return this;
    }

    public HorizontalLayoutGroupComponent WithSpacing(float value)
    {
        m_layoutGroup.spacing = value;
        return this;
    }

    public HorizontalLayoutGroupComponent WithTopPadding(int value)
    {
        var padding = m_layoutGroup.padding;
        padding.top = value;
        m_layoutGroup.padding = padding;
        return this;
    }

    public HorizontalLayoutGroupComponent WithBottomPadding(int value)
    {
        var padding = m_layoutGroup.padding;
        padding.bottom = value;
        m_layoutGroup.padding = padding;
        return this;
    }

    public HorizontalLayoutGroupComponent WithLeftPadding(int value)
    {
        var padding = m_layoutGroup.padding;
        padding.left = value;
        m_layoutGroup.padding = padding;
        return this;
    }

    public HorizontalLayoutGroupComponent WithRightPadding(int value)
    {
        var padding = m_layoutGroup.padding;
        padding.right = value;
        m_layoutGroup.padding = padding;
        return this;
    }

    public HorizontalLayoutGroupComponent WithPadding(int value) =>
        WithTopPadding(value).WithBottomPadding(value).WithLeftPadding(value).WithRightPadding(value);
}