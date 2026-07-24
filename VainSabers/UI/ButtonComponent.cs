using System;
using System.Collections.Generic;
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

public class DropdownComponent : UIComponent
{
    private const float RowHeight = 5f;

    private ButtonComponent m_headerButton = null!;
    private TextComponent m_label = null!;
    private RoundRectComponent m_listBackground = null!;

    private readonly List<TextButtonComponent> m_optionButtons = new();
    private List<string> m_options = new();
    private int m_selectedIndex = -1;
    private bool m_isOpen = false;

    public event Action<int>? OnSelectionChanged;

    public IReadOnlyList<string> Options => m_options;

    public int SelectedIndex
    {
        get => m_selectedIndex;
        set => SetSelectedIndex(value, true);
    }

    public string? SelectedValue =>
        m_selectedIndex >= 0 && m_selectedIndex < m_options.Count ? m_options[m_selectedIndex] : null;

    public bool IsOpen => m_isOpen;

    public void SetOptions(IEnumerable<string> options, int selectedIndex = 0)
    {
        m_options = new List<string>(options);
        Close();
        RebuildOptionButtons();
        SetSelectedIndex(m_options.Count > 0 ? Mathf.Clamp(selectedIndex, 0, m_options.Count - 1) : -1, false);
    }

    public void Open()
    {
        if (m_isOpen || m_options.Count == 0)
            return;

        m_isOpen = true;
        m_listBackground.gameObject.SetActive(true);
    }

    public void Close()
    {
        m_isOpen = false;
        m_listBackground.gameObject.SetActive(false);
    }

    public void Toggle()
    {
        if (m_isOpen)
            Close();
        else
            Open();
    }

    private void SetSelectedIndex(int index, bool invokeEvent)
    {
        if (index < -1 || index >= m_options.Count)
            return;

        m_selectedIndex = index;
        m_label.Text = SelectedValue ?? "";

        for (var i = 0; i < m_optionButtons.Count; i++)
            m_optionButtons[i].Color = i == m_selectedIndex ? new Color(0.3f, 0.3f, 0.3f, 1f) : new Color(0.15f, 0.15f, 0.15f, 1f);

        if (invokeEvent)
            OnSelectionChanged?.Invoke(m_selectedIndex);
    }

    private void RebuildOptionButtons()
    {
        foreach (var btn in m_optionButtons)
            Destroy(btn.gameObject);
        m_optionButtons.Clear();

        for (var i = 0; i < m_options.Count; i++)
        {
            var index = i;

            var optionButton = m_listBackground.AddChild<TextButtonComponent>()
                .ToTopEdge()
                .Move(0, -RowHeight * i)
                .WithText(m_options[i]);

            optionButton.Pivot = new Vector2(0.5f, 1f);
            optionButton.SizeDelta = new Vector2(0, RowHeight);
            optionButton.Color = new Color(0.15f, 0.15f, 0.15f, 1f);
            optionButton.OnClick += () =>
            {
                SetSelectedIndex(index, true);
                Close();
            };

            m_optionButtons.Add(optionButton);
        }

        m_listBackground.SizeDelta = new Vector2(m_listBackground.SizeDelta.x, RowHeight * m_options.Count);
    }

    protected override void Init()
    {
        base.Init();

        m_headerButton = AddChild<ButtonComponent>().ToFill();
        m_headerButton.Color = new Color(0.15f, 0.15f, 0.15f, 1f);
        m_headerButton.OnClick += Toggle;

        m_label = m_headerButton.AddChild<TextComponent>().ToFill().Inset(1);
        m_label.Alignment = TextAlignmentOptions.Left;
        m_label.OverflowMode = TextOverflowModes.Ellipsis;
        m_label.EnableWordWrapping = false;

        m_listBackground = AddChild<RoundRectComponent>()
            .ToBottomEdge()
            .Move(0, -0.5f);
        m_listBackground.Pivot = new Vector2(0.5f, 1f);
        m_listBackground.Color = new Color(0.1f, 0.1f, 0.1f, 1f);
        m_listBackground.IsRaycastTarget = true;
        m_listBackground.gameObject.SetActive(false);
    }
}