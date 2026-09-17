using System;
using System.Collections.Generic;
using System.Linq;
using HMUI;
using IPA.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VainSabers.Data;
using VainSabers.Helpers;
using VainSabers.Menu;
using VRUIControls;

namespace VainSabers.UI;

public enum GradientMode
{
    Float,
    Color
}

public class GradientInputComponent : UIComponent
{
    private const float PopupWidth = 42f;
    private const float PopupHeight = 42f;
    private const float GradientPreviewHeight = 4.5f;
    private const float TrackHeight = 4f;
    private const float FieldHeight = 4f;
    private const float ButtonSpacing = 0.5f;
    private const float DiamondBaseSize = 2.2f;
    private const float DiamondSelectedBonus = 0.8f;
    private const float DiamondHitSize = 6f;
    private const float DiamondDragDeadZoneDegrees = 2f;
    private const float DiamondDragSensitivity = 0.005f;

    private static readonly Color HeaderBaseColor = new Color(0.15f, 0.15f, 0.15f, 1f);
    private static readonly Color PopupBaseColor = new Color(0.07f, 0.07f, 0.07f, 1f);

    private ButtonComponent m_headerButton = null!;
    private RawImageComponent m_headerPreview = null!;

    private ButtonComponent m_popupBlocker = null!;
    private RoundRectComponent m_popupBackground = null!;
    private Canvas m_popupCanvas = null!;
    private VRGraphicRaycaster m_popupRaycaster = null!;

    private VerticalLayoutGroupComponent m_popupLayout = null!;
    private RawImageComponent m_popupPreview = null!;
    private RoundRectComponent m_trackContainer = null!;

    private NumberInputComponent m_timeInput = null!;
    private DropdownComponent m_easingDropdown = null!;
    private VerticalLayoutGroupComponent m_valueFieldsContainer = null!;

    private NumberInputComponent? m_floatValueInput;
    private NumberInputComponent? m_rInput;
    private NumberInputComponent? m_gInput;
    private NumberInputComponent? m_bInput;

    private ButtonComponent m_plusButton = null!;
    private ButtonComponent m_minusButton = null!;
    private TextButtonComponent m_cancelButton = null!;
    private TextButtonComponent m_acceptButton = null!;

    private readonly List<DiamondMarker> m_markers = new();
    private readonly List<ColorGradientKey> m_backupKeys = new();

    private ColorGradient m_gradient = null!;
    private GradientMode m_mode = GradientMode.Color;
    private ColorGradientKey? m_selectedKey;
    private bool m_isPopupOpen;
    private bool m_updatingControls;
    private bool m_suppressDropdownEvent;

    private float m_floatMin = 0f;
    private float m_floatMax = 1f;
    private float m_floatStep = 0.01f;

    public event Action? OnGradientChanged;

    public ColorGradient Gradient => m_gradient;
    public GradientMode Mode => m_mode;
    public ColorGradientKey? SelectedKey => m_selectedKey;
    public bool IsPopupOpen => m_isPopupOpen;

    public GradientInputComponent WithMode(GradientMode mode)
    {
        m_mode = mode;
        if (m_valueFieldsContainer != null)
            RebuildValueControls();
        RefreshPreviews();
        return this;
    }

    public GradientInputComponent WithFloatRange(float min, float max, float step)
    {
        m_floatMin = min;
        m_floatMax = max;
        m_floatStep = Mathf.Max(0.0001f, step);
        if (m_floatValueInput != null)
            m_floatValueInput.SetMinMaxStep(m_floatMin, m_floatMax, m_floatStep);
        // Rebuild to apply to new controls if needed
        return this;
    }

    public GradientInputComponent WithGradient(ColorGradient gradient)
    {
        SetGradient(gradient, m_mode);
        return this;
    }

    public GradientInputComponent WithGradient(ColorGradient gradient, GradientMode mode)
    {
        SetGradient(gradient, mode);
        return this;
    }

