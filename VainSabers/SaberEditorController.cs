using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using VainSabers.Config;
using VainSabers.Sabers;
using VainSabers.UI;

namespace VainSabers;

internal class SaberEditorController : MonoBehaviour
{
    private SimpleFloatingPanel? panel;

    private PluginConfig config = null!;

    public void Init(PluginConfig config)
    {
        this.config = config;
    }
    private void Awake()
    {
        MenuStateHandler.ModPanelStateChanged += StateChanged;
    }

    private void Start()
    {
        Invoke(nameof(UpdateUI), 0.1f);
    }

    private void UpdateUI()
    {
        
    }

    private void OnDestroy()
    {
        MenuStateHandler.ModPanelStateChanged -= StateChanged;
    }

    private void StateChanged(MenuStateHandler.ModPanelState state)
    {
        if (panel != null)
            panel.Destroy(); // just yeet it, we can make ui very easily at the call site. (See below)
        
        if (!state.EditorOpen)
            return;
        
        panel = SimpleFloatingPanel.Create(new Vector2(200, 110), new Vector3(0, 1.2f, 1.5f));
        panel.Show();

        var editor = panel.AddChild<SaberEditorComponent>().ToFill();
        editor.OnSave += () =>
        {
            Plugin.Log.Info("Saving and closing editor");
            if (state.EditingPreset != "")
            {
                MenuStateHandler.Sabers.right.Data.SaveToFile(
                    ConfigUtil.GetSaberProfile(state.EditingPreset));

                MenuStateHandler.Sabers.right.SetPreset(state.EditingPreset);
                MenuStateHandler.Sabers.left.SetPreset(state.EditingPreset);
            }
            MenuStateHandler.SetEditorOpen(false);
        };
    }
}

class SaberEditorComponent : UIComponent
{
    private TextButtonComponent m_saveButton = null!;

    public event Action? OnSave;

    private int m_selectedPartIndex = -1;

    private void ApplyToBothSabers(Action<BlurSaber> action)
    {
        action(MenuStateHandler.Sabers.right);
        action(MenuStateHandler.Sabers.left);
    }

    private void ApplyToBothParts(Action<BlurSaberPart> action)
    {
        if (m_selectedPartIndex < 0)
            return;
        action(MenuStateHandler.Sabers.right.Data.Components[m_selectedPartIndex]);
        action(MenuStateHandler.Sabers.left.Data.Components[m_selectedPartIndex]);
    }

    private BlurSaber EditingSaber => MenuStateHandler.Sabers.right;
    
    // panels
    private SubPanelComponent m_partPanel = null!;
    private SubPanelComponent m_geometryPanel = null!;
    private SubPanelComponent m_materialPanel = null!;

    // part panel stuff
    private DropdownComponent m_partDropdown = null!;
    private TextButtonComponent m_addPartButton = null!;
    private TextButtonComponent m_removePartButton = null!;

    // advanced geometry
    private int m_selectedRingIndex = 0;
    private TextComponent m_ringIndexText = null!;

    private UIComponent m_partSelectRow = null!;

    protected override void Init()
    {
        var layout = AddChild<HorizontalLayoutGroupComponent>().ToFill().WithSpacing(2);
        layout.ChildControlWidth = true;
        layout.ChildControlHeight = true;
        layout.ChildForceExpandWidth = true;
        layout.ChildForceExpandHeight = true;
        
        m_partPanel = layout.AddChild<SubPanelComponent>().WithLabel("Part");
        m_geometryPanel = layout.AddChild<SubPanelComponent>().WithLabel("Geometry");
        m_materialPanel = layout.AddChild<SubPanelComponent>().WithLabel("Material");

        m_saveButton = m_partPanel.AddChild<TextButtonComponent>().ToTopLeft()
            .ExtendBottom(4).ExtendRight(7).Move(1,-1).WithText("Save");
        m_saveButton.OnClick += () => OnSave?.Invoke();
        m_saveButton.Color = new  Color(0.3f, 0.5f, 0.7f, 1.0f);

        m_partSelectRow = m_partPanel.Content.AddChild<UIComponent>().WithPreferredHeight(4);

        m_partDropdown = m_partSelectRow.AddChild<DropdownComponent>().ToFill().InsetRight(10);
        m_partDropdown.OnSelectionChanged += SelectedPartChanged;

        m_addPartButton = m_partSelectRow.AddChild<TextButtonComponent>().ToRightEdge()
            .ExtendLeft(4).WithText("+");
        m_addPartButton.OnClick += AddPart;
        m_addPartButton.Color = new Color(0.1f, 0.6f, 0.2f, 1.0f);

        m_removePartButton = m_partSelectRow.AddChild<TextButtonComponent>().ToRightEdge()
            .ExtendLeft(4).Move(-5, 0).WithText("-");
        m_removePartButton.OnClick += RemovePart;
        m_removePartButton.Color = new Color(0.7f, 0.1f, 0.25f, 1.0f);

        UpdatePartDropdown();

        base.Init();
    }

