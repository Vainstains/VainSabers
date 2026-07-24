using UnityEngine;
using VainSabers.Helpers;

namespace VainSabers.UI;

public class UIComponent : MonoBehaviour, IUIParent
{
    private RectTransform m_rectTransform = null!;
    public RectTransform RectTransform => m_rectTransform;
    
    public Vector2 Pivot
    {
        get => m_rectTransform.pivot;
        set => m_rectTransform.pivot = value;
    }
    
    public Vector2 SizeDelta
    {
        get => m_rectTransform.sizeDelta;
        set => m_rectTransform.sizeDelta = value;
    }
    
    public Vector2 AnchorMin
    {
        get => m_rectTransform.anchorMin;
        set => m_rectTransform.anchorMin = value;
    }
    
    public Vector2 AnchorMax
    {
        get => m_rectTransform.anchorMax;
        set => m_rectTransform.anchorMax = value;
    }

    public Vector2 OffsetMin
    {
        get => m_rectTransform.offsetMin;
        set => m_rectTransform.offsetMin = value;
    }
    
    public Vector2 OffsetMax
    {
        get => m_rectTransform.offsetMax;
        set => m_rectTransform.offsetMax = value;
    }
    
    public Vector2 AnchoredPosition
    {
        get => m_rectTransform.anchoredPosition;
        set => m_rectTransform.anchoredPosition = value;
    }

    protected virtual void Init()
    {
        m_rectTransform = gameObject.RequireComponent<RectTransform>();
    }

    public T AddChild<T>() where T : UIComponent
    {
        return gameObject.AddInitChild<T>();
    }
}

