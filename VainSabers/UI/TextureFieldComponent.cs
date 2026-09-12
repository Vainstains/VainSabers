using System;
using System.Collections.Generic;
using System.Linq;
using IPA.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRUIControls;

namespace VainSabers.UI;

public class TextureFieldComponent : UIComponent
{
    private DropdownComponent _dropdown = null!;
    private TextButtonComponent _settingsButton = null!;
    private RoundRectComponent _popupBackground = null!;
    private Canvas _popupCanvas = null!;
    private VRGraphicRaycaster _popupRaycaster = null!;
    private ButtonComponent _popupBlocker = null!;

    private NumberInputComponent _xInput = null!;
    private NumberInputComponent _yInput = null!;
    private NumberInputComponent _speedInput = null!;
    private ToggleComponent _flipXToggle = null!;
    private ToggleComponent _flipYToggle = null!;

    private bool _isPopupOpen = false;
    // compact: float2 count, float3 speed+flips (y=flipX, z=flipY)
    private Vector2 _atlasCount = new Vector2(1, 1);
    private Vector3 _atlasSpeedFlip = new Vector3(1, 0, 0);

    public event Action<int>? OnSelectionChanged;
    public event Action<int>? OnAtlasXChanged;
    public event Action<int>? OnAtlasYChanged;
    public event Action<float>? OnAtlasSpeedChanged;
    public event Action<bool>? OnAtlasFlipXChanged;
    public event Action<bool>? OnAtlasFlipYChanged;

    public int SelectedIndex => _dropdown != null ? _dropdown.SelectedIndex : -1;
    public string? SelectedValue => _dropdown != null ? _dropdown.SelectedValue : null;
    public IReadOnlyList<string> Options => _dropdown != null ? _dropdown.Options : Array.Empty<string>();
    public bool IsPopupOpen => _isPopupOpen;

    public int AtlasX => Mathf.RoundToInt(_atlasCount.x);
    public int AtlasY => Mathf.RoundToInt(_atlasCount.y);
    public float AtlasSpeed => _atlasSpeedFlip.x;
    public bool AtlasFlipX => _atlasSpeedFlip.y > 0.5f;
    public bool AtlasFlipY => _atlasSpeedFlip.z > 0.5f;
    // compact accessors
    public Vector2 AtlasCount => _atlasCount;
    public Vector3 AtlasSpeedFlip => _atlasSpeedFlip;

    public void SetOptions(IEnumerable<string> options, int selectedIndex = 0)
    {
        _dropdown.SetOptions(options, selectedIndex);
    }

    public void SetAtlasValues(int x, int y, float speed, bool flipX = false, bool flipY = false)
    {
        SetAtlasValues(new Vector2(x, y), new Vector3(speed, flipX ? 1f : 0f, flipY ? 1f : 0f));
    }

    public void SetAtlasValues(Vector2 count, Vector3 speedFlip)
    {
        _atlasCount = new Vector2(Mathf.Clamp(count.x, 1, 16), Mathf.Clamp(count.y, 1, 16));
        _atlasSpeedFlip = new Vector3(Mathf.Clamp(speedFlip.x, 0f, 120f), speedFlip.y > 0.5f ? 1f : 0f, speedFlip.z > 0.5f ? 1f : 0f);
        if (_xInput != null)
            _xInput.SetValue(_atlasCount.x, false);
        if (_yInput != null)
            _yInput.SetValue(_atlasCount.y, false);
        if (_speedInput != null)
            _speedInput.SetValue(_atlasSpeedFlip.x, false);
        if (_flipXToggle != null)
            _flipXToggle.IsOn = _atlasSpeedFlip.y > 0.5f;
        if (_flipYToggle != null)
            _flipYToggle.IsOn = _atlasSpeedFlip.z > 0.5f;
    }

    public TextureFieldComponent WithOptions(IEnumerable<string> options, int selectedIndex = 0)
    {
        SetOptions(options, selectedIndex);
        return this;
    }

