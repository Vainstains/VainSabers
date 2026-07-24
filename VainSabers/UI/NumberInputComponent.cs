using System;
using System.Collections.Generic;
using HMUI;
using IPA.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VainSabers.Helpers;
using VainSabers.Menu;
using VRUIControls;

namespace VainSabers.UI;

public class NumberInputComponent : UIComponent
{
    private const float DefaultFontSize = 4f;
    private const float DefaultDragSensitivity = 0.05f;
    private const float DragDeadZoneDegrees = 2f;
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
    private bool m_dragActive = false;
    private bool m_wasDragged = false;
    private Transform? m_dragControllerTransform;
    private Vector3 m_dragStartForwardXZ;
    private float m_dragStartValue;
    private float m_deadZoneOffset;
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
        dragHandler.OnPointerPressed += OnDragPointerDown;
        dragHandler.OnPointerReleased += OnDragPointerUp;

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
        if (m_wasDragged)
        {
            m_wasDragged = false;
            return;
        }
        TogglePopup();
    }

    private void OnDragPointerDown(PointerEventData eventData)
    {
        var controller = VRPointerManager.Instance?.ActiveTransform;
        if (controller == null)
        {
            Plugin.Log.Info("DragPointerDown: no active controller");
            return;
        }
        
        m_isDragging = true;
        m_dragActive = false;
        m_wasDragged = false;
        m_dragControllerTransform = controller;
        m_dragStartForwardXZ = Vector3.ProjectOnPlane(controller.forward, Vector3.up).normalized;
        m_dragStartValue = m_value;
        m_deadZoneOffset = 0f;
    }

    private void OnDragPointerUp(PointerEventData eventData)
    {
        m_isDragging = false;
        m_dragActive = false;
        m_dragControllerTransform = null;
    }

    private void Update()
    {
        if (!m_isDragging || m_dragControllerTransform == null)
            return;

        Vector3 currentForwardXZ = Vector3.ProjectOnPlane(m_dragControllerTransform.forward, Vector3.up).normalized;
        float angle = Vector3.SignedAngle(m_dragStartForwardXZ, currentForwardXZ, Vector3.up);

        if (!m_dragActive)
        {
            if (Mathf.Abs(angle) > DragDeadZoneDegrees)
            {
                m_dragActive = true;
                m_wasDragged = true;
                m_deadZoneOffset = angle;
            }
        }

        if (m_dragActive)
        {
            float effectiveAngle = angle - m_deadZoneOffset;
            float newValue = m_dragStartValue + effectiveAngle * m_dragSensitivity;
            SetValue(newValue, true);
        }
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
    
    private class DragHandlerComponent : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public event Action<PointerEventData>? OnPointerPressed;
        public event Action<PointerEventData>? OnPointerReleased;

        public void OnPointerDown(PointerEventData eventData)
        {
            OnPointerPressed?.Invoke(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            OnPointerReleased?.Invoke(eventData);
        }
    }
}