using System;
using HMUI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VainSabers.Helpers;

namespace VainSabers.UI;

public class ToggleComponent : UIComponent, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    private TextComponent m_label = null!;
    private RoundRectComponent m_switchOuter = null!;
    private RoundRectComponent m_switchInner = null!;
    private ImageView m_hitbox = null!;

    private bool m_isOn = false;
    private bool m_isHovered = false;
    private bool m_isPressed = false;
    private bool m_interactable = true;

    public bool IsOn
    {
        get => m_isOn;
        set
        {
            m_isOn = value;
            UpdateVisuals();
        }
    }

    public bool Interactable
    {
        get => m_interactable;
        set
        {
            m_interactable = value;
            UpdateVisuals();
        }
    }

    public string Label
    {
        get => m_label.Text;
        set => m_label.Text = value;
    }

    public event Action<bool>? OnValueChanged;

    protected override void Init()
    {
        base.Init();

        m_hitbox = gameObject.RequireComponent<ImageView>();
        m_hitbox.raycastTarget = true;
        m_hitbox.color = Color.clear;
        m_hitbox.sprite = UIResources.LoadSpriteFromResource("VainSabers.ui_round.png", borderRatio: 0.5f);
        m_hitbox.type = Image.Type.Sliced;
        m_hitbox.material = UIResources.NoGlowMat;

        m_label = AddChild<TextComponent>()
            .ToLeftCenter()
            .InsetRight(8);
        m_label.Alignment = TextAlignmentOptions.Left;
        m_label.EnableWordWrapping = false;
        m_label.OverflowMode = TextOverflowModes.Overflow;

        m_switchOuter = AddChild<RoundRectComponent>()
            .ToRightCenter()
            .ExtendLeft(8).ExtendTop(2).ExtendBottom(2);
        m_switchOuter.Color = new Color(0.3f, 0.3f, 0.3f, 1f);

        m_switchInner = m_switchOuter.AddChild<RoundRectComponent>()
            .ToLeftCenter();

        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        if (m_switchOuter == null || m_switchInner == null)
            return;

        Color outerColor = new Color(0.3f, 0.3f, 0.3f, 1f);;
        Color innerColor;
        
        m_switchInner.ToFill().Inset(0.5f);
        if (m_isOn)
        {
            m_switchInner.InsetLeft(3);
            innerColor = new Color(0.3f, 0.5f, 0.8f, 1f);
        }
        else
        {
            m_switchInner.InsetRight(3);
            innerColor = new Color(0.5f, 0.2f, 0.2f, 1f);
        }

        if (!m_interactable)
        {
            outerColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
            innerColor = new Color(0.6f, 0.6f, 0.6f, 0.5f);
        }
        else if (m_isPressed)
        {
            outerColor *= new Color(0.8f, 0.8f, 0.8f, 1f);
            innerColor *= new Color(0.8f, 0.8f, 0.8f, 1f);
        }
        else if (m_isHovered)
        {
            outerColor += new Color(0.2f, 0.2f, 0.2f, 0.0f);
            innerColor += new Color(0.1f, 0.1f, 0.1f, 0.0f);
        }

        m_switchOuter.Color = outerColor;
        m_switchInner.Color = innerColor;
        
        m_label.Color = m_interactable ? new Color(0.9f, 0.9f, 0.9f, 1f) : new Color(0.6f, 0.6f, 0.6f, 0.8f);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!m_interactable) return;
        m_isHovered = true;
        UpdateVisuals();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        m_isHovered = false;
        m_isPressed = false;
        UpdateVisuals();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!m_interactable) return;
        m_isPressed = true;
        UpdateVisuals();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!m_interactable) return;

        bool wasPressed = m_isPressed;
        m_isPressed = false;

        if (wasPressed && m_isHovered)
        {
            IsOn = !m_isOn;
            OnValueChanged?.Invoke(m_isOn);
        }

        UpdateVisuals();
    }

    public ToggleComponent WithLabel(string text)
    {
        m_label.Text = text;
        return this;
    }

    public ToggleComponent WithSize(float width, float height)
    {
        m_switchOuter.SizeDelta = new Vector2(width, height);
        m_switchInner.SizeDelta = new Vector2(height - 2, height - 2);
        return this;
    }

    public ToggleComponent WithValue(bool value)
    {
        m_isOn = value;
        UpdateVisuals();
        return this;
    }
}