    private void AddPart()
    {
        var parts = EditingSaber.Data.Components;
        var newNumber = 1;
        var name = "Part N";
        while (true)
        {
            name = $"Part {newNumber}";
            if (!parts.Any(p => p.gameObject.name == name))
                break;
            newNumber++;
        }

        ApplyToBothSabers(saber => saber.Data.AddComponent(name));

        m_selectedPartIndex = parts.Count - 1;
        UpdatePartDropdown();
    }

    private void RemovePart()
    {
        if (EditingSaber.Data.Components.Count == 0)
            return;

        ApplyToBothSabers(saber =>
        {
            var part = saber.Data.Components[m_selectedPartIndex];
            saber.Data.RemoveComponent(part);
        });

        m_selectedPartIndex--;
        UpdatePartDropdown();
    }

    public void UpdatePartDropdown()
    {
        var parts = EditingSaber.Data.Components;
        m_partDropdown.SetOptions(parts.Select(p => p.gameObject.name));
        var index = m_selectedPartIndex;
        if (index < 0)
            index = 0;
        if (index >= parts.Count)
            index = parts.Count - 1;
        m_partDropdown.SelectedIndex = index;
        m_selectedPartIndex = index;

        if (parts.Count > 0)
            m_removePartButton.IsInteractable = true;
        else
            m_removePartButton.IsInteractable = false;
    }

    private void SelectedPartChanged(int index)
    {
        m_selectedPartIndex = index;
        m_selectedRingIndex = 0;

        if (index < 0)
        {
            m_geometryPanel.Title = "Geometry";
            m_materialPanel.Title = "Material";
        }
        else
        {
            m_geometryPanel.Title = $"{EditingSaber.Data.Components[index].gameObject.name} : Geometry";
            m_materialPanel.Title = $"{EditingSaber.Data.Components[index].gameObject.name} : Material";
        }

        RebuildPanels();
    }