    public void SetGradient(ColorGradient gradient, GradientMode mode)
    {
        m_gradient = gradient ?? throw new ArgumentNullException(nameof(gradient));
        m_mode = mode;
        if (m_gradient.Keys.Count == 0)
        {
            EnsureAtLeastOneKey();
        }
        // select first if needed
        if (m_selectedKey == null || !m_gradient.Keys.Contains(m_selectedKey))
            m_selectedKey = m_gradient.Keys.OrderBy(k => k.Time).FirstOrDefault();

        if (m_valueFieldsContainer != null)
            RebuildValueControls();

        RefreshAll();
    }

    public void SetMode(GradientMode mode)
    {
        m_mode = mode;
        RebuildValueControls();
        RefreshPreviews();
        RefreshMarkers();
        RefreshControls();
    }

    public void OpenPopup()
    {
        if (m_isPopupOpen)
            return;
        if (m_gradient == null)
        {
            m_gradient = new ColorGradient();
            EnsureAtLeastOneKey();
            m_selectedKey = m_gradient.Keys[0];
        }

        // backup
        m_backupKeys.Clear();
        foreach (var k in m_gradient.Keys)
            m_backupKeys.Add(new ColorGradientKey(k.Time, k.Color) { Easing = k.Easing });

        if (m_gradient.Keys.Count == 0)
            EnsureAtLeastOneKey();

        if (m_selectedKey == null || !m_gradient.Keys.Contains(m_selectedKey))
            m_selectedKey = m_gradient.Keys.OrderBy(k => k.Time).First();

        m_isPopupOpen = true;
        m_popupBackground.gameObject.SetActive(true);
        PopupStack.Register(m_popupCanvas, m_popupBlocker);
        m_headerButton.gameObject.SetActive(false);

        RebuildValueControls();
        RebuildMarkers();
        RefreshPreviews();
        RefreshControls();
        UpdatePlusMinusInteractability();
    }

    public void ClosePopup()
    {
        if (!m_isPopupOpen)
            return;
        m_isPopupOpen = false;
        PopupStack.Unregister(m_popupCanvas);
        m_popupBackground.gameObject.SetActive(false);
        m_popupBlocker.IsInteractable = false;
        m_headerButton.gameObject.SetActive(true);
        RefreshHeaderPreview();
    }

    public void Cancel()
    {
        if (!m_isPopupOpen)
            return;
        // Only allow cancel via click-away if topmost
        // Programmatic Cancel (e.g., button press) is always allowed via Accept/Cancel buttons
        // but blocker click is gated via OnBlockerClicked
        // revert
        m_gradient.Keys.Clear();
        foreach (var k in m_backupKeys)
            m_gradient.Keys.Add(new ColorGradientKey(k.Time, k.Color) { Easing = k.Easing });
        m_gradient.SetDirty();
        m_selectedKey = m_gradient.Keys.Count > 0 ? m_gradient.Keys.OrderBy(k => k.Time).First() : null;
        OnGradientChanged?.Invoke();
        ClosePopup();
        RefreshAll();
    }

    public void Accept()
    {
        if (!m_isPopupOpen)
            return;
        ClosePopup();
        // keep current gradient, ensure dirty sorted
        m_gradient.SetDirty();
        OnGradientChanged?.Invoke();
        RefreshHeaderPreview();
    }

    private void OnBlockerClicked()
    {
        if (!PopupStack.IsTopmost(m_popupCanvas))
            return;
        Cancel();
    }

