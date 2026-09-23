using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IPA.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VainSabers.Config;
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

    private ImageComponent _previewImage = null!;
    private UIComponent _previewGridRoot = null!;
    private readonly List<ImageComponent> _gridLines = new();

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
        UpdatePreviewTexture();
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
        RebuildPreviewGrid();
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
        UpdatePreviewTexture();
        RebuildPreviewGrid();
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
        
        _popupBackground = AddChild<RoundRectComponent>().ToBottomCenter().Move(0, 18f);
        _popupBackground.SizeDelta = new Vector2(30, 55);
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

        // Preview: image + grid overlay matching atlas cutting – use ImageComponent (premade, handles curved via CurvedCanvasSettingsHelper automatically)
        var previewContainer = popupLayout.AddChild<UIComponent>();
        previewContainer.LayoutElement.preferredHeight = 24f;
        previewContainer.LayoutElement.flexibleHeight = 0f;
        // Background for preview (dark checker) – RoundRectComponent handles curved via ImageView
        var previewBg = previewContainer.AddChild<RoundRectComponent>().ToFill();
        previewBg.Color = new Color(0.15f, 0.15f, 0.15f, 1f);
        previewBg.IsRaycastTarget = false;
        _previewImage = previewContainer.AddChild<ImageComponent>().ToFill().Inset(0.5f);
        _previewImage.Color = Color.white;
        _previewImage.PreserveAspect = false;
        _previewGridRoot = previewContainer.AddChild<UIComponent>().ToFill();
        // Ensure preview updates on dropdown change
        _dropdown.OnSelectionChanged += idx => { OnSelectionChanged?.Invoke(idx); UpdatePreviewTexture(); };

        _xInput = popupLayout.AddChild<FieldComponent>().WithPreferredHeight(4).WithLabel("Count X").SetComponent<NumberInputComponent>()
            .WithMinMaxStep(1f, 16f, 1f).WithValue(_atlasCount.x);
        _xInput.OnValueChanged += v =>
        {
            int iv = Mathf.RoundToInt(Mathf.Clamp(v, 1f, 16f));
            if (iv == Mathf.RoundToInt(_atlasCount.x)) return;
            _atlasCount.x = iv;
            OnAtlasXChanged?.Invoke(iv);
            RebuildPreviewGrid();
        };

        _yInput = popupLayout.AddChild<FieldComponent>().WithPreferredHeight(4).WithLabel("Count Y").SetComponent<NumberInputComponent>()
            .WithMinMaxStep(1f, 16f, 1f).WithValue(_atlasCount.y);
        _yInput.OnValueChanged += v =>
        {
            int iv = Mathf.RoundToInt(Mathf.Clamp(v, 1f, 16f));
            if (iv == Mathf.RoundToInt(_atlasCount.y)) return;
            _atlasCount.y = iv;
            OnAtlasYChanged?.Invoke(iv);
            RebuildPreviewGrid();
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

    private readonly Dictionary<string, Sprite> _previewSpriteCache = new();
    private static Sprite _emptySprite = null!;
    private static Sprite GetEmptySprite()
    {
        if (_emptySprite == null)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.clear);
            tex.Apply();
            _emptySprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, Vector4.zero, false);
        }
        return _emptySprite;
    }
    private void UpdatePreviewTexture()
    {
        if (_previewImage == null) return;
        string? selected = SelectedValue;
        if (string.IsNullOrEmpty(selected) || selected == "None")
        {
            _previewImage.Sprite = GetEmptySprite();
            _previewImage.Color = new Color(0.12f, 0.12f, 0.12f, 1f);
            return;
        }

        if (_previewSpriteCache.TryGetValue(selected!, out var cached))
        {
            _previewImage.Sprite = cached;
            _previewImage.Color = Color.white;
            return;
        }

        string path = Path.Combine(ConfigUtil.ConfigDir, selected!);
        if (!File.Exists(path))
        {
            _previewImage.Sprite = GetEmptySprite();
            _previewImage.Color = new Color(0.12f, 0.12f, 0.12f, 1f);
            return;
        }

        try
        {
            byte[] data = File.ReadAllBytes(path);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            if (tex.LoadImage(data))
            {
                var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, Vector4.zero, false);
                sprite.texture.wrapMode = TextureWrapMode.Clamp;
                _previewSpriteCache[selected!] = sprite;
                _previewImage.Sprite = sprite;
                _previewImage.Color = Color.white;
            }
            else
            {
                UnityEngine.Object.Destroy(tex);
                _previewImage.Sprite = GetEmptySprite();
            }
        }
        catch
        {
            _previewImage.Sprite = GetEmptySprite();
        }
    }

    private void RebuildPreviewGrid()
    {
        if (_previewGridRoot == null) return;
        // Clear previous lines
        for (int i = _previewGridRoot.transform.childCount - 1; i >= 0; i--)
            UnityEngine.Object.Destroy(_previewGridRoot.transform.GetChild(i).gameObject);
        _gridLines.Clear();

        int cols = Mathf.RoundToInt(_atlasCount.x);
        int rows = Mathf.RoundToInt(_atlasCount.y);
        if (cols <= 1 && rows <= 1) return;
        if (_previewGridRoot == null) return;

        // Grid lines are thin ImageComponents with semi-transparent white
        Color lineColor = new Color(1f, 1f, 1f, 0.45f);
        // Vertical lines (x divisions)
        for (int i = 1; i < cols; i++)
        {
            float t = (float)i / cols;
            var line = _previewGridRoot.AddChild<ImageComponent>().SetAnchors(new Vector2(t, 0f), new Vector2(t, 1f)).ClearOffsets();
            line.RectTransform.sizeDelta = new Vector2(0.2f, 0f);
            line.Color = lineColor;
            line.Type = Image.Type.Simple;
            _gridLines.Add(line);
        }
        // Horizontal lines (y divisions)
        for (int i = 1; i < rows; i++)
        {
            float t = (float)i / rows;
            var line = _previewGridRoot.AddChild<ImageComponent>().SetAnchors(new Vector2(0f, t), new Vector2(1f, t)).ClearOffsets();
            line.RectTransform.sizeDelta = new Vector2(0f, 0.2f);
            line.Color = lineColor;
            line.Type = Image.Type.Simple;
            _gridLines.Add(line);
        }

        var borderColor = new Color(1f, 1f, 1f, 0.25f);
    }

    private void Update()
    {
        if (_isPopupOpen)
        {
            // the grid lines should fade between black and white on a sine wave, a period of 0.5s
            float t = (Time.time % 0.5f) / 0.5f;
            Color a = Color.black;
            Color b = Color.white;
            for (int i = 0; i < _gridLines.Count; i++)
            {
                _gridLines[i].Color = Color.Lerp(a, b, Mathf.Sin(t * Mathf.PI * 2f) * 0.5f + 0.5f);
            }
        }
    }
}
