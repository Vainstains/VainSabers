using System;
using HMUI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VainSabers.Helpers;

namespace VainSabers.UI;

public class ButtonComponent : UIComponent, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    private ImageView m_imageView = null!;
    private Color m_baseColor = new Color(0.3f, 0.5f, 0.8f);

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
        if (m_isPressed)
            return color * new Color(0.7f, 0.7f, 0.7f, 1.0f);
        if (m_isHovered)
            return color;
        
        return color * new Color(0.85f, 0.85f, 0.85f, 1.0f);
    }

    protected virtual void UpdateState()
    {
        m_imageView.color = GetColor();
    }

    protected override void Init()
    {
        base.Init();
        m_imageView = gameObject.RequireComponent<ImageView>();
        m_imageView.raycastTarget = true;
        m_imageView.sprite = UIResources.LoadSpriteFromResource("VainSabers.ui_round.png", borderRatio: 0.5f);
        m_imageView.type = Image.Type.Sliced;
        m_imageView.material = UIResources.NoGlowMat;
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

public class FieldComponent<T> : UIComponent where T : UIComponent
{
    private TextComponent m_label = null!;
    private T m_component = null!;
    private float m_splitRatio = 0.5f;

    public T Component => m_component;

    public float SplitRatio
    {
        get => m_splitRatio;
        set
        {
            m_splitRatio = value;
            UpdateLayout();
        }
    }

    public FieldComponent<T> WithLabel(string text)
    {
        m_label.Text = text;
        return this;
    }
    
    protected override void Init()
    {
        base.Init();
        m_label = AddChild<TextComponent>();
        m_label.Alignment = TextAlignmentOptions.TopLeft;
        m_label.OverflowMode = TextOverflowModes.Overflow;
        m_label.EnableWordWrapping = false;
        m_label.Color = new Color(0.9f, 0.9f, 0.9f, 1.0f);

        m_component = AddChild<T>();
        m_component.Pivot = new Vector2(0.5f, 0.5f);
        m_component.SizeDelta = new Vector2(0, 0);
        
        UpdateLayout();
    }

    private void UpdateLayout()
    {
        m_label.ClearOffsets().SetAnchors(new Vector2(0, 0), new Vector2(m_splitRatio, 1f)).InsetTop(0.5f);
        m_component.ClearOffsets().SetAnchors(new Vector2(m_splitRatio, 0), new Vector2(1f, 1f));
    }
}