    protected override void Init()
    {
        base.Init();

        if (m_gradient == null)
            m_gradient = new ColorGradient();

        // Header button - fills parent
        m_headerButton = AddChild<ButtonComponent>().ToFill();
        m_headerButton.InstantClick = false;
        m_headerButton.Color = HeaderBaseColor;
        m_headerButton.OnClick += OnHeaderClick;

        // Header preview fills button with small inset
        m_headerPreview = m_headerButton.AddChild<RawImageComponent>().ToFill().Inset(0.3f);
        m_headerPreview.RaycastTarget = false;

        // Blocker
        m_popupBlocker = AddChild<ButtonComponent>().ToFill().Extend(200);
        m_popupBlocker.Color = new Color(0, 0, 0, 0);
        m_popupBlocker.OnClick += OnBlockerClicked;
        m_popupBlocker.IsInteractable = false;

        // Popup background centered above header
        m_popupBackground = AddChild<RoundRectComponent>().ToBottomCenter().Move(0, PopupHeight * 0.5f);
        m_popupBackground.SizeDelta = new Vector2(PopupWidth, PopupHeight);
        m_popupBackground.Color = PopupBaseColor;
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

        // Popup vertical layout
        m_popupLayout = m_popupBackground.AddChild<VerticalLayoutGroupComponent>().ToFill();
        m_popupLayout.WithPadding(2).WithSpacing(ButtonSpacing);
        m_popupLayout.ChildControlWidth = true;
        m_popupLayout.ChildControlHeight = true;
        m_popupLayout.ChildForceExpandWidth = true;
        m_popupLayout.ChildForceExpandHeight = false;

        // Gradient preview at top
        var previewContainer = m_popupLayout.AddChild<RoundRectComponent>().WithPreferredHeight(GradientPreviewHeight);
        previewContainer.LayoutElement.flexibleHeight = 0;
        previewContainer.Color = new Color(0.2f, 0.2f, 0.2f, 1f);
        previewContainer.IsRaycastTarget = false;
        m_popupPreview = previewContainer.AddChild<RawImageComponent>().ToFill().Inset(0.3f);
        m_popupPreview.RaycastTarget = false;

        // Track for diamond markers just below
        m_trackContainer = m_popupLayout.AddChild<RoundRectComponent>().WithPreferredHeight(TrackHeight);
        m_trackContainer.LayoutElement.flexibleHeight = 0;
        m_trackContainer.Color = new Color(0.12f, 0.12f, 0.12f, 1f);
        m_trackContainer.IsRaycastTarget = false;

        // Time field
        var timeField = m_popupLayout.AddChild<FieldComponent>().WithPreferredHeight(FieldHeight);
        timeField.WithLabel("T");
        m_timeInput = timeField.SetComponent<NumberInputComponent>();
        m_timeInput.SetMinMaxStep(0f, 1f, 0.01f);
        m_timeInput.OnValueChanged += OnTimeInputChanged;

        // Easing field
        var easingField = m_popupLayout.AddChild<FieldComponent>().WithPreferredHeight(FieldHeight);
        easingField.WithLabel("Easing");
        m_easingDropdown = easingField.SetComponent<DropdownComponent>();
        m_easingDropdown.SetEnumOptions(Easing.Linear);
        m_easingDropdown.OnSelectionChanged += OnEasingChanged;

        // Value fields container - dynamic
        m_valueFieldsContainer = m_popupLayout.AddChild<VerticalLayoutGroupComponent>().WithPreferredHeight(FieldHeight);
        m_valueFieldsContainer.LayoutElement.flexibleHeight = 0;
        m_valueFieldsContainer.ChildControlWidth = true;
        m_valueFieldsContainer.ChildControlHeight = true;
        m_valueFieldsContainer.ChildForceExpandWidth = true;
        m_valueFieldsContainer.ChildForceExpandHeight = false;
        m_valueFieldsContainer.WithSpacing(ButtonSpacing);
        RebuildValueControls();

        // Plus / Minus row - only + and - as spec
        var plusMinusRow = m_popupLayout.AddChild<HorizontalLayoutGroupComponent>().WithPreferredHeight(FieldHeight);
        plusMinusRow.LayoutElement.flexibleHeight = 0;
        plusMinusRow.ChildControlWidth = true;
        plusMinusRow.ChildControlHeight = true;
        plusMinusRow.ChildForceExpandWidth = true;
        plusMinusRow.ChildForceExpandHeight = true;
        plusMinusRow.WithSpacing(ButtonSpacing);
        plusMinusRow.WithPadding(0);

        m_plusButton = plusMinusRow.AddChild<ButtonComponent>();
        m_plusButton.Color = new Color(0.2f, 0.2f, 0.25f, 1f);
        var plusText = m_plusButton.AddChild<TextComponent>().ToFill();
        plusText.Alignment = TextAlignmentOptions.Center;
        plusText.Color = Color.white;
        plusText.FontSize = 4f;
        plusText.Text = "+";
        m_plusButton.OnClick += OnPlusClicked;

        m_minusButton = plusMinusRow.AddChild<ButtonComponent>();
        m_minusButton.Color = new Color(0.2f, 0.2f, 0.25f, 1f);
        var minusText = m_minusButton.AddChild<TextComponent>().ToFill();
        minusText.Alignment = TextAlignmentOptions.Center;
        minusText.Color = Color.white;
        minusText.FontSize = 4f;
        minusText.Text = "-";
        m_minusButton.OnClick += OnMinusClicked;

        // Cancel / Accept at very bottom
        var bottomRow = m_popupLayout.AddChild<HorizontalLayoutGroupComponent>().WithPreferredHeight(FieldHeight);
        bottomRow.LayoutElement.flexibleHeight = 0;
        bottomRow.ChildControlWidth = true;
        bottomRow.ChildControlHeight = true;
        bottomRow.ChildForceExpandWidth = true;
        bottomRow.ChildForceExpandHeight = true;
        bottomRow.WithSpacing(ButtonSpacing);
        bottomRow.WithPadding(0);

        m_cancelButton = bottomRow.AddChild<TextButtonComponent>();
        m_cancelButton.Color = new Color(0.5f, 0.2f, 0.2f, 1f);
        m_cancelButton.WithText("Cancel");
        m_cancelButton.OnClick += Cancel;

        m_acceptButton = bottomRow.AddChild<TextButtonComponent>();
        m_acceptButton.Color = new Color(0.2f, 0.5f, 0.2f, 1f);
        m_acceptButton.WithText("Accept");
        m_acceptButton.OnClick += Accept;

        RefreshAll();
    }

