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
    private const float PopupHeight = 40f;
    private const float ButtonSize = 4.5f;
    private const float ButtonSpacing = 0.4f;
    private const int MaxBufferLength = 40;

    private struct KeyDef
    {
        public readonly string Normal;
        public readonly string Shifted;
        public readonly char InputNormal;
        public readonly char InputShifted;

        public KeyDef(string normal, string shifted)
        {
            Normal = normal;
            Shifted = shifted;
            InputNormal = normal[0];
            InputShifted = shifted[0];
        }

        public KeyDef(string normal, string shifted, char inputNormal, char inputShifted)
        {
            Normal = normal;
            Shifted = shifted;
            InputNormal = inputNormal;
            InputShifted = inputShifted;
        }
    }

    private static readonly KeyDef[] Row1 = {
        new("1", "!"), new("2", "@"), new("3", "#"), new("4", "$"), new("5", "%"),
        new("6", "^"), new("7", "&"), new("8", "*"), new("9", "("), new("0", ")")
    };

    private static readonly KeyDef[] Row2 = {
        new("q", "Q"), new("w", "W"), new("e", "E"), new("r", "R"), new("t", "T"),
        new("y", "Y"), new("u", "U"), new("i", "I"), new("o", "O"), new("p", "P")
    };

    private static readonly KeyDef[] Row3 = {
        new("a", "A"), new("s", "S"), new("d", "D"), new("f", "F"), new("g", "G"),
        new("h", "H"), new("j", "J"), new("k", "K"), new("l", "L")
    };

    private static readonly KeyDef[] Row4 = {
        new("z", "Z"), new("x", "X"), new("c", "C"), new("v", "V"), new("b", "B"),
        new("n", "N"), new("m", "M")
    };

    private static readonly KeyDef[] Row5 = {
        new("-", "_"), new("=", "+"), new("[", "{"), new("]", "}"),
        new(";", ":"), new("'", "\""), new(",", "<"), new(".", ">")
    };

    private static readonly KeyDef[][] AllRows = { Row1, Row2, Row3, Row4, Row5 };

    private readonly List<KeyButtonInfo> m_keyButtons = new();

    private ButtonComponent m_headerButton = null!;
    private TextComponent m_displayText = null!;
    private RoundRectComponent m_popupBackground = null!;
    private Canvas m_popupCanvas = null!;
    private VRGraphicRaycaster m_popupRaycaster = null!;
    private ButtonComponent m_popupBlocker = null!;

    private TextComponent m_popupDisplayText = null!;
    private ButtonComponent m_okButton = null!;
    private VerticalLayoutGroupComponent m_rowsContainer = null!;
    private ButtonComponent m_shiftButton = null!;
    private TextComponent m_capsButtonText = null!;

    private string m_value = "";
    private string m_inputBuffer = "";
    private bool m_isPopupOpen = false;
    private bool m_shift = false;

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
        m_shift = false;
        m_inputBuffer = m_value;
        m_popupDisplayText.Text = m_inputBuffer;

        BuildKeyboard();
        UpdateShiftVisuals();
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
        foreach (var info in m_keyButtons)
            if (info.Button != null)
                Destroy(info.Button.gameObject);
        m_keyButtons.Clear();

        m_rowsContainer.ClearChildren();

        bool showShifted = m_shift;

        foreach (var row in AllRows)
        {
            var rowLayout = m_rowsContainer.AddChild<HorizontalLayoutGroupComponent>();
            rowLayout.LayoutElement.preferredHeight = ButtonSize;
            rowLayout.ChildControlWidth = true;
            rowLayout.ChildControlHeight = true;
            rowLayout.ChildForceExpandWidth = true;
            rowLayout.ChildForceExpandHeight = true;
            rowLayout.WithSpacing(ButtonSpacing);
            rowLayout.WithPadding(0);

            foreach (var keyDef in row)
            {
                string label = showShifted ? keyDef.Shifted : keyDef.Normal;
                char input = showShifted ? keyDef.InputShifted : keyDef.InputNormal;
                var btn = rowLayout.AddChild<ButtonComponent>();
                btn.Color = new Color(0.2f, 0.2f, 0.25f, 1f);
                var btnText = btn.AddChild<TextComponent>().ToFill().Inset(0.5f);
                btnText.Alignment = TextAlignmentOptions.Center;
                btnText.Color = new Color(0.95f, 0.95f, 0.95f, 1f);
                btnText.FontSize = 3f;
                btnText.Text = label;
                var capturedDef = keyDef;
                btn.OnClick += () => OnCharacterClick(m_shift ? capturedDef.InputShifted : capturedDef.InputNormal);
                m_keyButtons.Add(new KeyButtonInfo(keyDef, btn, btnText));
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

        m_shiftButton = bottomRow.AddChild<ButtonComponent>();
        m_shiftButton.Color = new Color(0.2f, 0.2f, 0.25f, 1f);
        m_capsButtonText = m_shiftButton.AddChild<TextComponent>().ToFill().Inset(0.5f);
        m_capsButtonText.Alignment = TextAlignmentOptions.Center;
        m_capsButtonText.Color = new Color(0.95f, 0.95f, 0.95f, 1f);
        m_capsButtonText.FontSize = 2.5f;
        m_capsButtonText.Text = "SHIFT";
        m_shiftButton.OnClick += OnShiftClick;

        var spaceBtn = bottomRow.AddChild<ButtonComponent>();
        spaceBtn.LayoutElement.flexibleWidth = 2;
        spaceBtn.Color = new Color(0.2f, 0.2f, 0.25f, 1f);
        var spaceBtnText = spaceBtn.AddChild<TextComponent>().ToFill().Inset(0.5f);
        spaceBtnText.Alignment = TextAlignmentOptions.Center;
        spaceBtnText.Color = new Color(0.95f, 0.95f, 0.95f, 1f);
        spaceBtnText.FontSize = 3f;
        spaceBtnText.Text = "_";
        spaceBtn.OnClick += () => OnCharacterClick(' ');

        var backBtn = bottomRow.AddChild<ButtonComponent>();
        backBtn.Color = new Color(0.2f, 0.2f, 0.25f, 1f);
        var backBtnText = backBtn.AddChild<TextComponent>().ToFill().Inset(0.5f);
        backBtnText.Alignment = TextAlignmentOptions.Center;
        backBtnText.Color = new Color(0.95f, 0.95f, 0.95f, 1f);
        backBtnText.FontSize = 3f;
        backBtnText.Text = "<";
        backBtn.OnClick += OnBackspaceClick;

        m_okButton = bottomRow.AddChild<ButtonComponent>();
        m_okButton.LayoutElement.preferredWidth = ButtonSize * 1.3f + ButtonSpacing;
        m_okButton.Color = new Color(0.2f, 0.5f, 0.2f, 1f);
        var okText = m_okButton.AddChild<TextComponent>().ToFill().Inset(0.5f);
        okText.Alignment = TextAlignmentOptions.Center;
        okText.Color = Color.white;
        okText.FontSize = 3f;
        okText.Text = "OK";
        m_okButton.OnClick += OnOkClick;
    }

    private void OnCharacterClick(char c)
    {
        if (m_inputBuffer.Length >= MaxLength)
            return;

        m_inputBuffer += c;
        m_popupDisplayText.Text = m_inputBuffer;
    }

    private void OnShiftClick()
    {
        m_shift = !m_shift;
        UpdateShiftVisuals();
    }

    private void UpdateShiftVisuals()
    {
        bool showShifted = m_shift;

        m_shiftButton.Color = m_shift
            ? new Color(0.35f, 0.35f, 0.5f, 1f)
            : new Color(0.2f, 0.2f, 0.25f, 1f);

        foreach (var info in m_keyButtons)
        {
            info.Text.Text = showShifted ? info.Definition.Shifted : info.Definition.Normal;
        }
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

    private class KeyButtonInfo
    {
        public readonly KeyDef Definition;
        public readonly ButtonComponent Button;
        public readonly TextComponent Text;

        public KeyButtonInfo(KeyDef def, ButtonComponent button, TextComponent text)
        {
            Definition = def;
            Button = button;
            Text = text;
        }
    }
}