    private void RebuildPanels()
    {
        m_partPanel.Content.ClearChildren(m_partSelectRow);
        m_geometryPanel.Content.ClearChildren();
        m_materialPanel.Content.ClearChildren();

        if (m_selectedPartIndex < 0)
            return;
        
        var referencePart = EditingSaber.Data.Components[m_selectedPartIndex];
        
        // Part panel

        m_partPanel.Content.AddSubHeader("Position");
        m_partPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("X").SetComponent<NumberInputComponent>().WithMinMaxStep(-1f, 1f, 0.005f)
            .WithValue(referencePart.transform.localPosition.x).OnValueChanged += val =>
            ApplyToBothParts(part => 
            part.transform.localPosition = part.transform.localPosition with { x = val });
        m_partPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Y").SetComponent<NumberInputComponent>().WithMinMaxStep(-1f, 1f, 0.005f)
            .WithValue(referencePart.transform.localPosition.y).OnValueChanged += val =>
            ApplyToBothParts(part => 
            part.transform.localPosition = part.transform.localPosition with { y = val });
        m_partPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Z").SetComponent<NumberInputComponent>().WithMinMaxStep(-1f, 1f, 0.005f)
            .WithValue(referencePart.transform.localPosition.z).OnValueChanged += val =>
            ApplyToBothParts(part =>     
            part.transform.localPosition = part.transform.localPosition with { z = val });
        
        m_partPanel.Content.AddSubHeader("Rotation");
        m_partPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("X").SetComponent<NumberInputComponent>().WithMinMaxStep(-180f, 180f, 1f).WithSensitivityCoef(45)
            .WithValue(referencePart.RotX).OnValueChanged += val =>
            ApplyToBothParts(part => part.RotX = val);
        m_partPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Y").SetComponent<NumberInputComponent>().WithMinMaxStep(-180f, 180f, 1f).WithSensitivityCoef(45)
            .WithValue(referencePart.RotY).OnValueChanged += val =>
            ApplyToBothParts(part => part.RotY = val);
        m_partPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Z").SetComponent<NumberInputComponent>().WithMinMaxStep(-180f, 180f, 1f).WithSensitivityCoef(45)
            .WithValue(referencePart.RotZ).OnValueChanged += val =>
            ApplyToBothParts(part => part.RotZ = val);
        
        m_partPanel.Content.AddSubHeader("Geometry");
        m_partPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Length").SetComponent<NumberInputComponent>()
            .WithMinMaxStep(0.01f, 1f, 0.005f)
            .WithValue(referencePart.Length).OnValueChanged += val =>
            ApplyToBothParts(part => part.Length = val);
        
        // Material panel
        m_materialPanel.Content.AddSubHeader("General");
        m_materialPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Hue Shift").SetComponent<NumberInputComponent>().WithMinMaxStep(-0.5f, 0.5f, 0.025f)
            .WithValue(referencePart.HueShift).OnValueChanged += val =>
            ApplyToBothParts(part => part.HueShift = val);
        m_materialPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Use Lit Shader").SetComponent<ToggleComponent>().WithValue(referencePart.Lit)
            .OnValueChanged += val =>
            {
                ApplyToBothParts(part => part.Lit = val);
                RebuildPanels();
            };

        m_materialPanel.Content.AddSubHeader("Rim Shading");
        m_materialPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Strength").SetComponent<NumberInputComponent>().WithMinMaxStep(-3f, 3f, 0.1f).WithSensitivityCoef(0.3f)
            .WithValue(referencePart.RimFactor).OnValueChanged += val =>
            ApplyToBothParts(part => part.RimFactor = val);
        m_materialPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Falloff Power").SetComponent<NumberInputComponent>().WithMinMaxStep(0.25f, 6f, 0.25f)
            .WithValue(referencePart.RimPower).OnValueChanged += val =>
            ApplyToBothParts(part => part.RimPower = val);
        m_materialPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Perpendicular Filter").SetComponent<NumberInputComponent>().WithMinMaxStep(0f, 1f, 0.1f).WithSensitivityCoef(0.3f)
            .WithValue(referencePart.RimPerpendicular).OnValueChanged += val =>
            ApplyToBothParts(part => part.RimPerpendicular = val);
        
        m_materialPanel.Content.AddSubHeader("Blur");
        m_materialPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Time").SetComponent<NumberInputComponent>().WithMinMaxStep(0f, 1f, 0.1f).WithSensitivityCoef(0.3f)
            .WithValue(referencePart.BlurFactor).OnValueChanged += val =>
            ApplyToBothParts(part => part.BlurFactor = val);
        m_materialPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Softness").SetComponent<NumberInputComponent>().WithMinMaxStep(0f, 5f, 0.1f).WithSensitivityCoef(0.3f)
            .WithValue(referencePart.BlurFadeFactor).OnValueChanged += val =>
            ApplyToBothParts(part => part.BlurFadeFactor = val);

        m_materialPanel.Content.AddSubHeader("Rendering");
        m_materialPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Depth Offset").SetComponent<NumberInputComponent>().WithMinMaxStep(-0.02f, 0.02f, 0.001f).WithSensitivityCoef(0.03f)
            .WithValue(referencePart.DepthOffset).OnValueChanged += val =>
            ApplyToBothParts(part => part.DepthOffset = val);
        m_materialPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Queue Offset").SetComponent<NumberInputComponent>().WithMinMaxStep(-10f, 10f, 1f)
            .WithValue(referencePart.RenderQueueOffset).OnValueChanged += val =>
            ApplyToBothParts(part => part.RenderQueueOffset = Mathf.RoundToInt(val));
        
        // Geometry panel
        var options = new List<string>();
        foreach (var value in Enum.GetValues(typeof(BlurSaberPart.GeometryType)))
            options.Add(value.ToString());
        
        var typeDropdown = m_geometryPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Geometry Type").SetComponent<DropdownComponent>();
        typeDropdown.SetOptions(options);
        typeDropdown.SelectedIndex = options.IndexOf(referencePart.GeometryHandling.ToString());
        typeDropdown.OnSelectionChanged += index =>
        {
            var value = Enum.Parse(typeof(BlurSaberPart.GeometryType), options[index]);
            ApplyToBothParts(part => part.GeometryHandling = (BlurSaberPart.GeometryType)value);
            RebuildPanels();
        };

        switch (referencePart.GeometryHandling)
        {
            case BlurSaberPart.GeometryType.Simple:
                BuildSimpleGeometryPanel(referencePart);
                break;
            case BlurSaberPart.GeometryType.Advanced:
                BuildAdvancedGeometryPanel(referencePart);
                break;
        }
    }

