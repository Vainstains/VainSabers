using System;
using System.Collections.Generic;
using HMUI;
using IPA.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VainSabers.Helpers;
using VRUIControls;

namespace VainSabers.UI;

public class NumberInputComponent : UIComponent
{
    private const float DefaultFontSize = 4f;
    private const float DefaultDragSensitivity = 0.05f;
    private const float PopupWidth = 20f;
    private const float PopupHeight = 30f;
    private const float ButtonSize = 5f;
    private const float ButtonSpacing = 0.5f;

    private ButtonComponent m_headerButton = null!;
    private TextComponent m_displayText = null!;
    private RoundRectComponent m_popupBackground = null!;
    private Canvas m_popupCanvas = null!;
    private VRGraphicRaycaster m_popupRaycaster = null!;
    private ButtonComponent m_popupBlocker = null!;

    private readonly List<ButtonComponent> m_numpadButtons = new();
    private TextComponent m_popupDisplayText = null!;
    private ButtonComponent m_okButton = null!;
    private ButtonComponent m_cancelButton = null!;
    private VerticalLayoutGroupComponent m_rowsContainer = null!;

    private float m_value = 0f;
    private float m_minValue = 0f;
    private float m_maxValue = 100f;
    private float m_stepSize = 1f;
    private float m_dragSensitivity = DefaultDragSensitivity;
    private int m_decimalPlaces = 0;
    private string m_formatString = "F0";

    private bool m_isDragging = false;
    private bool m_isPopupOpen = false;
    private Vector2 m_dragStartPos;
    private float m_dragStartValue;
    private string m_inputBuffer = "";
    private bool m_isTextInputMode = false;

    public event Action<float>? OnValueChanged;

    public float Value
    {
        get => m_value;
        set => SetValue(value, true);
    }

    public float MinValue
    {
        get => m_minValue;
        set
        {
            m_minValue = value;
            UpdateFormatString();
            SetValue(m_value, false);
        }
    }

    public float MaxValue
    {
        get => m_maxValue;
        set
        {
            m_maxValue = value;
            UpdateFormatString();
            SetValue(m_value, false);
        }
    }

    public float StepSize
    {
        get => m_stepSize;
        set
        {
            m_stepSize = Math.Max(0.001f, value);
            UpdateFormatString();
        }
    }

    public float DragSensitivity
    {
        get => m_dragSensitivity;
        set => m_dragSensitivity = Math.Max(0.001f, value);
    }

    public int DecimalPlaces
    {
        get => m_decimalPlaces;
        set
        {
            m_decimalPlaces = Math.Max(0, value);
            UpdateFormatString();
        }
    }

    public bool IsPopupOpen => m_isPopupOpen;

    public void SetValue(float value, bool invokeEvent)
    {
        float clamped = Mathf.Clamp(value, m_minValue, m_maxValue);
        float stepped = Mathf.Round(clamped / m_stepSize) * m_stepSize;
        stepped = Mathf.Clamp(stepped, m_minValue, m_maxValue);

        if (Math.Abs(m_value - stepped) < 0.0001f)
            return;
        
        m_value = stepped;
        UpdateDisplayText();
        m_inputBuffer = m_value.ToString(m_formatString);

        if (m_isTextInputMode && m_popupDisplayText != null)
            m_popupDisplayText.Text = m_inputBuffer;

        if (invokeEvent)
            OnValueChanged?.Invoke(m_value);
    }

    public void SetMinMaxStep(float min, float max, float step)
    {
        m_minValue = min;
        m_maxValue = max;
        m_stepSize = Math.Max(0.001f, step);
        UpdateFormatString();
        SetValue(m_value, false);
    }

    public NumberInputComponent WithMinMaxStep(float min, float max, float step)
    {
        SetMinMaxStep(min, max, step);
        return this;
    }

    public NumberInputComponent WithValue(float value)
    {
        SetValue(value, false);
        return this;
    }

    public void OpenPopup()
    {
        if (m_isPopupOpen)
            return;

        m_isPopupOpen = true;
        m_isTextInputMode = true;
        m_inputBuffer = m_value.ToString(m_formatString);

        BuildNumpad();
        m_popupBackground.gameObject.SetActive(true);
        m_popupBlocker.IsInteractable = true;
        m_headerButton.gameObject.SetActive(false);
    }

    public void ClosePopup()
    {
        if (!m_isPopupOpen)
            return;

        m_isPopupOpen = false;
        m_isTextInputMode = false;
        m_popupBackground.gameObject.SetActive(false);
        m_popupBlocker.IsInteractable = false;
        m_headerButton.gameObject.SetActive(true);
    }

    public void TogglePopup()
    {
        if (m_isPopupOpen)
            ClosePopup();
        else
            OpenPopup();
    }

