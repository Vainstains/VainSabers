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
        m_imageView.sprite = UIResources.RoundSprite;
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

public class TextButtonComponent : ButtonComponent
{
    private TextComponent? m_textComponent;

    public TextComponent Text => m_textComponent ?? null!;

    protected override void UpdateState()
    {
        base.UpdateState();
        if (m_textComponent == null)
            return;
        m_textComponent.Color = IsInteractable ? new Color(0.9f, 0.9f, 0.9f, 1.0f) : new Color(0.7f, 0.7f, 0.7f, 0.8f);
    }

    protected override void Init()
    {
        base.Init();
        m_textComponent = AddChild<TextComponent>().ToFill();
        m_textComponent.Text = "Button";
        m_textComponent.Alignment = TextAlignmentOptions.Center;
        m_textComponent.OverflowMode = TextOverflowModes.Overflow;
        m_textComponent.EnableWordWrapping = false;

        UpdateState();
    }

    public TextButtonComponent WithText(string text)
    {
        if (m_textComponent == null)
            throw new Exception("shouldnt be possible (text component not initialized)");
        m_textComponent.Text = text;
        return this;
    }
}