    private void BuildSimpleGeometryPanel(BlurSaberPart referencePart)
    {
        m_geometryPanel.Content.AddSubHeader("Start Properties");
        m_geometryPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Radius").SetComponent<NumberInputComponent>().WithMinMaxStep(0.001f, 0.05f, 0.001f).WithSensitivityCoef(0.03f)
            .WithValue(referencePart.StartRadius).OnValueChanged += val =>
            ApplyToBothParts(part => part.StartRadius = val);
        
        m_geometryPanel.Content.AddSpace(2);
        
        m_geometryPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("R").SetComponent<NumberInputComponent>().WithMinMaxStep(-1f, 1f, 0.005f)
            .WithValue(referencePart.StartColor.r).OnValueChanged += val =>
            ApplyToBothParts(part => part.StartColor = new Color(val, part.StartColor.g, part.StartColor.b, part.StartColor.a));
        m_geometryPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("G").SetComponent<NumberInputComponent>().WithMinMaxStep(-1f, 1f, 0.005f)
            .WithValue(referencePart.StartColor.g).OnValueChanged += val =>
            ApplyToBothParts(part => part.StartColor = new Color(part.StartColor.r, val, part.StartColor.b, part.StartColor.a));
        m_geometryPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("B").SetComponent<NumberInputComponent>().WithMinMaxStep(-1f, 1f, 0.005f)
            .WithValue(referencePart.StartColor.b).OnValueChanged += val =>
            ApplyToBothParts(part => part.StartColor = new Color(part.StartColor.r, part.StartColor.g, val, part.StartColor.a));
        
        m_geometryPanel.Content.AddSpace(2);

        m_geometryPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Custom Weight").SetComponent<NumberInputComponent>().WithMinMaxStep(0f, 1f, 0.005f)
            .WithValue(referencePart.StartCustomColorWeight).OnValueChanged += val =>
            ApplyToBothParts(part => part.StartCustomColorWeight = val);
        if (!referencePart.Lit)
        m_geometryPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Glow").SetComponent<NumberInputComponent>().WithMinMaxStep(0f, 1.5f, 0.005f)
            .WithValue(referencePart.StartGlow).OnValueChanged += val =>
            ApplyToBothParts(part => part.StartGlow = val);
        m_geometryPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Opacity").SetComponent<NumberInputComponent>().WithMinMaxStep(0f, 1f, 0.01f)
            .WithValue(referencePart.StartOpacity).OnValueChanged += val =>
            ApplyToBothParts(part => part.StartOpacity = val);
        
        m_geometryPanel.Content.AddSubHeader("End Properties");
        m_geometryPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Radius").SetComponent<NumberInputComponent>().WithMinMaxStep(0.001f, 0.05f, 0.001f).WithSensitivityCoef(0.03f)
            .WithValue(referencePart.EndRadius).OnValueChanged += val =>
            ApplyToBothParts(part => part.EndRadius = val);
        
        m_geometryPanel.Content.AddSpace(2);
        
        m_geometryPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("R").SetComponent<NumberInputComponent>().WithMinMaxStep(-1f, 1f, 0.005f)
            .WithValue(referencePart.EndColor.r).OnValueChanged += val =>
            ApplyToBothParts(part => part.EndColor = new Color(val, part.EndColor.g, part.EndColor.b, part.EndColor.a));
        m_geometryPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("G").SetComponent<NumberInputComponent>().WithMinMaxStep(-1f, 1f, 0.005f)
            .WithValue(referencePart.EndColor.g).OnValueChanged += val =>
            ApplyToBothParts(part => part.EndColor = new Color(part.EndColor.r, val, part.EndColor.b, part.EndColor.a));
        m_geometryPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("B").SetComponent<NumberInputComponent>().WithMinMaxStep(-1f, 1f, 0.005f)
            .WithValue(referencePart.EndColor.b).OnValueChanged += val =>
            ApplyToBothParts(part => part.EndColor = new Color(part.EndColor.r, part.EndColor.g, val, part.EndColor.a));
        
        m_geometryPanel.Content.AddSpace(2);

        m_geometryPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Custom Weight").SetComponent<NumberInputComponent>().WithMinMaxStep(0f, 1f, 0.005f)
            .WithValue(referencePart.EndCustomColorWeight).OnValueChanged += val =>
            ApplyToBothParts(part => part.EndCustomColorWeight = val);
        if (!referencePart.Lit)
        m_geometryPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Glow").SetComponent<NumberInputComponent>().WithMinMaxStep(0f, 1.5f, 0.005f)
            .WithValue(referencePart.EndGlow).OnValueChanged += val =>
            ApplyToBothParts(part => part.EndGlow = val);
        m_geometryPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Opacity").SetComponent<NumberInputComponent>().WithMinMaxStep(0f, 1f, 0.01f)
            .WithValue(referencePart.EndOpacity).OnValueChanged += val =>
            ApplyToBothParts(part => part.EndOpacity = val);
        
        m_geometryPanel.Content.AddSubHeader("Common Properties");
        m_geometryPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Bulge Amount").SetComponent<NumberInputComponent>().WithMinMaxStep(-1f, 1f, 0.005f)
            .WithValue(referencePart.BulgeAmount).OnValueChanged += val =>
            ApplyToBothParts(part => part.BulgeAmount = val);
        m_geometryPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Rings").SetComponent<NumberInputComponent>().WithMinMaxStep(2f, 10f, 1f)
            .WithValue(referencePart.MinimumRings).OnValueChanged += val =>
            ApplyToBothParts(part => part.MinimumRings = Mathf.RoundToInt(Mathf.Clamp(val, 2f, 10f)));
        m_geometryPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Inverted").SetComponent<ToggleComponent>().WithValue(referencePart.Inverted)
            .OnValueChanged += val =>
            ApplyToBothParts(part => part.Inverted = val);
        m_geometryPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Use End Caps").SetComponent<ToggleComponent>().WithValue(referencePart.EnableEndCaps)
            .OnValueChanged += val =>
            ApplyToBothParts(part => part.EnableEndCaps = val);
        m_geometryPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("End Cap Extension").SetComponent<NumberInputComponent>().WithMinMaxStep(0f, 3f, 0.01f)
            .WithValue(referencePart.EndCapExtension).OnValueChanged += val =>
            ApplyToBothParts(part => part.EndCapExtension = val);
    }