    protected override void Init()
    {
        base.Init();
        
        // Header button (clickable and draggable)
        m_headerButton = AddChild<ButtonComponent>().ToFill();
        m_headerButton.Color = new Color(0.15f, 0.15f, 0.15f, 1f);
        m_headerButton.InstantClick = false;
        m_headerButton.OnClick += OnHeaderClick;

        var dragHandler = m_headerButton.gameObject.AddComponent<DragHandlerComponent>();
        dragHandler.OnDragStart += OnDragStart;
        dragHandler.OnDragged += OnDrag;
        dragHandler.OnDragEnd += OnDragEnd;

        // Display text inside header
        m_displayText = m_headerButton.AddChild<TextComponent>().ToFill().Inset(0.5f);
        m_displayText.Alignment = TextAlignmentOptions.Center;
        m_displayText.OverflowMode = TextOverflowModes.Overflow;
        m_displayText.EnableWordWrapping = false;
        m_displayText.Color = new Color(0.9f, 0.9f, 0.9f, 1.0f);
        m_displayText.FontSize = DefaultFontSize;

        // Popup blocker (full‑screen transparent)
        m_popupBlocker = AddChild<ButtonComponent>().ToFill().Extend(200);
        m_popupBlocker.Color = new Color(0, 0, 0, 0);
        m_popupBlocker.OnClick += ClosePopup;
        m_popupBlocker.IsInteractable = false;

        // Popup background – centered above the header
        m_popupBackground = AddChild<RoundRectComponent>().ToCenter().Move(0, 30f);
        m_popupBackground.SizeDelta = new Vector2(PopupWidth, PopupHeight);
        m_popupBackground.Color = new Color(0.1f, 0.1f, 0.1f, 0.95f);
        m_popupBackground.IsRaycastTarget = true;
        m_popupBackground.gameObject.SetActive(false);

        // Canvas for popup (to make it world‑space and clickable)
        m_popupCanvas = m_popupBackground.gameObject.AddComponent<Canvas>();
        m_popupCanvas.renderMode = RenderMode.WorldSpace;
        m_popupCanvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord2;
        m_popupCanvas.sortingOrder = 20;

        var scaler = m_popupBackground.gameObject.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 3.44f;
        scaler.referencePixelsPerUnit = 10;

        m_popupRaycaster = m_popupBackground.gameObject.AddComponent<VRGraphicRaycaster>();
        m_popupRaycaster.SetField("_physicsRaycaster", UIResources.Raycaster);

        // Popup layout: vertical stack containing display and numpad rows
        var popupLayout = m_popupBackground.AddChild<VerticalLayoutGroupComponent>().ToFill();
        popupLayout.WithPadding(2).WithSpacing(ButtonSpacing);
        popupLayout.ChildControlWidth = true;
        popupLayout.ChildControlHeight = true;
        popupLayout.ChildForceExpandWidth = true;
        popupLayout.ChildForceExpandHeight = false; // children will use their preferred heights

        // Display at the top of the popup
        m_popupDisplayText = popupLayout.AddChild<TextComponent>();
        m_popupDisplayText.LayoutElement.preferredHeight = 10f;
        m_popupDisplayText.LayoutElement.flexibleHeight = 0;
        m_popupDisplayText.Alignment = TextAlignmentOptions.Center;
        m_popupDisplayText.OverflowMode = TextOverflowModes.Overflow;
        m_popupDisplayText.EnableWordWrapping = false;
        m_popupDisplayText.Color = new Color(0.9f, 0.9f, 0.9f, 1f);
        m_popupDisplayText.FontSize = 4f;
        m_popupDisplayText.Text = m_inputBuffer;

        // Rows container – takes remaining space, stacks rows vertically
        m_rowsContainer = popupLayout.AddChild<VerticalLayoutGroupComponent>();
        m_rowsContainer.LayoutElement.flexibleHeight = 1;
        m_rowsContainer.ChildControlWidth = true;
        m_rowsContainer.ChildControlHeight = true;
        m_rowsContainer.ChildForceExpandWidth = true;
        m_rowsContainer.ChildForceExpandHeight = false; // rows keep their preferred height
        m_rowsContainer.WithSpacing(ButtonSpacing);

        UpdateDisplayText();
    }

    private void OnHeaderClick()
    {
        TogglePopup();
    }

    private void OnDragStart(PointerEventData eventData)
    {
        Plugin.Log.Info("Start Drag");
        m_isDragging = true;
        m_dragStartPos = eventData.position;
        m_dragStartValue = m_value;
    }

    private void OnDrag(PointerEventData eventData)
    {
        if (!m_isDragging)
            return;

        float deltaX = eventData.position.x - m_dragStartPos.x;
        Plugin.Log.Info($"delta x: {deltaX}");
        float deltaValue = deltaX * m_dragSensitivity * m_stepSize;
        Plugin.Log.Info($"delta val: {deltaValue}");
        float newValue = m_dragStartValue + deltaValue;
        SetValue(newValue, true);
    }

    private void OnDragEnd(PointerEventData eventData)
    {
        m_isDragging = false;
        Plugin.Log.Info("End Drag");
    }

    private void UpdateDisplayText()
    {
        m_displayText.Text = m_value.ToString(m_formatString);
    }