    private void OnHeaderClick()
    {
        OpenPopup();
    }

    private void EnsureAtLeastOneKey()
    {
        if (m_gradient.Keys.Count > 0)
            return;
        if (m_mode == GradientMode.Float)
        {
            float mid = (m_floatMin + m_floatMax) * 0.5f;
            float v = Mathf.Clamp(mid, m_floatMin, m_floatMax);
            // Store as grayscale; preview will handle remapping
            m_gradient.Keys.Add(new ColorGradientKey(0f, new Color(v, v, v, 1f)));
        }
        else
            m_gradient.Keys.Add(new ColorGradientKey(0f, Color.white));
        m_gradient.SetDirty();
    }

    private void RebuildValueControls()
    {
        if (m_valueFieldsContainer == null)
            return;

        // clean previous
        foreach (Transform child in m_valueFieldsContainer.transform)
            Destroy(child.gameObject);
        m_floatValueInput = null;
        m_rInput = null;
        m_gInput = null;
        m_bInput = null;

        if (m_mode == GradientMode.Float)
        {
            m_valueFieldsContainer.LayoutElement.preferredHeight = FieldHeight;
            var field = m_valueFieldsContainer.AddChild<FieldComponent>().WithPreferredHeight(FieldHeight);
            field.WithLabel("Value");
            m_floatValueInput = field.SetComponent<NumberInputComponent>();
            m_floatValueInput.SetMinMaxStep(m_floatMin, m_floatMax, m_floatStep);
            m_floatValueInput.OnValueChanged += OnFloatValueChanged;
        }
        else
        {
            m_valueFieldsContainer.LayoutElement.preferredHeight = FieldHeight * 3 + ButtonSpacing * 2;
            var rField = m_valueFieldsContainer.AddChild<FieldComponent>().WithPreferredHeight(FieldHeight);
            rField.WithLabel("R");
            m_rInput = rField.SetComponent<NumberInputComponent>();
            m_rInput.SetMinMaxStep(-1f, 1f, 0.005f);
            m_rInput.OnValueChanged += OnRChanged;

            var gField = m_valueFieldsContainer.AddChild<FieldComponent>().WithPreferredHeight(FieldHeight);
            gField.WithLabel("G");
            m_gInput = gField.SetComponent<NumberInputComponent>();
            m_gInput.SetMinMaxStep(-1f, 1f, 0.005f);
            m_gInput.OnValueChanged += OnGChanged;

            var bField = m_valueFieldsContainer.AddChild<FieldComponent>().WithPreferredHeight(FieldHeight);
            bField.WithLabel("B");
            m_bInput = bField.SetComponent<NumberInputComponent>();
            m_bInput.SetMinMaxStep(-1f, 1f, 0.005f);
            m_bInput.OnValueChanged += OnBChanged;
        }
        // refresh controls if popup open
        if (m_isPopupOpen)
            RefreshControls();
    }