    private void BuildAdvancedGeometryPanel(BlurSaberPart referencePart)
    {
        if (referencePart.RingParams.Count == 0)
        {
            var start = new BlurSaberRingParams(
                0f, referencePart.StartRadius, referencePart.StartColor,
                referencePart.StartCustomColorWeight, referencePart.StartGlow,
                referencePart.StartOpacity, referencePart.Inverted);
            var end = new BlurSaberRingParams(
                1f, referencePart.EndRadius, referencePart.EndColor,
                referencePart.EndCustomColorWeight, referencePart.EndGlow,
                referencePart.EndOpacity, referencePart.Inverted);
            ApplyToBothParts(part =>
            {
                part.RingParams.Clear();
                part.RingParams.Add(start);
                part.RingParams.Add(end);
            });
            m_selectedRingIndex = 0;
        }

        if (m_selectedRingIndex >= referencePart.RingParams.Count)
            m_selectedRingIndex = referencePart.RingParams.Count - 1;

        var ringNavRow = m_geometryPanel.Content.AddChild<UIComponent>().WithPreferredHeight(4);
        var ringNavLayout = ringNavRow.AddChild<HorizontalLayoutGroupComponent>().ToFill();
        ringNavLayout.WithSpacing(0.5f).WithPadding(0);
        ringNavLayout.ChildControlWidth = true;
        ringNavLayout.ChildControlHeight = true;
        ringNavLayout.ChildForceExpandWidth = true;
        ringNavLayout.ChildForceExpandHeight = true;

        var prevBtn = ringNavLayout.AddChild<TextButtonComponent>().WithText("<");
        prevBtn.Color = new Color(0.3f, 0.3f, 0.35f, 1f);
        prevBtn.OnClick += () =>
        {
            if (m_selectedRingIndex > 0)
            {
                m_selectedRingIndex--;
                RebuildPanels();
            }
        };

        m_ringIndexText = ringNavLayout.AddChild<TextComponent>();
        m_ringIndexText.Alignment = TextAlignmentOptions.Center;
        m_ringIndexText.Color = new Color(0.9f, 0.9f, 0.9f, 1f);
        m_ringIndexText.FontSize = 3.5f;
        m_ringIndexText.Text = $"{m_selectedRingIndex + 1}/{referencePart.RingParams.Count}";

        var nextBtn = ringNavLayout.AddChild<TextButtonComponent>().WithText(">");
        nextBtn.Color = new Color(0.3f, 0.3f, 0.35f, 1f);
        nextBtn.OnClick += () =>
        {
            if (m_selectedRingIndex < referencePart.RingParams.Count - 1)
            {
                m_selectedRingIndex++;
                RebuildPanels();
            }
        };

        var removeBtn = ringNavLayout.AddChild<TextButtonComponent>().WithText("-");
        removeBtn.Color = new Color(0.6f, 0.2f, 0.2f, 1f);
        removeBtn.OnClick += () =>
        {
            if (referencePart.RingParams.Count <= 1)
                return;
            ApplyToBothParts(part =>
            {
                if (m_selectedRingIndex >= part.RingParams.Count)
                    m_selectedRingIndex = part.RingParams.Count - 1;
                part.RingParams.RemoveAt(m_selectedRingIndex);
            });
            if (m_selectedRingIndex >= EditingSaber.Data.Components[m_selectedPartIndex].RingParams.Count)
                m_selectedRingIndex = EditingSaber.Data.Components[m_selectedPartIndex].RingParams.Count - 1;
            RebuildPanels();
        };
        removeBtn.IsInteractable = referencePart.RingParams.Count > 1;

        var addBtn = ringNavLayout.AddChild<TextButtonComponent>().WithText("+");
        addBtn.Color = new Color(0.2f, 0.5f, 0.2f, 1f);
        addBtn.OnClick += () =>
        {
            var current = referencePart.RingParams[m_selectedRingIndex];
            var newRing = new BlurSaberRingParams(
                Mathf.Clamp01(current.PosAlongPart01 + 0.1f),
                current.Radius, current.Color, current.CustomWeight,
                current.Glow, current.Opacity, current.Inverted);
            ApplyToBothParts(part =>
            {
                part.RingParams.Insert(m_selectedRingIndex + 1, newRing);
            });
            m_selectedRingIndex++;
            RebuildPanels();
        };

        m_geometryPanel.Content.AddSpace(1);

        var ring = referencePart.RingParams[m_selectedRingIndex];

        m_geometryPanel.Content.AddSubHeader("Ring Properties");
        m_geometryPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Position").SetComponent<NumberInputComponent>().WithMinMaxStep(0f, 1f, 0.01f)
            .WithValue(ring.PosAlongPart01).OnValueChanged += val =>
            {
                var i = m_selectedRingIndex;
                ApplyToBothParts(part =>
                {
                    if (i < part.RingParams.Count)
                        part.RingParams[i] = part.RingParams[i] with { PosAlongPart01 = Mathf.Clamp01(val) };
                });
            };
        m_geometryPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Radius").SetComponent<NumberInputComponent>().WithMinMaxStep(0.001f, 0.05f, 0.001f).WithSensitivityCoef(0.03f)
            .WithValue(ring.Radius).OnValueChanged += val =>
            {
                var i = m_selectedRingIndex;
                ApplyToBothParts(part =>
                {
                    if (i < part.RingParams.Count)
                        part.RingParams[i] = part.RingParams[i] with { Radius = val };
                });
            };
        m_geometryPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Inverted").SetComponent<ToggleComponent>().WithValue(ring.Inverted)
            .OnValueChanged += val =>
            {
                var i = m_selectedRingIndex;
                ApplyToBothParts(part =>
                {
                    if (i < part.RingParams.Count)
                        part.RingParams[i] = part.RingParams[i] with { Inverted = val };
                });
            };

        m_geometryPanel.Content.AddSpace(1);

        m_geometryPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("R").SetComponent<NumberInputComponent>().WithMinMaxStep(-1f, 1f, 0.005f)
            .WithValue(ring.Color.r).OnValueChanged += val =>
            {
                var i = m_selectedRingIndex;
                ApplyToBothParts(part =>
                {
                    if (i < part.RingParams.Count)
                    {
                        var c = part.RingParams[i].Color;
                        part.RingParams[i] = part.RingParams[i] with { Color = new Color(val, c.g, c.b, c.a) };
                    }
                });
            };
        m_geometryPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("G").SetComponent<NumberInputComponent>().WithMinMaxStep(-1f, 1f, 0.005f)
            .WithValue(ring.Color.g).OnValueChanged += val =>
            {
                var i = m_selectedRingIndex;
                ApplyToBothParts(part =>
                {
                    if (i < part.RingParams.Count)
                    {
                        var c = part.RingParams[i].Color;
                        part.RingParams[i] = part.RingParams[i] with { Color = new Color(c.r, val, c.b, c.a) };
                    }
                });
            };
        m_geometryPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("B").SetComponent<NumberInputComponent>().WithMinMaxStep(-1f, 1f, 0.005f)
            .WithValue(ring.Color.b).OnValueChanged += val =>
            {
                var i = m_selectedRingIndex;
                ApplyToBothParts(part =>
                {
                    if (i < part.RingParams.Count)
                    {
                        var c = part.RingParams[i].Color;
                        part.RingParams[i] = part.RingParams[i] with { Color = new Color(c.r, c.g, val, c.a) };
                    }
                });
            };

        m_geometryPanel.Content.AddSpace(1);

        m_geometryPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Custom Weight").SetComponent<NumberInputComponent>().WithMinMaxStep(0f, 1f, 0.005f)
            .WithValue(ring.CustomWeight).OnValueChanged += val =>
            {
                var i = m_selectedRingIndex;
                ApplyToBothParts(part =>
                {
                    if (i < part.RingParams.Count)
                        part.RingParams[i] = part.RingParams[i] with { CustomWeight = val };
                });
            };
        if (!referencePart.Lit)
        m_geometryPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Glow").SetComponent<NumberInputComponent>().WithMinMaxStep(0f, 1.5f, 0.005f)
            .WithValue(ring.Glow).OnValueChanged += val =>
            {
                var i = m_selectedRingIndex;
                ApplyToBothParts(part =>
                {
                    if (i < part.RingParams.Count)
                        part.RingParams[i] = part.RingParams[i] with { Glow = val };
                });
            };
        m_geometryPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Opacity").SetComponent<NumberInputComponent>().WithMinMaxStep(0f, 1f, 0.01f)
            .WithValue(ring.Opacity).OnValueChanged += val =>
            {
                var i = m_selectedRingIndex;
                ApplyToBothParts(part =>
                {
                    if (i < part.RingParams.Count)
                        part.RingParams[i] = part.RingParams[i] with { Opacity = val };
                });
            };
    }
}