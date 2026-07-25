using System;
using System.Collections.Generic;
using HMUI;
using IPA.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VainSabers.Helpers;
using VRUIControls;

namespace VainSabers.UI;

public class TextInputComponent : UIComponent
{
    private const float DefaultFontSize = 4f;
    private const float PopupWidth = 40f;
    private const float PopupHeight = 35f;
    private const float ButtonSize = 4.5f;
    private const float ButtonSpacing = 0.4f;
    private const int MaxBufferLength = 40;

    private static readonly string[] KeyboardRows =
    {
        "1234567890",
        "qwertyuiop",
        "asdfghjkl",
        "zxcvbnm,."
    };

    private ButtonComponent m_headerButton = null!;
    private TextComponent m_displayText = null!;
    private RoundRectComponent m_popupBackground = null!;
    private Canvas m_popupCanvas = null!;
    private VRGraphicRaycaster m_popupRaycaster = null!;
    private ButtonComponent m_popupBlocker = null!;

    private readonly List<ButtonComponent> m_keyButtons = new();
    private TextComponent m_popupDisplayText = null!;
    private ButtonComponent m_okButton = null!;
    private VerticalLayoutGroupComponent m_rowsContainer = null!;
    private ButtonComponent m_capsButton = null!;
    private TextComponent m_capsButtonText = null!;

    private string m_value = "";
    private string m_inputBuffer = "";
    private bool m_isPopupOpen = false;
    private bool m_capsLock = false;
    private bool m_shiftActive = false;

    public event Action<string>? OnValueChanged;

    public int MaxLength { get; set; } = MaxBufferLength;

    public void MoveKeyboardX(float offset)
    {
        m_popupBackground.Move(offset, 0);
    }

    public string Value
    {
        get => m_value;
        set => SetValue(value, true);
    }

    public bool IsPopupOpen => m_isPopupOpen;

    public void SetValue(string value, bool invokeEvent)
    {
        var clamped = value?.Length > MaxLength ? value[..MaxLength] : value ?? "";

        if (m_value == clamped)
            return;

        m_value = clamped;

        if (invokeEvent)
            OnValueChanged?.Invoke(m_value);
    }

    public TextInputComponent WithValue(string value)
    {
        SetValue(value, false);
        return this;
    }

    public void OpenPopup()
    {
        if (m_isPopupOpen)
            return;

        m_isPopupOpen = true;
        m_capsLock = false;
        m_shiftActive = false;
        m_inputBuffer = m_value;
        m_popupDisplayText.Text = m_inputBuffer;

        BuildKeyboard();
        UpdateCapsVisual();
        m_popupBackground.gameObject.SetActive(true);
        m_popupBlocker.IsInteractable = true;
        m_headerButton.gameObject.SetActive(false);
    }

    public void ClosePopup()
    {
        if (!m_isPopupOpen)
            return;

        m_isPopupOpen = false;
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

        m_headerButton = AddChild<ButtonComponent>().ToFill();
        m_headerButton.Color = new Color(0.15f, 0.15f, 0.15f, 1f);
        m_headerButton.OnClick += TogglePopup;

        m_displayText = m_headerButton.AddChild<TextComponent>().ToFill().Inset(0.5f);
        m_displayText.Alignment = TextAlignmentOptions.Center;
        m_displayText.OverflowMode = TextOverflowModes.Overflow;
        m_displayText.EnableWordWrapping = false;
        m_displayText.Color = new Color(0.9f, 0.9f, 0.9f, 1.0f);
        m_displayText.FontSize = DefaultFontSize;
        m_displayText.Text = "@";

        m_popupBlocker = AddChild<ButtonComponent>().ToFill().Extend(200);
        m_popupBlocker.Color = new Color(0, 0, 0, 0);
        m_popupBlocker.OnClick += ClosePopup;
        m_popupBlocker.IsInteractable = false;

        m_popupBackground = AddChild<RoundRectComponent>().ToBottomCenter().Move(0, PopupHeight * 0.5f);
        m_popupBackground.SizeDelta = new Vector2(PopupWidth, PopupHeight);
        m_popupBackground.Color = new Color(0.07f, 0.07f, 0.07f, 1.0f);
        m_popupBackground.IsRaycastTarget = true;
        m_popupBackground.gameObject.SetActive(false);

        m_popupCanvas = m_popupBackground.gameObject.AddComponent<Canvas>();
        m_popupCanvas.renderMode = RenderMode.WorldSpace;
        m_popupCanvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord2;
        m_popupCanvas.overrideSorting = true;
        m_popupCanvas.sortingOrder = 20;

        var scaler = m_popupBackground.gameObject.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 3.44f;
        scaler.referencePixelsPerUnit = 10;

        m_popupRaycaster = m_popupBackground.gameObject.AddComponent<VRGraphicRaycaster>();
        m_popupRaycaster.SetField("_physicsRaycaster", UIResources.Raycaster);

        var popupLayout = m_popupBackground.AddChild<VerticalLayoutGroupComponent>().ToFill();
        popupLayout.WithPadding(2).WithSpacing(ButtonSpacing);
        popupLayout.ChildControlWidth = true;
        popupLayout.ChildControlHeight = true;
        popupLayout.ChildForceExpandWidth = true;
        popupLayout.ChildForceExpandHeight = false;

        var textDisplayContainer = popupLayout.AddChild<RoundRectComponent>()
            .WithPreferredHeight(5);
        textDisplayContainer.LayoutElement.flexibleHeight = 0;
        textDisplayContainer.Color = new Color(0.2f, 0.2f, 0.2f, 0.7f);

        m_popupDisplayText = textDisplayContainer.AddChild<TextComponent>().ToFill();
        m_popupDisplayText.Alignment = TextAlignmentOptions.Center;
        m_popupDisplayText.OverflowMode = TextOverflowModes.Overflow;
        m_popupDisplayText.EnableWordWrapping = false;
        m_popupDisplayText.Color = new Color(0.9f, 0.9f, 0.9f, 1f);
        m_popupDisplayText.FontSize = 4f;
        m_popupDisplayText.Text = "";

        m_rowsContainer = popupLayout.AddChild<VerticalLayoutGroupComponent>();
        m_rowsContainer.LayoutElement.flexibleHeight = 1;
        m_rowsContainer.ChildControlWidth = true;
        m_rowsContainer.ChildControlHeight = true;
        m_rowsContainer.ChildForceExpandWidth = true;
        m_rowsContainer.ChildForceExpandHeight = false;
        m_rowsContainer.WithSpacing(ButtonSpacing);
    }