    private void RebuildMarkers()
    {
        foreach (var m in m_markers)
            if (m != null)
                Destroy(m.gameObject);
        m_markers.Clear();

        // sort for stable visual ordering but keep reference
        m_gradient.Keys.Sort((a, b) => a.Time.CompareTo(b.Time));

        foreach (var key in m_gradient.Keys)
        {
            var marker = m_trackContainer.AddChild<DiamondMarker>();
            marker.Setup(key, this, m_trackContainer);
            m_markers.Add(marker);
        }
        RefreshMarkersSelection();
    }

    private void RefreshMarkers()
    {
        foreach (var m in m_markers)
            m.Refresh();
        RefreshMarkersSelection();
    }

    private void RefreshMarkersSelection()
    {
        foreach (var m in m_markers)
            m.SetSelected(m.Key == m_selectedKey);
    }

    private void RefreshControls()
    {
        if (m_selectedKey == null)
        {
            // no selection: disable? For now set defaults and disable plus/minus handled separately
            UpdatePlusMinusInteractability();
            return;
        }

        m_updatingControls = true;
        m_suppressDropdownEvent = true;

        m_timeInput.SetValue(m_selectedKey.Time, false);

        // easing dropdown
        int easingIndex = (int)m_selectedKey.Easing;
        if (easingIndex >= 0 && easingIndex < m_easingDropdown.Options.Count)
            m_easingDropdown.SelectedIndex = easingIndex;

        if (m_mode == GradientMode.Float)
        {
            if (m_floatValueInput != null)
                m_floatValueInput.SetValue(m_selectedKey.Color.r, false);
        }
        else
        {
            if (m_rInput != null) m_rInput.SetValue(m_selectedKey.Color.r, false);
            if (m_gInput != null) m_gInput.SetValue(m_selectedKey.Color.g, false);
            if (m_bInput != null) m_bInput.SetValue(m_selectedKey.Color.b, false);
        }

        m_updatingControls = false;
        m_suppressDropdownEvent = false;
        UpdatePlusMinusInteractability();
    }

    private void UpdatePlusMinusInteractability()
    {
        if (m_minusButton != null)
            m_minusButton.IsInteractable = m_gradient != null && m_gradient.Keys.Count > 1 && m_selectedKey != null;
        if (m_plusButton != null)
            m_plusButton.IsInteractable = m_gradient != null;
    }

    private void RefreshPreviews()
    {
        if (m_gradient == null)
            return;
        // ensure dirty handling done via GetGradientTexture which sorts and updates
        Texture2D tex = m_gradient.GetGradientTexture();
        if (m_headerPreview != null)
            m_headerPreview.Texture = tex;
        if (m_popupPreview != null)
            m_popupPreview.Texture = tex;

        // update marker colors
        foreach (var m in m_markers)
            m.RefreshColor();
    }

    private void RefreshHeaderPreview()
    {
        if (m_gradient == null || m_headerPreview == null)
            return;
        m_headerPreview.Texture = m_gradient.GetGradientTexture();
    }

    private void RefreshAll()
    {
        RefreshPreviews();
        RefreshControls();
        UpdatePlusMinusInteractability();
    }

    // Control callbacks
    private void OnTimeInputChanged(float value)
    {
        if (m_updatingControls || m_selectedKey == null)
            return;
        float clamped = Mathf.Clamp01(value);
        m_selectedKey.Time = clamped;
        m_gradient.SetDirty();
        // update marker position
        foreach (var m in m_markers)
            if (m.Key == m_selectedKey)
                m.RefreshPosition();
        // sort and maybe rebuild markers order? keep positions
        m_gradient.Keys.Sort((a, b) => a.Time.CompareTo(b.Time));
        RefreshPreviews();
        OnGradientChanged?.Invoke();
    }

