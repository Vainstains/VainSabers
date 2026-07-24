using System;
using HMUI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VainSabers.Helpers;

namespace VainSabers.UI;

public class UIComponent : MonoBehaviour
{
    private RectTransform m_rectTransform = null!;
    
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

    public UIComponent Move(Vector2 delta)
    {
        AnchoredPosition += delta;
        return this;
    }

    public UIComponent ExtendTop(float delta)
    {
        OffsetMax += new Vector2(0, delta);
        return this;
    }

    public UIComponent ExtendBottom(float delta)
    {
        OffsetMin += new Vector2(0, -delta);
        return this;
    }

    public UIComponent ExtendLeft(float delta)
    {
        OffsetMin += new Vector2(-delta, 0);
        return this;
    }

    public UIComponent ExtendRight(float delta)
    {
        OffsetMax += new Vector2(delta, 0);
        return this;
    }

    public UIComponent InsetTop(float delta) => ExtendTop(-delta);
    public UIComponent InsetBottom(float delta) => ExtendBottom(delta);
    public UIComponent InsetLeft(float delta) => ExtendLeft(delta);
    public UIComponent InsetRight(float delta) => ExtendRight(delta);

    public UIComponent Extend(float delta) =>
        ExtendTop(delta).ExtendBottom(delta).ExtendLeft(delta).ExtendRight(delta);

    public UIComponent Inset(float delta) =>
        InsetTop(delta).InsetBottom(delta).InsetLeft(delta).InsetRight(delta);
}

public class ImageComponent : UIComponent
{
    private ImageView m_imageView = null!;

    public bool IsRaycastTarget
    {
        get => m_imageView.raycastTarget;
        set => m_imageView.raycastTarget = value;
    }

    public Color Color
    {
        get => m_imageView.color;
        set => m_imageView.color = value;
    }

    public Sprite? Sprite
    {
        get => m_imageView.sprite;
        set => m_imageView.sprite = value;
    }

    protected override void Init()
    {
        base.Init();
        m_imageView = gameObject.RequireComponent<ImageView>();
        m_imageView.raycastTarget = false;
        m_imageView.color = Color.white;
        m_imageView.sprite = null;
    }
}

public class TextComponent : UIComponent
{
    private CurvedTextMeshPro m_textMeshPro = null!;

    public Color Color
    {
        get => m_textMeshPro.color;
        set => m_textMeshPro.color = value;
    }

    public string Text
    {
        get => m_textMeshPro.text;
        set => m_textMeshPro.text = value;
    }

    public float FontSize
    {
        get => m_textMeshPro.fontSize;
        set => m_textMeshPro.fontSize = value;
    }

    public TextAlignmentOptions Alignment
    {
        get => m_textMeshPro.alignment;
        set => m_textMeshPro.alignment = value;
    }

    public TextOverflowModes OverflowMode
    {
        get => m_textMeshPro.overflowMode;
        set => m_textMeshPro.overflowMode = value;
    }

    public bool EnableWordWrapping
    {
        get => m_textMeshPro.enableWordWrapping;
        set => m_textMeshPro.enableWordWrapping = value;
    }

    protected override void Init()
    {
        base.Init();
        m_textMeshPro = gameObject.RequireComponent<CurvedTextMeshPro>();
        m_textMeshPro.color = Color.white;
        m_textMeshPro.fontSize = 4f;
        m_textMeshPro.alignment = TextAlignmentOptions.TopLeft;
        m_textMeshPro.overflowMode = TextOverflowModes.Overflow;
        m_textMeshPro.enableWordWrapping = true;
    }
}

public class ButtonComponent : UIComponent, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    private ImageView m_imageView = null!;
    private Color m_baseColor = Color.white;

    private bool m_isHovered = false;
    private bool m_isPressed = false;

    public bool IsInteractable
    {
        get => m_imageView.raycastTarget;
        set
        {
            m_imageView.raycastTarget = value;
            UpdateState();
        }
    }

    public Color Color
    {
        get => m_baseColor;
        set
        {
            m_baseColor = value;
            UpdateState();
        }
    }

    public event Action? OnClick;

    private Color GetColor()
    {
        var color = m_baseColor;

        if (!m_imageView.raycastTarget)
            return color * new Color(0.4f, 0.4f, 0.4f, 0.7f);
        else if (m_isHovered)
            return color + new Color(0.2f, 0.2f, 0.2f, 0.0f);
        else if (m_isPressed)
            return color * new Color(0.8f, 0.8f, 0.8f, 1.0f);
        
        return color;
    }

    private void UpdateState()
    {
        m_imageView.color = GetColor();
    }

    protected override void Init()
    {
        base.Init();
        m_imageView = gameObject.RequireComponent<ImageView>();
        m_imageView.raycastTarget = true;
        m_imageView.sprite = null;
        UpdateState();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        m_isHovered = true;
        UpdateState();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        m_isHovered = false;
        UpdateState();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        m_isPressed = true;
        UpdateState();

        OnClick?.Invoke();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        m_isPressed = false;
        UpdateState();
    }
}