    public TextureFieldComponent WithAtlasValues(int x, int y, float speed, bool flipX = false, bool flipY = false)
    {
        SetAtlasValues(x, y, speed, flipX, flipY);
        return this;
    }

    public void OpenPopup()
    {
        if (_isPopupOpen)
            return;
        _isPopupOpen = true;
        _popupBackground.gameObject.SetActive(true);
        PopupStack.Register(_popupCanvas, _popupBlocker);
        // sync inputs (in case values changed externally before popup opened)
        _xInput.SetValue(_atlasCount.x, false);
        _yInput.SetValue(_atlasCount.y, false);
        _speedInput.SetValue(_atlasSpeedFlip.x, false);
        _flipXToggle.IsOn = _atlasSpeedFlip.y > 0.5f;
        _flipYToggle.IsOn = _atlasSpeedFlip.z > 0.5f;
    }

    public void ClosePopup()
    {
        if (!_isPopupOpen)
            return;
        _isPopupOpen = false;
        PopupStack.Unregister(_popupCanvas);
        _popupBackground.gameObject.SetActive(false);
        _popupBlocker.IsInteractable = false;
    }

    public void TogglePopup()
    {
        if (_isPopupOpen) ClosePopup();
        else OpenPopup();
    }

    private void OnBlockerClicked()
    {
        if (!PopupStack.IsTopmost(_popupCanvas))
            return;
        ClosePopup();
    }