    private void BuildKeyboard()
    {
        foreach (var btn in m_keyButtons)
            Destroy(btn.gameObject);
        m_keyButtons.Clear();

        m_rowsContainer.ClearChildren();

        foreach (string row in KeyboardRows)
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
                char key = c;
                AddKeyButton(rowLayout, key.ToString(), () => OnCharacterClick(key));
            }
        }

        var bottomRow = m_rowsContainer.AddChild<HorizontalLayoutGroupComponent>();
        bottomRow.LayoutElement.preferredHeight = ButtonSize;
        bottomRow.ChildControlWidth = true;
        bottomRow.ChildControlHeight = true;
        bottomRow.ChildForceExpandWidth = true;
        bottomRow.ChildForceExpandHeight = true;
        bottomRow.WithSpacing(ButtonSpacing);
        bottomRow.WithPadding(0);

        m_capsButton = bottomRow.AddChild<ButtonComponent>();
        m_capsButton.Color = new Color(0.2f, 0.2f, 0.25f, 1f);
        m_capsButtonText = m_capsButton.AddChild<TextComponent>().ToFill().Inset(0.5f);
        m_capsButtonText.Alignment = TextAlignmentOptions.Center;
        m_capsButtonText.Color = new Color(0.95f, 0.95f, 0.95f, 1f);
        m_capsButtonText.FontSize = 2.5f;
        m_capsButtonText.Text = "CAPS";
        m_capsButton.OnClick += OnCapsClick;

        AddKeyButton(bottomRow, " ", () => OnCharacterClick(' '));

        AddKeyButton(bottomRow, "<", OnBackspaceClick);

        m_okButton = bottomRow.AddChild<ButtonComponent>();
        m_okButton.LayoutElement.preferredWidth = ButtonSize * 2 + ButtonSpacing;
        m_okButton.Color = new Color(0.2f, 0.5f, 0.2f, 1f);
        var okText = m_okButton.AddChild<TextComponent>().ToFill().Inset(0.5f);
        okText.Alignment = TextAlignmentOptions.Center;
        okText.Color = Color.white;
        okText.FontSize = 3f;
        okText.Text = "OK";
        m_okButton.OnClick += OnOkClick;
    }

    private void AddKeyButton(HorizontalLayoutGroupComponent row, string label, Action onClick)
    {
        var btn = row.AddChild<ButtonComponent>();
        btn.Color = new Color(0.2f, 0.2f, 0.25f, 1f);
        var btnText = btn.AddChild<TextComponent>().ToFill().Inset(0.5f);
        btnText.Alignment = TextAlignmentOptions.Center;
        btnText.Color = new Color(0.95f, 0.95f, 0.95f, 1f);
        btnText.FontSize = 3f;
        btnText.Text = label;
        btn.OnClick += onClick;
        m_keyButtons.Add(btn);
    }

    private void OnCharacterClick(char c)
    {
        if (m_inputBuffer.Length >= MaxLength)
            return;

        bool useUpper = m_capsLock ^ m_shiftActive;
        if (useUpper && char.IsLetter(c))
            c = char.ToUpper(c);

        m_inputBuffer += c;
        m_popupDisplayText.Text = m_inputBuffer;

        if (m_shiftActive)
        {
            m_shiftActive = false;
            UpdateCapsVisual();
        }
    }

    private void OnCapsClick()
    {
        m_capsLock = !m_capsLock;
        UpdateCapsVisual();
    }

    private void UpdateCapsVisual()
    {
        if (m_capsButtonText == null)
            return;

        bool active = m_capsLock || m_shiftActive;
        m_capsButton.Color = active
            ? new Color(0.35f, 0.35f, 0.5f, 1f)
            : new Color(0.2f, 0.2f, 0.25f, 1f);
        m_capsButtonText.Text = m_capsLock ? "CAPS" : (m_shiftActive ? "SHIFT" : "CAPS");
    }

    private void OnBackspaceClick()
    {
        if (m_inputBuffer.Length > 0)
        {
            m_inputBuffer = m_inputBuffer[..^1];
            m_popupDisplayText.Text = m_inputBuffer;
        }
    }

    private void OnOkClick()
    {
        SetValue(m_inputBuffer, true);
        ClosePopup();
    }
}