    private void OnEasingChanged(int index)
    {
        if (m_suppressDropdownEvent || m_updatingControls || m_selectedKey == null)
            return;
        if (index < 0 || index >= System.Enum.GetValues(typeof(Easing)).Length)
            return;
        m_selectedKey.Easing = (Easing)index;
        m_gradient.SetDirty();
        RefreshPreviews();
        OnGradientChanged?.Invoke();
    }

    private void OnFloatValueChanged(float value)
    {
        if (m_updatingControls || m_selectedKey == null)
            return;
        float clamped = Mathf.Clamp(value, m_floatMin, m_floatMax);
        m_selectedKey.Color = new Color(clamped, clamped, clamped, 1f);
        m_gradient.SetDirty();
        RefreshPreviews();
        OnGradientChanged?.Invoke();
    }

    private void OnRChanged(float value)
    {
        if (m_updatingControls || m_selectedKey == null)
            return;
        var c = m_selectedKey.Color;
        c.r = Mathf.Clamp(value, -1f, 1f);
        m_selectedKey.Color = c;
        m_gradient.SetDirty();
        RefreshPreviews();
        OnGradientChanged?.Invoke();
    }

    private void OnGChanged(float value)
    {
        if (m_updatingControls || m_selectedKey == null)
            return;
        var c = m_selectedKey.Color;
        c.g = Mathf.Clamp(value, -1f, 1f);
        m_selectedKey.Color = c;
        m_gradient.SetDirty();
        RefreshPreviews();
        OnGradientChanged?.Invoke();
    }

    private void OnBChanged(float value)
    {
        if (m_updatingControls || m_selectedKey == null)
            return;
        var c = m_selectedKey.Color;
        c.b = Mathf.Clamp(value, -1f, 1f);
        m_selectedKey.Color = c;
        m_gradient.SetDirty();
        RefreshPreviews();
        OnGradientChanged?.Invoke();
    }

    private void OnPlusClicked()
    {
        if (m_gradient == null)
            return;
        Color newColor;
        float newTime;
        Easing newEasing = Easing.Linear;

        if (m_selectedKey != null)
        {
            newColor = m_selectedKey.Color;
            newEasing = m_selectedKey.Easing;
            newTime = Mathf.Clamp01(m_selectedKey.Time + 0.05f);
            // if time collides, try to find free slot slightly offset
            int attempts = 0;
            while (m_gradient.Keys.Any(k => Mathf.Abs(k.Time - newTime) < 0.001f) && attempts < 20)
            {
                newTime = Mathf.Clamp01(newTime + 0.01f);
                attempts++;
                if (newTime >= 0.999f)
                    newTime = Mathf.Clamp01(m_selectedKey.Time - 0.05f - attempts * 0.01f);
            }
        }
        else
        {
            newColor = m_mode == GradientMode.Float ? new Color(0.5f, 0.5f, 0.5f, 1f) : Color.white;
            newTime = 0.5f;
        }

        var newKey = new ColorGradientKey(newTime, newColor) { Easing = newEasing };
        m_gradient.Keys.Add(newKey);
        m_gradient.Keys.Sort((a, b) => a.Time.CompareTo(b.Time));
        m_gradient.SetDirty();
        m_selectedKey = newKey;
        RebuildMarkers();
        RefreshPreviews();
        RefreshControls();
        OnGradientChanged?.Invoke();
    }

    private void OnMinusClicked()
    {
        if (m_gradient == null || m_selectedKey == null)
            return;
        if (m_gradient.Keys.Count <= 1)
            return;

        int idx = m_gradient.Keys.IndexOf(m_selectedKey);
        m_gradient.Keys.Remove(m_selectedKey);
        m_gradient.SetDirty();

        // pick nearest
        if (m_gradient.Keys.Count > 0)
        {
            idx = Mathf.Clamp(idx, 0, m_gradient.Keys.Count - 1);
            m_selectedKey = m_gradient.Keys[idx];
        }
        else
        {
            m_selectedKey = null;
        }

        RebuildMarkers();
        RefreshPreviews();
        RefreshControls();
        OnGradientChanged?.Invoke();
    }