    protected override void Init()
    {
        base.Init();

        var hLayout = AddChild<HorizontalLayoutGroupComponent>().ToFill();
        hLayout.ChildControlWidth = true;
        hLayout.ChildControlHeight = true;
        hLayout.ChildForceExpandWidth = false;
        hLayout.ChildForceExpandHeight = true;
        hLayout.WithSpacing(0.5f);
        hLayout.WithPadding(0);

        _dropdown = hLayout.AddChild<DropdownComponent>();
        _dropdown.LayoutElement.minWidth = 0;
        _dropdown.LayoutElement.preferredWidth = 0;
        _dropdown.LayoutElement.flexibleWidth = 1;
        _dropdown.OnSelectionChanged += idx => OnSelectionChanged?.Invoke(idx);

        _settingsButton = hLayout.AddChild<TextButtonComponent>().WithText("...");
        _settingsButton.LayoutElement.minWidth = 5;
        _settingsButton.LayoutElement.preferredWidth = 5;
        _settingsButton.LayoutElement.flexibleWidth = 0;
        _settingsButton.LayoutElement.minHeight = 0;
        _settingsButton.LayoutElement.preferredHeight = 4;
        _settingsButton.LayoutElement.flexibleHeight = 0;
        _settingsButton.Color = new Color(0.25f, 0.25f, 0.28f, 1f);
        _settingsButton.OnClick += TogglePopup;

        // blocker (full-screen transparent, behind popup)
        _popupBlocker = AddChild<ButtonComponent>().ToFill().Extend(200);
        _popupBlocker.Color = new Color(0, 0, 0, 0);
        _popupBlocker.OnClick += OnBlockerClicked;
        _popupBlocker.IsInteractable = false;

        // popup background – centered above the field
        _popupBackground = AddChild<RoundRectComponent>().ToBottomCenter().Move(0, 18f);
        _popupBackground.SizeDelta = new Vector2(30, 26);
        _popupBackground.Color = new Color(0.09f, 0.09f, 0.09f, 1f);
        _popupBackground.IsRaycastTarget = true;
        _popupBackground.gameObject.SetActive(false);

        _popupCanvas = _popupBackground.gameObject.AddComponent<Canvas>();
        _popupCanvas.renderMode = RenderMode.WorldSpace;
        _popupCanvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord2;
        _popupCanvas.overrideSorting = true;
        _popupCanvas.sortingOrder = 20;

        var scaler = _popupBackground.gameObject.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 3.44f;
        scaler.referencePixelsPerUnit = 10;

        _popupRaycaster = _popupBackground.gameObject.AddComponent<VRGraphicRaycaster>();
        _popupRaycaster.SetField("_physicsRaycaster", UIResources.Raycaster);

        var popupLayout = _popupBackground.AddChild<VerticalLayoutGroupComponent>().ToFill();
        popupLayout.WithPadding(1).WithSpacing(0.5f);
        popupLayout.ChildControlWidth = true;
        popupLayout.ChildControlHeight = true;
        popupLayout.ChildForceExpandWidth = true;
        popupLayout.ChildForceExpandHeight = false;

        var header = popupLayout.AddChild<TextComponent>();
        header.LayoutElement.preferredHeight = 3.5f;
        header.Alignment = TextAlignmentOptions.Center;
        header.FontSize = 3.2f;
        header.Color = new Color(0.85f, 0.85f, 0.85f, 1f);
        header.Text = "Atlas";

        _xInput = popupLayout.AddChild<FieldComponent>().WithPreferredHeight(4).WithLabel("Count X").SetComponent<NumberInputComponent>()
            .WithMinMaxStep(1f, 16f, 1f).WithValue(_atlasCount.x);
        _xInput.OnValueChanged += v =>
        {
            int iv = Mathf.RoundToInt(Mathf.Clamp(v, 1f, 16f));
            if (iv == Mathf.RoundToInt(_atlasCount.x)) return;
            _atlasCount.x = iv;
            OnAtlasXChanged?.Invoke(iv);
        };

        _yInput = popupLayout.AddChild<FieldComponent>().WithPreferredHeight(4).WithLabel("Count Y").SetComponent<NumberInputComponent>()
            .WithMinMaxStep(1f, 16f, 1f).WithValue(_atlasCount.y);
        _yInput.OnValueChanged += v =>
        {
            int iv = Mathf.RoundToInt(Mathf.Clamp(v, 1f, 16f));
            if (iv == Mathf.RoundToInt(_atlasCount.y)) return;
            _atlasCount.y = iv;
            OnAtlasYChanged?.Invoke(iv);
        };

        _speedInput = popupLayout.AddChild<FieldComponent>().WithPreferredHeight(4).WithLabel("Speed (fps)").SetComponent<NumberInputComponent>()
            .WithMinMaxStep(0f, 120f, 1f).WithSensitivityCoef(5f).WithValue(_atlasSpeedFlip.x);
        _speedInput.OnValueChanged += v =>
        {
            float fv = Mathf.Clamp(v, 0f, 120f);
            if (Mathf.Abs(fv - _atlasSpeedFlip.x) < 0.001f) return;
            _atlasSpeedFlip.x = fv;
            OnAtlasSpeedChanged?.Invoke(fv);
        };

        _flipXToggle = popupLayout.AddChild<FieldComponent>().WithPreferredHeight(4).WithLabel("Reverse X").SetComponent<ToggleComponent>()
            .WithValue(_atlasSpeedFlip.y > 0.5f);
        _flipXToggle.OnValueChanged += v =>
        {
            bool cur = _atlasSpeedFlip.y > 0.5f;
            if (v == cur) return;
            _atlasSpeedFlip.y = v ? 1f : 0f;
            OnAtlasFlipXChanged?.Invoke(v);
        };

        _flipYToggle = popupLayout.AddChild<FieldComponent>().WithPreferredHeight(4).WithLabel("Reverse Y").SetComponent<ToggleComponent>()
            .WithValue(_atlasSpeedFlip.z > 0.5f);
        _flipYToggle.OnValueChanged += v =>
        {
            bool cur = _atlasSpeedFlip.z > 0.5f;
            if (v == cur) return;
            _atlasSpeedFlip.z = v ? 1f : 0f;
            OnAtlasFlipYChanged?.Invoke(v);
        };
    }
}