    private void UpdateFormatString()
    {
        if (m_stepSize >= 1f)
            m_decimalPlaces = 0;
        else if (m_stepSize >= 0.1f)
            m_decimalPlaces = 1;
        else if (m_stepSize >= 0.01f)
            m_decimalPlaces = 2;
        else if (m_stepSize >= 0.001f)
            m_decimalPlaces = 3;
        else
            m_decimalPlaces = 4;

        m_formatString = $"F{m_decimalPlaces}";
    }

    private void BuildNumpad()
    {
        foreach (var btn in m_numpadButtons)
            Destroy(btn.gameObject);
        m_numpadButtons.Clear();
        
        m_rowsContainer.ClearChildren();

        string[] rows = { "123", "456", "789", "0<" };

        foreach (string row in rows)
        {
            var rowLayout = m_rowsContainer.AddChild<HorizontalLayoutGroupComponent>();
            rowLayout.LayoutElement.preferredHeight = ButtonSize;
            rowLayout.ChildControlWidth = true;
            rowLayout.ChildControlHeight = true;
            rowLayout.ChildForceExpandWidth = true;
            rowLayout.ChildForceExpandHeight = true;
            rowLayout.WithSpacing(ButtonSpacing);
            rowLayout.WithPadding(0);
            foreach (char c in row)
            {
                var btn = rowLayout.AddChild<ButtonComponent>();
                btn.Color = new Color(0.2f, 0.2f, 0.25f, 1f);

                var btnText = btn.AddChild<TextComponent>().ToFill().Inset(0.5f);
                btnText.Alignment = TextAlignmentOptions.Center;
                btnText.Color = new Color(0.95f, 0.95f, 0.95f, 1f);
                btnText.FontSize = 3.5f;

                if (c == '<')
                {
                    btnText.Text = "<";
                    btn.OnClick += OnBackspaceClick;
                }
                else
                {
                    btnText.Text = c.ToString();
                    char digit = c;
                    btn.OnClick += () => OnDigitClick(digit);
                }

                m_numpadButtons.Add(btn);
            }
        }
        
        var okCancelRow = m_rowsContainer.AddChild<HorizontalLayoutGroupComponent>();
        okCancelRow.LayoutElement.preferredHeight = ButtonSize;
        okCancelRow.ChildControlWidth = true;
        okCancelRow.ChildControlHeight = true;
        okCancelRow.ChildForceExpandWidth = true;
        okCancelRow.ChildForceExpandHeight = true;
        okCancelRow.WithSpacing(ButtonSpacing);
        okCancelRow.WithPadding(0);
        
        m_cancelButton = okCancelRow.AddChild<ButtonComponent>();
        m_cancelButton.Color = new Color(0.5f, 0.2f, 0.2f, 1f);
        var cancelText = m_cancelButton.AddChild<TextComponent>().ToFill().Inset(0.5f);
        cancelText.Alignment = TextAlignmentOptions.Center;
        cancelText.Color = Color.white;
        cancelText.FontSize = 3f;
        cancelText.Text = "X";
        m_cancelButton.OnClick += () =>
        {
            m_inputBuffer = m_value.ToString(m_formatString);
            m_popupDisplayText.Text = m_inputBuffer;
            ClosePopup();
        };
        
        m_okButton = okCancelRow.AddChild<ButtonComponent>();
        m_okButton.Color = new Color(0.2f, 0.5f, 0.2f, 1f);
        var okText = m_okButton.AddChild<TextComponent>().ToFill().Inset(0.5f);
        okText.Alignment = TextAlignmentOptions.Center;
        okText.Color = Color.white;
        okText.FontSize = 3f;
        okText.Text = "OK";
        m_okButton.OnClick += OnOkClick;
    }

    private void OnDigitClick(char digit)
    {
        if (m_inputBuffer.Length >= 10)
            return;

        if (m_inputBuffer == "0" || m_inputBuffer == "-0")
            m_inputBuffer = digit.ToString();
        else if (m_inputBuffer == "-" && digit == '0')
            m_inputBuffer = "-0";
        else
            m_inputBuffer += digit;

        m_popupDisplayText.Text = m_inputBuffer;
    }

    private void OnBackspaceClick()
    {
        if (m_inputBuffer.Length > 0)
        {
            m_inputBuffer = m_inputBuffer[..^1];
            if (m_inputBuffer == "-")
                m_inputBuffer = "";
            m_popupDisplayText.Text = m_inputBuffer;
        }
    }

    private void OnOkClick()
    {
        if (float.TryParse(m_inputBuffer, out float parsedValue))
        {
            SetValue(parsedValue, true);
        }
        ClosePopup();
    }
    
    private class DragHandlerComponent : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public event Action<PointerEventData>? OnDragStart;
        public event Action<PointerEventData>? OnDragged;
        public event Action<PointerEventData>? OnDragEnd;

        public void OnBeginDrag(PointerEventData eventData)
        {
            OnDragStart?.Invoke(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            OnDragged?.Invoke(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            OnDragEnd?.Invoke(eventData);
        }
    }
}