    internal void SelectKey(ColorGradientKey key)
    {
        if (key == null)
            return;
        m_selectedKey = key;
        RefreshMarkersSelection();
        RefreshControls();
    }

    internal void OnKeyTimeDragged(ColorGradientKey key)
    {
        if (key != m_selectedKey)
        {
            // dragging a non-selected key: select it?
            // keep selected as is? But spec says keyframe indicators can be clicked to select, dragged left-right.
            // dragging should imply selection, so select it
            m_selectedKey = key;
            RefreshMarkersSelection();
        }

        m_updatingControls = true;
        if (m_timeInput != null)
            m_timeInput.SetValue(key.Time, false);
        m_updatingControls = false;

        m_gradient.SetDirty();
        RefreshPreviews();
        OnGradientChanged?.Invoke();
    }

    internal float GetTimeFromPointer(PointerEventData eventData)
    {
        if (m_trackContainer == null)
            return 0.5f;
        var rect = m_trackContainer.RectTransform;
        // Try multiple cameras for VR / WorldSpace robustness
        var cams = new[] { eventData.pressEventCamera, eventData.enterEventCamera, Camera.main };
        foreach (var cam in cams)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, cam, out Vector2 local))
            {
                float width = rect.rect.width;
                if (width < 0.001f)
                    width = PopupWidth - 4f;
                float normalized = (local.x + width * 0.5f) / width;
                return Mathf.Clamp01(normalized);
            }
        }
        // Final fallback with null camera (works for ScreenSpace)
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, null, out Vector2 localNull))
        {
            float width = rect.rect.width;
            if (width < 0.001f)
                width = PopupWidth - 4f;
            float normalized = (localNull.x + width * 0.5f) / width;
            return Mathf.Clamp01(normalized);
        }
        // Delta fallback - nudge based on delta if absolute failed
        if (eventData.delta.sqrMagnitude > 0.01f)
        {
            float width = rect.rect.width;
            if (width < 0.001f) width = PopupWidth - 4f;
            // use selected key time + delta proportion
            if (m_selectedKey != null)
                return Mathf.Clamp01(m_selectedKey.Time + eventData.delta.x / width);
        }
        return 0.5f;
    }

    private class DiamondMarker : UIComponent, IPointerDownHandler
    {
        private ImageView m_imageView = null!;
        private ImageView m_colorView = null!;
        private RectTransform m_colorRect = null!;
        private ImageView m_hitView = null!;
        public ColorGradientKey Key { get; private set; } = null!;
        private GradientInputComponent m_owner = null!;
        private RoundRectComponent m_track = null!;
        private bool m_isSelected;
        private ControllerYawDragHandler? m_yawHandler;
        private float m_dragStartTime;

        public void Setup(ColorGradientKey key, GradientInputComponent owner, RoundRectComponent track)
        {
            Key = key;
            m_owner = owner;
            m_track = track;
            Refresh();
            SetSelected(owner.SelectedKey == key);
        }

        public void Refresh()
        {
            RefreshPosition();
            RefreshColor();
            RefreshSize();
        }

        public void RefreshPosition()
        {
            if (Key == null)
                return;
            // use anchor positioning so it auto maps 0-1 across track width
            RectTransform.anchorMin = new Vector2(Key.Time, 0.5f);
            RectTransform.anchorMax = new Vector2(Key.Time, 0.5f);
            RectTransform.pivot = new Vector2(0.5f, 0.5f);
            RectTransform.anchoredPosition = Vector2.zero;
        }

        public void RefreshColor()
        {
            if (m_imageView == null || Key == null)
                return;
            // Outer always white half-opacity to resolve gray-on-gray
            m_imageView.color = new Color(1f, 1f, 1f, 0.5f);
            if (m_colorView != null)
                m_colorView.color = Key.Color;
        }

        public void RefreshSize()
        {
            float size = m_isSelected ? DiamondBaseSize + DiamondSelectedBonus : DiamondBaseSize;
            SizeDelta = new Vector2(size, size);
            RectTransform.localEulerAngles = new Vector3(0, 0, 45f);

            if (m_colorRect != null)
            {
                float inner = Mathf.Max(0.5f, size - 0.5f);
                m_colorRect.sizeDelta = new Vector2(inner, inner);
            }
        }

        public void SetSelected(bool selected)
        {
            if (m_isSelected == selected)
                return;
            m_isSelected = selected;
            RefreshSize();
            // Keep outer white half-opacity; inner already shows real color
        }

        protected override void Init()
        {
            base.Init();
            // Outer diamond - white half opacity border
            m_imageView = gameObject.RequireComponent<ImageView>();
            m_imageView.raycastTarget = false; // hit handled by dedicated hit area
            m_imageView.sprite = UIResources.LoadSpriteFromResource("VainSabers.ui_round.png", borderRatio: 0.5f);
            m_imageView.type = Image.Type.Sliced;
            m_imageView.material = UIResources.NoGlowMat;
            m_imageView.color = new Color(1f, 1f, 1f, 0.5f);

            // Inner color diamond inset 0.5 units
            var colorGo = new GameObject("Color");
            colorGo.transform.SetParent(transform, false);
            m_colorRect = colorGo.AddComponent<RectTransform>();
            m_colorRect.anchorMin = new Vector2(0.5f, 0.5f);
            m_colorRect.anchorMax = new Vector2(0.5f, 0.5f);
            m_colorRect.pivot = new Vector2(0.5f, 0.5f);
            m_colorRect.anchoredPosition = Vector2.zero;
            // size set in RefreshSize to be outer -1.0
            m_colorView = colorGo.AddComponent<ImageView>();
            m_colorView.sprite = UIResources.LoadSpriteFromResource("VainSabers.ui_round.png", borderRatio: 0.5f);
            m_colorView.type = Image.Type.Sliced;
            m_colorView.material = UIResources.NoGlowMat;
            m_colorView.color = Color.white;
            m_colorView.raycastTarget = false;

            // Larger invisible hit area to make dragging easy (especially in VR)
            var hitGo = new GameObject("HitArea");
            hitGo.transform.SetParent(transform, false);
            var hitRect = hitGo.AddComponent<RectTransform>();
            hitRect.anchorMin = new Vector2(0.5f, 0.5f);
            hitRect.anchorMax = new Vector2(0.5f, 0.5f);
            hitRect.pivot = new Vector2(0.5f, 0.5f);
            hitRect.sizeDelta = new Vector2(DiamondHitSize, DiamondHitSize);
            hitRect.anchoredPosition = Vector2.zero;
            m_hitView = hitGo.AddComponent<ImageView>();
            m_hitView.sprite = UIResources.LoadSpriteFromResource("VainSabers.ui_round.png", borderRatio: 0.5f);
            m_hitView.type = Image.Type.Sliced;
            m_hitView.material = UIResources.NoGlowMat;
            m_hitView.color = new Color(1, 1, 1, 0.01f); // near-transparent but raycastable
            m_hitView.raycastTarget = true;

            // Common rotation-based drag handler (reused with NumberInputComponent)
            m_yawHandler = gameObject.AddComponent<ControllerYawDragHandler>();
            m_yawHandler.DeadZoneDegrees = DiamondDragDeadZoneDegrees;
            m_yawHandler.OnDragStarted += () =>
            {
                m_dragStartTime = Key.Time;
                m_owner.SelectKey(Key);
            };
            m_yawHandler.OnYawDragged += (effectiveAngle) =>
            {
                float newTime = m_dragStartTime + effectiveAngle * DiamondDragSensitivity;
                newTime = Mathf.Clamp01(newTime);
                if (Mathf.Abs(newTime - Key.Time) < 0.0001f) return;
                Key.Time = newTime;
                RefreshPosition();
                m_owner.OnKeyTimeDragged(Key);
            };
            m_yawHandler.OnDragEnded += () =>
            {
                m_owner.Gradient.Keys.Sort((a, b) => a.Time.CompareTo(b.Time));
                m_owner.Gradient.SetDirty();
                m_owner.RefreshPreviews();
            };

            // ensure pivot center
            RectTransform.pivot = new Vector2(0.5f, 0.5f);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            m_owner.SelectKey(Key);
        }
    }
}
