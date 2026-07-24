using System;
using System.Collections.Generic;
using System.Linq;
using HMUI;
using IPA.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VainSabers.Helpers;
using VRUIControls;

namespace VainSabers.UI;

public class DropdownComponent : UIComponent
{
    class DropdownItemComponent : UIComponent, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
    {
        private ImageView m_background = null!;
        private TextComponent m_text = null!;
        private bool m_isHovered = false;
        private bool m_isSelected = false;

        public event Action? OnClick;

        public bool IsSelected
        {
            get => m_isSelected;
            set
            {
                if (m_isSelected == value) return;
                m_isSelected = value;
                UpdateVisuals();
            }
        }

        public string Text
        {
            get => m_text.Text;
            set => m_text.Text = value;
        }

        protected override void Init()
        {
            base.Init();
            m_background = gameObject.RequireComponent<ImageView>();
            m_background.sprite = UIResources.LoadSpriteFromResource("VainSabers.ui_round.png", borderRatio: 0.5f);
            m_background.type = Image.Type.Sliced;
            m_background.material = UIResources.NoGlowMat;
            m_background.raycastTarget = true;
            
            m_text = AddChild<TextComponent>().ToFill().InsetLeft(1.5f);
            m_text.Alignment = TextAlignmentOptions.Left;
            m_text.OverflowMode = TextOverflowModes.Overflow;
            m_text.EnableWordWrapping = false;
            m_text.Color = new Color(0.9f, 0.9f, 0.9f, 1.0f);

            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            Color baseColor;
            if (m_isSelected)
            {
                baseColor = new Color(0.3f, 0.5f, 0.8f, 0.5f);
            }
            else
            {
                baseColor = new Color(1f, 1f, 1f, 0f);
            }

            if (m_isHovered)
            {
                float r = Mathf.Min(baseColor.r * 1.2f, 1f);
                float g = Mathf.Min(baseColor.g * 1.2f, 1f);
                float b = Mathf.Min(baseColor.b * 1.2f, 1f);
                float a = Mathf.Max(baseColor.a, 0.15f);
                baseColor = new Color(r, g, b, a);
            }

            m_background.color = baseColor;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            m_isHovered = true;
            UpdateVisuals();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            m_isHovered = false;
            UpdateVisuals();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            OnClick?.Invoke();
        }
    }
    
    private const float RowHeight = 4f;
    private const string PlaceholderText = "  -";

    private ButtonComponent m_headerButton = null!;
    private TextComponent m_label = null!;
    private RoundRectComponent m_listBackground = null!;
    private ImageComponent m_arrow = null!;
    private Canvas m_listCanvas = null!;
    private VRGraphicRaycaster m_listRaycaster = null!;
    private ButtonComponent m_blocker = null!;

    private readonly List<DropdownItemComponent> m_optionButtons = new();
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
        m_options = options?.ToList() ?? new List<string>();
        Close();
        RebuildOptionButtons();
        SetSelectedIndex(m_options.Count > 0 ? Mathf.Clamp(selectedIndex, 0, m_options.Count - 1) : -1, false);
    }

    public void Open()
    {
        if (m_isOpen || m_options.Count == 0)
            return;

        m_isOpen = true;
        RebuildOptionButtons();
        SetSelectedIndex(m_selectedIndex, false);
        m_listBackground.gameObject.SetActive(true);
        m_blocker.IsInteractable = true;

        m_arrow.RectTransform.eulerAngles = new Vector3(0f, 0f, -90f);
    }

    public void Close()
    {
        m_isOpen = false;
        m_listBackground.gameObject.SetActive(false);
        m_blocker.IsInteractable = false;

        m_arrow.RectTransform.eulerAngles = new Vector3(0f, 0f, 0f);
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
        // Show placeholder when no valid selection
        m_label.Text = SelectedValue ?? PlaceholderText;   // <-- Changed line

        for (var i = 0; i < m_optionButtons.Count; i++)
            m_optionButtons[i].IsSelected = (i == m_selectedIndex);

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
            var optionButton = m_listBackground.AddChild<DropdownItemComponent>()
                .ToTopEdge()
                .Move(0, -RowHeight * i);
            optionButton.Text = m_options[i];

            optionButton.Pivot = new Vector2(0.5f, 1f);
            optionButton.SizeDelta = new Vector2(0, RowHeight);
            
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
        
        m_blocker = AddChild<ButtonComponent>()
            .ToFill().Extend(200);
        m_blocker.Color = new Color(0, 0, 0, 0);
        m_blocker.OnClick += Close;
        m_blocker.IsInteractable = false;

        m_label = m_headerButton.AddChild<TextComponent>().ToFill().Inset(1).InsetLeft(1);
        m_label.Alignment = TextAlignmentOptions.Left;
        m_label.OverflowMode = TextOverflowModes.Overflow;
        m_label.EnableWordWrapping = false;
        m_label.Color = new Color(0.9f, 0.9f, 0.9f, 1.0f);

        m_arrow = m_headerButton.AddChild<ImageComponent>().ToRightCenter().Move(-2f, 0)
            .Extend(1.5f);
        m_arrow.Color = new Color(0.5f, 0.5f, 0.5f, 1f);
        m_arrow.Sprite = UIResources.LoadSpriteFromResource("VainSabers.dropdown_arrow.png");
        m_arrow.Pivot = new Vector2(0.5f, 0.5f);
        
        m_listBackground = AddChild<RoundRectComponent>()
            .ToBottomEdge()
            .Move(0, -0.5f);
        m_listBackground.Pivot = new Vector2(0.5f, 1f);
        m_listBackground.Color = new Color(0.1f, 0.1f, 0.1f, 1f);
        m_listBackground.IsRaycastTarget = true;
        m_listBackground.gameObject.SetActive(false);
        
        m_listCanvas = m_listBackground.gameObject.AddComponent<Canvas>();
        m_listCanvas.renderMode = RenderMode.WorldSpace;
        m_listCanvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord2;
        m_listCanvas.sortingOrder = 20;

        var scaler = m_listBackground.gameObject.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 3.44f;
        scaler.referencePixelsPerUnit = 10;

        m_listRaycaster = m_listBackground.gameObject.AddComponent<VRGraphicRaycaster>();
        m_listRaycaster.SetField("_physicsRaycaster", UIResources.Raycaster);
    }
}