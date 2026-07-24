using System;
using System.Linq;
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
        
        panel = SimpleFloatingPanel.Create(new Vector2(200, 80), new Vector3(0, 1.2f, 1.5f));
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

        m_partPanel.Content.ClearChildren(m_partSelectRow);
        m_geometryPanel.Content.ClearChildren();
        m_materialPanel.Content.ClearChildren();

        RebuildPanels();
    }

    private void RebuildPanels()
    {
        if (m_selectedPartIndex < 0)
            return;
        
        var referencePart = EditingSaber.Data.Components[m_selectedPartIndex];
        
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
    }
}