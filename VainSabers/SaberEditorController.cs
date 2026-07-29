using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using VainSabers.Config;
using VainSabers.Helpers;
using VainSabers.Sabers;
using VainSabers.UI;

namespace VainSabers;

internal class SaberEditorController : MonoBehaviour
{
    private SimpleFloatingPanel? panel;

    private PluginConfig config = null!;

    private GameObject? m_leftPreviewAnchor;
    private GameObject? m_rightPreviewAnchor;
    private bool m_inPreviewMode;

    public void Init(PluginConfig config)
    {
        this.config = config;
    }
    private void Awake()
    {
        MenuStateHandler.ModPanelStateChanged += StateChanged;
    }

    private void OnDestroy()
    {
        MenuStateHandler.ModPanelStateChanged -= StateChanged;
    }

    private void StateChanged(MenuStateHandler.ModPanelState state)
    {
        ExitPreviewMode();

        if (panel != null)
            panel.Destroy(); // just yeet it, we can make ui very easily at the call site. (See below)
        
        if (!state.EditorOpen)
            return;
        
        panel = SimpleFloatingPanel.Create(new Vector2(250, 126), new Vector3(0, 1.2f, 2.0f));
        panel.Show();

        var fpfc = FindObjectOfType<FirstPersonFlyingController>();
        bool isFpfc = fpfc != null && fpfc.enabled;
        bool holdSabers = !isFpfc;

        if (!holdSabers)
            EnterPreviewMode();

        var editor = panel.AddChild<SaberEditorComponent>().ToFill();
        editor.HoldSabers = holdSabers;
        editor.OnHoldSabersToggled += hold =>
        {
            if (hold) ExitPreviewMode();
            else EnterPreviewMode();
        };
        var editingPreset = string.IsNullOrEmpty(state.EditingPreset) ? config.CurrentSaber : state.EditingPreset;
        editor.ConfigTitle = $"Config : {editingPreset}";
        editor.OnSave += () =>
        {
            Plugin.Log.Info("Saving and closing editor");
            Plugin.Log.Info($"Finishing editing: {editingPreset}");
            if (editingPreset != "")
            {
                var profile = ConfigUtil.GetSaberProfile(editingPreset);
                Plugin.Log.Info($"Saving to profile: {profile}");
                MenuStateHandler.Sabers.right.Data.SaveToFile(profile);

                MenuStateHandler.Sabers.right.SetPreset(editingPreset);
                MenuStateHandler.Sabers.left.SetPreset(editingPreset);
            }
            MenuStateHandler.SetEditorOpen(false);
        };
        editor.OnRevert += () =>
        {
            Plugin.Log.Info("Reverting...");
            MenuStateHandler.SetEditorOpen(false);
            MenuStateHandler.SetEditorOpen(true);
        };
        editor.OnDelete += () =>
        {
            Plugin.Log.Info($"Deleting preset: {editingPreset}");
            var path = ConfigUtil.GetSaberProfile(editingPreset);
            if (File.Exists(path))
                File.Delete(path);
            MenuStateHandler.SetEditingPreset("");
            config.CurrentSaber = "";
            MenuStateHandler.NotifyPresetListChanged();
            MenuStateHandler.SetEditorOpen(false);
        };
        editor.OnRename += newName =>
        {
            Plugin.Log.Info($"Renaming preset: {editingPreset} -> {newName}");
            var oldPath = ConfigUtil.GetSaberProfile(editingPreset);
            var newPath = ConfigUtil.GetSaberProfile(newName);

            if (File.Exists(newPath))
                return;

            if (editingPreset != "")
                MenuStateHandler.Sabers.right.Data.SaveToFile(oldPath);

            if (File.Exists(oldPath))
                File.Move(oldPath, newPath);
            else
                MenuStateHandler.Sabers.right.Data.SaveToFile(newPath);

            editingPreset = newName;
            config.CurrentSaber = newName;
            editor.ConfigTitle = $"Config : {newName}";
            MenuStateHandler.SetEditingPreset(newName);
            MenuStateHandler.NotifyPresetListChanged();
        };
    }

    private void EnterPreviewMode()
    {
        if (m_inPreviewMode) return;

        var (leftSaber, rightSaber) = MenuStateHandler.Sabers;
        if (leftSaber == null || rightSaber == null) return;

        m_leftPreviewAnchor = new GameObject("LeftSaberPreviewAnchor");
        m_leftPreviewAnchor.transform.position = new Vector3(-0.1f, 0.8f, 1.2f);
        m_leftPreviewAnchor.transform.rotation = Quaternion.LookRotation(Vector3.up);

        m_rightPreviewAnchor = new GameObject("RightSaberPreviewAnchor");
        m_rightPreviewAnchor.transform.position = new Vector3(0.1f, 0.8f, 1.2f);
        m_rightPreviewAnchor.transform.rotation = Quaternion.LookRotation(Vector3.up);

        leftSaber.transform.position = m_leftPreviewAnchor.transform.position;
        leftSaber.transform.rotation = m_leftPreviewAnchor.transform.rotation;
        rightSaber.transform.position = m_rightPreviewAnchor.transform.position;
        rightSaber.transform.rotation = m_rightPreviewAnchor.transform.rotation;

        leftSaber.SetPreviewTransform(m_leftPreviewAnchor.transform);
        rightSaber.SetPreviewTransform(m_rightPreviewAnchor.transform);

        m_inPreviewMode = true;
    }

    private void ExitPreviewMode()
    {
        if (!m_inPreviewMode) return;

        var (leftSaber, rightSaber) = MenuStateHandler.Sabers;

        if (leftSaber != null)
        {
            leftSaber.SetPreviewTransform(null);
            leftSaber.transform.position = Vector3.zero;
            leftSaber.transform.rotation = Quaternion.identity;
        }
        if (rightSaber != null)
        {
            rightSaber.SetPreviewTransform(null);
            rightSaber.transform.position = Vector3.zero;
            rightSaber.transform.rotation = Quaternion.identity;
        }

        if (m_leftPreviewAnchor != null)
            Destroy(m_leftPreviewAnchor);
        if (m_rightPreviewAnchor != null)
            Destroy(m_rightPreviewAnchor);

        m_leftPreviewAnchor = null;
        m_rightPreviewAnchor = null;
        m_inPreviewMode = false;
    }
}

class SaberEditorComponent : UIComponent
{
    private static readonly Color RedColor = (VainColor)"#f3769c" * 1.3f;
    private static readonly Color GreenColor = (VainColor)"#72ee91" * 1.3f;
    private static readonly Color BlueColor = (VainColor)"#79a7f7" * 1.3f;

    private TextButtonComponent m_saveButton = null!;
    private TextButtonComponent m_deleteButton = null!;
    private TextButtonComponent m_holdSabersButton = null!;
    private TextInputComponent m_renameInput = null!;

    public event Action? OnSave;
    public event Action? OnRevert;
    public event Action? OnDelete;
    public event Action<string>? OnRename;
    public event Action<bool>? OnHoldSabersToggled;

    private bool m_holdSabers = true;
    public bool HoldSabers
    {
        get => m_holdSabers;
        set
        {
            m_holdSabers = value;
            if (m_holdSabersButton != null)
                m_holdSabersButton.Text.Text = value ? "Un-hold Sabers" : "Hold Sabers";
            m_saberBackgroundColumn.gameObject.SetActive(!value);
        }
    }

    public string ConfigTitle
    {
        get => m_configPanel.Title;
        set => m_configPanel.Title = value;
    }

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

    private void ApplyToBothResolvedParts(Action<BlurSaberPart> action)
    {
        if (m_selectedPartIndex < 0)
            return;

        ApplyToBothSabers(saber =>
        {
            var part = saber.Data.Components[m_selectedPartIndex];
            if (part.LinkedPartIndex >= 0 && part.LinkedPartIndex < saber.Data.ComponentCount)
            {
                var source = saber.Data.Components[part.LinkedPartIndex];
                if (source != null)
                    action(source);
                else
                    action(part);
            }
            else
            {
                action(part);
            }
        });
    }

    private BlurSaberPart ResolveSource(BlurSaberPart part)
    {
        if (part.LinkedPartIndex >= 0 && part.LinkedPartIndex < EditingSaber.Data.ComponentCount)
        {
            var source = EditingSaber.Data.Components[part.LinkedPartIndex];
            if (source != null)
                return source;
        }
        return part;
    }

    private BlurSaber EditingSaber => MenuStateHandler.Sabers.right;

    private static List<string> GetTextureFileNames()
    {
        var names = new List<string> { "None" };
        if (!Directory.Exists(ConfigUtil.ConfigDir))
            return names;
        var textures = Directory.GetFiles(ConfigUtil.ConfigDir, "*.png")
            .Concat(Directory.GetFiles(ConfigUtil.ConfigDir, "*.jpg"))
            .Concat(Directory.GetFiles(ConfigUtil.ConfigDir, "*.jpeg"))
            .Select(Path.GetFileName)
            .OrderBy(x => x)
            .ToList();
        names.AddRange(textures!);
        return names;
    }
    
    // panels
    private SubPanelComponent m_configPanel = null!;
    private SubPanelComponent m_partPanel = null!;
    private UIComponent m_saberBackgroundColumn = null!;
    private SubPanelComponent m_geometryPanel = null!;
    private SubPanelComponent m_materialPanel = null!;
    private SubPanelComponent m_trailPanel = null!;

    // part panel stuff
    private DropdownComponent m_partDropdown = null!;
    private TextButtonComponent m_addPartButton = null!;
    private TextButtonComponent m_removePartButton = null!;
    private TextInputComponent m_partNameInput = null!;
    private TextButtonComponent m_revertButton = null!;
    
    private readonly string[] m_deleteLabels = { "Delete", "Delete (Sure?)", "Delete (Really sure?)" };
    private readonly string[] m_revertLabels = { "Revert", "Revert (Sure?)", "Revert (Really sure?)" };
    private int m_deleteStage = 0;
    private int m_revertStage = 0;

    // advanced geometry
    private int m_selectedRingIndex = 0;
    private TextComponent m_ringIndexText = null!;

    private UIComponent m_partSelectRow = null!;
    private UIComponent m_partActionRow = null!;
    private DropdownComponent m_linkDropdown = null!;
    private TextButtonComponent m_duplicateButton = null!;

    // trail editor
    private int m_selectedTipTrailIndex = 0;
    private int m_selectedTrailMode = 0;
    private TextComponent m_tipTrailIndexText = null!;
    private DropdownComponent m_trailModeDropdown = null!;

    protected override void Init()
    {
        var layout = AddChild<HorizontalLayoutGroupComponent>().ToFill().WithSpacing(2);
        layout.ChildControlWidth = true;
        layout.ChildControlHeight = true;
        layout.ChildForceExpandWidth = false;
        layout.ChildForceExpandHeight = true;
        
        var leftColumn = layout.AddChild<VerticalLayoutGroupComponent>();
        leftColumn.ChildControlWidth = true;
        leftColumn.ChildControlHeight = true;
        leftColumn.ChildForceExpandWidth = true;
        leftColumn.ChildForceExpandHeight = false;
        leftColumn.WithSpacing(2);
        leftColumn.LayoutElement.flexibleWidth = 1;

        m_configPanel = leftColumn.AddChild<SubPanelComponent>().WithLabel("Config");
        m_configPanel.LayoutElement.flexibleHeight = 1;

        var configContent = m_configPanel.Content;
        m_saveButton = configContent.AddChild<TextButtonComponent>().WithPreferredHeight(4).WithText("Save");
        m_saveButton.OnClick += () => OnSave?.Invoke();
        m_saveButton.Color = new Color(0.3f, 0.5f, 0.7f, 1.0f);

        m_deleteButton = configContent.AddChild<TextButtonComponent>().WithPreferredHeight(4).WithText("Delete");
        m_deleteButton.OnClick += () =>
            HandleConfirmClick(m_deleteButton, m_deleteLabels,
                () => m_deleteStage, v => m_deleteStage = v,
                () => OnDelete?.Invoke(),
                () => { m_revertStage = 0; m_revertButton.Text.Text = m_revertLabels[0]; });
        m_deleteButton.Color = new Color(0.7f, 0.15f, 0.15f, 1.0f);

        m_revertButton = configContent.AddChild<TextButtonComponent>().WithPreferredHeight(4).WithText("Revert");
        m_revertButton.OnClick += () =>
            HandleConfirmClick(m_revertButton, m_revertLabels,
                () => m_revertStage, v => m_revertStage = v,
                () => OnRevert?.Invoke(),
                () => { m_deleteStage = 0; m_deleteButton.Text.Text = m_deleteLabels[0]; });
        m_revertButton.Color = new Color(0.6f, 0.5f, 0.2f, 1.0f);

        m_renameInput = configContent.AddChild<TextInputComponent>().WithPreferredHeight(4);
        m_renameInput.OnValueChanged += name =>
        {
            if (!string.IsNullOrWhiteSpace(name))
                OnRename?.Invoke(name);
        };

        m_holdSabersButton = configContent.AddChild<TextButtonComponent>().WithPreferredHeight(4).WithText("Un-hold Sabers");
        m_holdSabersButton.OnClick += () => HoldSabers = !HoldSabers;
        m_holdSabersButton.OnClick += () => OnHoldSabersToggled?.Invoke(m_holdSabers);

        m_partPanel = leftColumn.AddChild<SubPanelComponent>().WithLabel("Part");
        m_partPanel.LayoutElement.flexibleHeight = 3;

        m_geometryPanel = layout.AddChild<SubPanelComponent>().WithLabel("Geometry");
        m_geometryPanel.LayoutElement.flexibleWidth = 1;

        m_saberBackgroundColumn = layout.AddChild<UIComponent>();
        m_saberBackgroundColumn.LayoutElement.preferredWidth = 40;
        m_saberBackgroundColumn.LayoutElement.flexibleWidth = 0;
        m_saberBackgroundColumn.AddChild<RoundRectComponent>().ToFill().Color = (VainColor)"#050505";

        m_materialPanel = layout.AddChild<SubPanelComponent>().WithLabel("Material");
        m_materialPanel.LayoutElement.flexibleWidth = 1;
        m_trailPanel = layout.AddChild<SubPanelComponent>().WithLabel("Trails");
        m_trailPanel.LayoutElement.flexibleWidth = 1;

        m_partSelectRow = m_partPanel.Content.AddChild<UIComponent>().WithPreferredHeight(4);

        m_partDropdown = m_partSelectRow.AddChild<DropdownComponent>().ToFill().InsetRight(15);
        m_partDropdown.OnSelectionChanged += SelectedPartChanged;

        m_partNameInput = m_partSelectRow.AddChild<TextInputComponent>().ToRightEdge()
            .ExtendLeft(4).Move(-10, 0);
        m_partNameInput.MoveKeyboardX(-10);
        m_partNameInput.OnValueChanged += OnPartNameChanged;

        m_removePartButton = m_partSelectRow.AddChild<TextButtonComponent>().ToRightEdge()
            .ExtendLeft(4).Move(-5, 0).WithText("-");
        m_removePartButton.OnClick += RemovePart;
        m_removePartButton.Color = new Color(0.7f, 0.1f, 0.25f, 1.0f);

        m_addPartButton = m_partSelectRow.AddChild<TextButtonComponent>().ToRightEdge()
            .ExtendLeft(4).WithText("+");
        m_addPartButton.OnClick += AddPart;
        m_addPartButton.Color = new Color(0.1f, 0.6f, 0.2f, 1.0f);

        m_partActionRow = m_partPanel.Content.AddChild<UIComponent>().WithPreferredHeight(4);

        m_linkDropdown = m_partActionRow.AddChild<FieldComponent>().ToFill().InsetRight(10)
            .WithLabel("Link").SetComponent<DropdownComponent>();
        m_linkDropdown.OnSelectionChanged += OnLinkChanged;

        m_duplicateButton = m_partActionRow.AddChild<TextButtonComponent>().ToRightEdge()
            .ExtendLeft(4).WithText("Dup");
        m_duplicateButton.OnClick += DuplicatePart;
        m_duplicateButton.Color = new Color(0.2f, 0.5f, 0.7f, 1.0f);

        UpdatePartDropdown();

        base.Init();
    }
    
    private void HandleConfirmClick(
        TextButtonComponent button, string[] labels,
        Func<int> getStage, Action<int> setStage,
        Action onConfirmed, Action resetOther)
    {
        int stage = getStage() + 1;
        if (stage >= labels.Length)
        {
            setStage(0);
            button.Text.Text = labels[0];
            resetOther();
            onConfirmed();
        }
        else
        {
            setStage(stage);
            button.Text.Text = labels[stage];
        }
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

    private void OnPartNameChanged(string newName)
    {
        if (m_selectedPartIndex < 0)
            return;

        if (string.IsNullOrWhiteSpace(newName))
            return;

        ApplyToBothSabers(saber =>
        {
            var part = saber.Data.Components[m_selectedPartIndex];
            if (part != null)
                part.gameObject.name = newName;
        });

        UpdatePartDropdown();

        if (m_selectedPartIndex >= 0)
        {
            m_geometryPanel.Title = $"{newName} : Geometry";
            m_materialPanel.Title = $"{newName} : Material";
        }
    }

    private void DuplicatePart()
    {
        if (m_selectedPartIndex < 0)
            return;

        ApplyToBothSabers(saber =>
        {
            var source = saber.Data.Components[m_selectedPartIndex];
            saber.Data.DuplicateComponent(source);
        });

        m_selectedPartIndex = EditingSaber.Data.Components.Count - 1;
        UpdatePartDropdown();
    }

    private void OnLinkChanged(int index)
    {
        if (m_selectedPartIndex < 0)
            return;

        int linkIndex = index - 1;
        ApplyToBothParts(part => part.LinkedPartIndex = linkIndex);
    }

    private void UpdateLinkDropdown()
    {
        var parts = EditingSaber.Data.Components;
        var options = new List<string> { "None" };
        foreach (var part in parts)
            options.Add(part.gameObject.name);

        m_linkDropdown.SetOptions(options);

        if (m_selectedPartIndex >= 0 && m_selectedPartIndex < parts.Count)
        {
            int linkIndex = parts[m_selectedPartIndex].LinkedPartIndex;
            m_linkDropdown.SelectedIndex = linkIndex + 1;
        }
        else
        {
            m_linkDropdown.SelectedIndex = 0;
        }
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
        {
            m_removePartButton.IsInteractable = true;
            m_partNameInput.SetValue(parts[index].gameObject.name, false);
            m_partActionRow.gameObject.SetActive(true);
        }
        else
        {
            m_removePartButton.IsInteractable = false;
            m_partNameInput.SetValue("", false);
            m_partActionRow.gameObject.SetActive(false);
        }
        UpdateLinkDropdown();
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
        m_partPanel.Content.ClearChildren(m_partSelectRow, m_partActionRow);
        m_geometryPanel.Content.ClearChildren();
        m_materialPanel.Content.ClearChildren();
        m_trailPanel.Content.ClearChildren();

        if (m_selectedPartIndex < 0)
            return;
        
        var referencePart = EditingSaber.Data.Components[m_selectedPartIndex];
        var sourcePart = ResolveSource(referencePart);
        
        // Part panel

        m_partPanel.Content.AddSubHeader("Position");
        m_partPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("X").SetComponent<NumberInputComponent>().WithMinMaxStep(-1f, 1f, 0.005f).WithSensitivityCoef(0.25f)
            .WithTint(RedColor)
            .WithValue(referencePart.transform.localPosition.x).OnValueChanged += val =>
            ApplyToBothParts(part => 
            part.transform.localPosition = part.transform.localPosition with { x = val });
        m_partPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Y").SetComponent<NumberInputComponent>().WithMinMaxStep(-1f, 1f, 0.005f).WithSensitivityCoef(0.25f)
            .WithTint(GreenColor)
            .WithValue(referencePart.transform.localPosition.y).OnValueChanged += val =>
            ApplyToBothParts(part => 
            part.transform.localPosition = part.transform.localPosition with { y = val });
        m_partPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Z").SetComponent<NumberInputComponent>().WithMinMaxStep(-1f, 1f, 0.005f).WithSensitivityCoef(0.25f)
            .WithTint(BlueColor)
            .WithValue(referencePart.transform.localPosition.z).OnValueChanged += val =>
            ApplyToBothParts(part =>     
            part.transform.localPosition = part.transform.localPosition with { z = val });
        
        m_partPanel.Content.AddSubHeader("Rotation");
        m_partPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("X").SetComponent<NumberInputComponent>().WithMinMaxStep(-180f, 180f, 1f).WithSensitivityCoef(45)
            .WithTint(RedColor)
            .WithValue(referencePart.RotX).OnValueChanged += val =>
            ApplyToBothParts(part => part.RotX = val);
        m_partPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Y").SetComponent<NumberInputComponent>().WithMinMaxStep(-180f, 180f, 1f).WithSensitivityCoef(45)
            .WithTint(GreenColor)
            .WithValue(referencePart.RotY).OnValueChanged += val =>
            ApplyToBothParts(part => part.RotY = val);
        m_partPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Z").SetComponent<NumberInputComponent>().WithMinMaxStep(-180f, 180f, 1f).WithSensitivityCoef(45)
            .WithTint(BlueColor)
            .WithValue(referencePart.RotZ).OnValueChanged += val =>
            ApplyToBothParts(part => part.RotZ = val);
        
        m_partPanel.Content.AddSubHeader("Geometry");
        m_partPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Length").SetComponent<NumberInputComponent>()
            .WithMinMaxStep(0.001f, 1f, 0.001f)
            .WithValue(sourcePart.Length).OnValueChanged += val =>
            ApplyToBothResolvedParts(part => part.Length = val);
        
        // Material panel
        m_materialPanel.Content.AddSubHeader("General");
        m_materialPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Hue Shift").SetComponent<NumberInputComponent>().WithMinMaxStep(-0.5f, 0.5f, 0.025f)
            .WithValue(sourcePart.HueShift).OnValueChanged += val =>
            ApplyToBothResolvedParts(part => part.HueShift = val);
        m_materialPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Use Lit Shader").SetComponent<ToggleComponent>().WithValue(sourcePart.Lit)
            .OnValueChanged += val =>
            {
                ApplyToBothResolvedParts(part => part.Lit = val);
                RebuildPanels();
            };

        m_materialPanel.Content.AddSubHeader("Textures");
        var textureFiles = GetTextureFileNames();
        var colorTexDropdown = m_materialPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Color / Opacity").SetComponent<DropdownComponent>();
        var colorTexIdx = textureFiles.IndexOf(sourcePart.ColorTextureName ?? "");
        if (colorTexIdx < 0) colorTexIdx = 0;
        colorTexDropdown.SetOptions(textureFiles, colorTexIdx);
        colorTexDropdown.OnSelectionChanged += idx =>
        {
            var name = idx > 0 ? textureFiles[idx] : null;
            ApplyToBothResolvedParts(part => part.ColorTextureName = name);
        };
        var glowTexDropdown = m_materialPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Glow").SetComponent<DropdownComponent>();
        var glowTexIdx = textureFiles.IndexOf(sourcePart.GlowTextureName ?? "");
        if (glowTexIdx < 0) glowTexIdx = 0;
        glowTexDropdown.SetOptions(textureFiles, glowTexIdx);
        glowTexDropdown.OnSelectionChanged += idx =>
        {
            var name = idx > 0 ? textureFiles[idx] : null;
            ApplyToBothResolvedParts(part => part.GlowTextureName = name);
        };

        m_materialPanel.Content.AddSubHeader("Rim Shading");
        m_materialPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Strength").SetComponent<NumberInputComponent>().WithMinMaxStep(-3f, 3f, 0.1f).WithSensitivityCoef(0.3f)
            .WithValue(sourcePart.RimFactor).OnValueChanged += val =>
            ApplyToBothResolvedParts(part => part.RimFactor = val);
        m_materialPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Falloff Power").SetComponent<NumberInputComponent>().WithMinMaxStep(0.25f, 6f, 0.25f)
            .WithValue(sourcePart.RimPower).OnValueChanged += val =>
            ApplyToBothResolvedParts(part => part.RimPower = val);
        m_materialPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Perpendicular Filter").SetComponent<NumberInputComponent>().WithMinMaxStep(0f, 1f, 0.1f).WithSensitivityCoef(0.3f)
            .WithValue(sourcePart.RimPerpendicular).OnValueChanged += val =>
            ApplyToBothResolvedParts(part => part.RimPerpendicular = val);
        
        m_materialPanel.Content.AddSubHeader("Blur");
        m_materialPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Time").SetComponent<NumberInputComponent>().WithMinMaxStep(0f, 1f, 0.1f).WithSensitivityCoef(0.3f)
            .WithValue(sourcePart.BlurFactor).OnValueChanged += val =>
            ApplyToBothResolvedParts(part => part.BlurFactor = val);
        m_materialPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Softness").SetComponent<NumberInputComponent>().WithMinMaxStep(0f, 5f, 0.1f).WithSensitivityCoef(0.3f)
            .WithValue(sourcePart.BlurFadeFactor).OnValueChanged += val =>
            ApplyToBothResolvedParts(part => part.BlurFadeFactor = val);

        m_materialPanel.Content.AddSubHeader("Rendering");
        m_materialPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Depth Offset").SetComponent<NumberInputComponent>().WithMinMaxStep(-0.02f, 0.02f, 0.001f).WithSensitivityCoef(0.03f)
            .WithValue(sourcePart.DepthOffset).OnValueChanged += val =>
            ApplyToBothResolvedParts(part => part.DepthOffset = val);
        m_materialPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Queue Offset").SetComponent<NumberInputComponent>().WithMinMaxStep(-10f, 10f, 1f)
            .WithValue(sourcePart.RenderQueueOffset).OnValueChanged += val =>
            ApplyToBothResolvedParts(part => part.RenderQueueOffset = Mathf.RoundToInt(val));
        
        // Geometry panel
        var typeDropdown = m_geometryPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Geometry Type").SetComponent<DropdownComponent>();
        typeDropdown.SetEnumOptions(sourcePart.GeometryHandling);
        typeDropdown.OnSelectionChanged += index =>
        {
            ApplyToBothResolvedParts(part => part.GeometryHandling = typeDropdown.SelectedEnumValue<BlurSaberPart.GeometryType>());
            RebuildPanels();
        };

        switch (sourcePart.GeometryHandling)
        {
            case BlurSaberPart.GeometryType.Simple:
                BuildSimpleGeometryPanel(sourcePart);
                break;
            case BlurSaberPart.GeometryType.Advanced:
                BuildAdvancedGeometryPanel(sourcePart);
                break;
        }

        RebuildTrailPanel();
    }

    private void BuildSimpleGeometryPanel(BlurSaberPart referencePart)
    {
        m_geometryPanel.Content.AddSubHeader("Start Properties");
        m_geometryPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Radius").SetComponent<NumberInputComponent>().WithMinMaxStep(0.0001f, 0.05f, 0.0001f).WithSensitivityCoef(0.03f)
            .WithValue(referencePart.StartRadius).OnValueChanged += val =>
            ApplyToBothResolvedParts(part => part.StartRadius = val);
        if (!referencePart.Lit)
            m_geometryPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
                .WithLabel("Glow").SetComponent<NumberInputComponent>().WithMinMaxStep(0f, 1.5f, 0.005f)
                .WithValue(referencePart.StartGlow).OnValueChanged += val =>
                ApplyToBothResolvedParts(part => part.StartGlow = val);
        m_geometryPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Opacity").SetComponent<NumberInputComponent>().WithMinMaxStep(0f, 1f, 0.01f)
            .WithValue(referencePart.StartOpacity).OnValueChanged += val =>
            ApplyToBothResolvedParts(part => part.StartOpacity = val);
        m_geometryPanel.Content.AddSpace(2);
        m_geometryPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("R").SetComponent<NumberInputComponent>().WithMinMaxStep(-1f, 1f, 0.005f)
            .WithTint(RedColor)
            .WithValue(referencePart.StartColor.r).OnValueChanged += val =>
            ApplyToBothResolvedParts(part => part.StartColor = new Color(val, part.StartColor.g, part.StartColor.b, part.StartColor.a));
        m_geometryPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("G").SetComponent<NumberInputComponent>().WithMinMaxStep(-1f, 1f, 0.005f)
            .WithTint(GreenColor)
            .WithValue(referencePart.StartColor.g).OnValueChanged += val =>
            ApplyToBothResolvedParts(part => part.StartColor = new Color(part.StartColor.r, val, part.StartColor.b, part.StartColor.a));
        m_geometryPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("B").SetComponent<NumberInputComponent>().WithMinMaxStep(-1f, 1f, 0.005f)
            .WithTint(BlueColor)
            .WithValue(referencePart.StartColor.b).OnValueChanged += val =>
            ApplyToBothResolvedParts(part => part.StartColor = new Color(part.StartColor.r, part.StartColor.g, val, part.StartColor.a));
        m_geometryPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Custom Weight").SetComponent<NumberInputComponent>().WithMinMaxStep(0f, 1f, 0.005f)
            .WithValue(referencePart.StartCustomColorWeight).OnValueChanged += val =>
            ApplyToBothResolvedParts(part => part.StartCustomColorWeight = val);
        
        
        m_geometryPanel.Content.AddSubHeader("End Properties");
        m_geometryPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Radius").SetComponent<NumberInputComponent>().WithMinMaxStep(0.0001f, 0.05f, 0.0001f).WithSensitivityCoef(0.03f)
            .WithValue(referencePart.EndRadius).OnValueChanged += val =>
            ApplyToBothResolvedParts(part => part.EndRadius = val);
        if (!referencePart.Lit)
            m_geometryPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
                .WithLabel("Glow").SetComponent<NumberInputComponent>().WithMinMaxStep(0f, 1.5f, 0.005f)
                .WithValue(referencePart.EndGlow).OnValueChanged += val =>
                ApplyToBothResolvedParts(part => part.EndGlow = val);
        m_geometryPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Opacity").SetComponent<NumberInputComponent>().WithMinMaxStep(0f, 1f, 0.01f)
            .WithValue(referencePart.EndOpacity).OnValueChanged += val =>
            ApplyToBothResolvedParts(part => part.EndOpacity = val);
        m_geometryPanel.Content.AddSpace(2);
        m_geometryPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("R").SetComponent<NumberInputComponent>().WithMinMaxStep(-1f, 1f, 0.005f)
            .WithTint(RedColor)
            .WithValue(referencePart.EndColor.r).OnValueChanged += val =>
            ApplyToBothResolvedParts(part => part.EndColor = new Color(val, part.EndColor.g, part.EndColor.b, part.EndColor.a));
        m_geometryPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("G").SetComponent<NumberInputComponent>().WithMinMaxStep(-1f, 1f, 0.005f)
            .WithTint(GreenColor)
            .WithValue(referencePart.EndColor.g).OnValueChanged += val =>
            ApplyToBothResolvedParts(part => part.EndColor = new Color(part.EndColor.r, val, part.EndColor.b, part.EndColor.a));
        m_geometryPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("B").SetComponent<NumberInputComponent>().WithMinMaxStep(-1f, 1f, 0.005f)
            .WithTint(BlueColor)
            .WithValue(referencePart.EndColor.b).OnValueChanged += val =>
            ApplyToBothResolvedParts(part => part.EndColor = new Color(part.EndColor.r, part.EndColor.g, val, part.EndColor.a));
        m_geometryPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Custom Weight").SetComponent<NumberInputComponent>().WithMinMaxStep(0f, 1f, 0.005f)
            .WithValue(referencePart.EndCustomColorWeight).OnValueChanged += val =>
            ApplyToBothResolvedParts(part => part.EndCustomColorWeight = val);
        
        m_geometryPanel.Content.AddSubHeader("Common Properties");
        m_geometryPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Bulge Amount").SetComponent<NumberInputComponent>().WithMinMaxStep(-1f, 1f, 0.005f)
            .WithValue(referencePart.BulgeAmount).OnValueChanged += val =>
            ApplyToBothResolvedParts(part => part.BulgeAmount = val);
        m_geometryPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Rings").SetComponent<NumberInputComponent>().WithMinMaxStep(2f, 10f, 1f)
            .WithValue(referencePart.MinimumRings).OnValueChanged += val =>
            ApplyToBothResolvedParts(part => part.MinimumRings = Mathf.RoundToInt(Mathf.Clamp(val, 2f, 10f)));
        m_geometryPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Inverted").SetComponent<ToggleComponent>().WithValue(referencePart.Inverted)
            .OnValueChanged += val =>
            ApplyToBothResolvedParts(part => part.Inverted = val);
        m_geometryPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Use End Caps").SetComponent<ToggleComponent>().WithValue(referencePart.EnableEndCaps)
            .OnValueChanged += val =>
            ApplyToBothResolvedParts(part => part.EnableEndCaps = val);
        m_geometryPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("End Cap Extension").SetComponent<NumberInputComponent>().WithMinMaxStep(0f, 3f, 0.01f)
            .WithValue(referencePart.EndCapExtension).OnValueChanged += val =>
            ApplyToBothResolvedParts(part => part.EndCapExtension = val);
        m_geometryPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Rounded Normals").SetComponent<ToggleComponent>().WithValue(referencePart.EnableRoundedNormals)
            .OnValueChanged += val =>
            ApplyToBothResolvedParts(part => part.EnableRoundedNormals = val);
    }

    private void BuildAdvancedGeometryPanel(BlurSaberPart referencePart)
    {
        if (referencePart.RingParams.Count == 0)
        {
            var start = new BlurSaberRingParams(
                0f, referencePart.StartRadius, referencePart.StartColor,
                referencePart.StartCustomColorWeight, referencePart.StartGlow,
                referencePart.StartOpacity, referencePart.Inverted, Vector2.zero);
            var end = new BlurSaberRingParams(
                1f, referencePart.EndRadius, referencePart.EndColor,
                referencePart.EndCustomColorWeight, referencePart.EndGlow,
                referencePart.EndOpacity, referencePart.Inverted, Vector2.zero);
            ApplyToBothResolvedParts(part =>
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
            ApplyToBothResolvedParts(part =>
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
                current.Glow, current.Opacity, current.Inverted, current.Offset);
            ApplyToBothResolvedParts(part =>
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
            .WithLabel("Position").SetComponent<NumberInputComponent>().WithMinMaxStep(-1f, 2f, 0.001f).WithSensitivityCoef(0.5f)
            .WithValue(ring.PosAlongPart01).OnValueChanged += val =>
            {
                var i = m_selectedRingIndex;
                ApplyToBothResolvedParts(part =>
                {
                    if (i < part.RingParams.Count)
                        part.RingParams[i] = part.RingParams[i] with { PosAlongPart01 = val };
                });
            };
        m_geometryPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Radius").SetComponent<NumberInputComponent>().WithMinMaxStep(0.0001f, 0.05f, 0.0001f).WithSensitivityCoef(0.03f)
            .WithValue(ring.Radius).OnValueChanged += val =>
            {
                var i = m_selectedRingIndex;
                ApplyToBothResolvedParts(part =>
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
                ApplyToBothResolvedParts(part =>
                {
                    if (i < part.RingParams.Count)
                        part.RingParams[i] = part.RingParams[i] with { Inverted = val };
                });
            };
        m_geometryPanel.Content.AddSubHeader("Offset");
        m_geometryPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Up").SetComponent<NumberInputComponent>().WithMinMaxStep(-0.1f, 0.1f, 0.001f).WithSensitivityCoef(0.03f)
            .WithValue(ring.Offset.y).OnValueChanged += val =>
            {
                var i = m_selectedRingIndex;
                ApplyToBothResolvedParts(part =>
                {
                    if (i < part.RingParams.Count)
                        part.RingParams[i] = part.RingParams[i] with { Offset = new Vector2(part.RingParams[i].Offset.x, val) };
                });
            };
        m_geometryPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Right").SetComponent<NumberInputComponent>().WithMinMaxStep(-0.1f, 0.1f, 0.001f).WithSensitivityCoef(0.03f)
            .WithValue(ring.Offset.x).OnValueChanged += val =>
            {
                var i = m_selectedRingIndex;
                ApplyToBothResolvedParts(part =>
                {
                    if (i < part.RingParams.Count)
                        part.RingParams[i] = part.RingParams[i] with { Offset = new Vector2(val, part.RingParams[i].Offset.y) };
                });
            };

        if (!referencePart.Lit)
            m_geometryPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
                .WithLabel("Glow").SetComponent<NumberInputComponent>().WithMinMaxStep(0f, 1.5f, 0.005f)
                .WithValue(ring.Glow).OnValueChanged += val =>
            {
                var i = m_selectedRingIndex;
                ApplyToBothResolvedParts(part =>
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
            ApplyToBothResolvedParts(part =>
            {
                if (i < part.RingParams.Count)
                    part.RingParams[i] = part.RingParams[i] with { Opacity = val };
            });
        };

        m_geometryPanel.Content.AddSpace(2);

        m_geometryPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("R").SetComponent<NumberInputComponent>().WithMinMaxStep(-1f, 1f, 0.005f)
            .WithTint(RedColor)
            .WithValue(ring.Color.r).OnValueChanged += val =>
            {
                var i = m_selectedRingIndex;
                ApplyToBothResolvedParts(part =>
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
            .WithTint(GreenColor)
            .WithValue(ring.Color.g).OnValueChanged += val =>
            {
                var i = m_selectedRingIndex;
                ApplyToBothResolvedParts(part =>
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
            .WithTint(BlueColor)
            .WithValue(ring.Color.b).OnValueChanged += val =>
            {
                var i = m_selectedRingIndex;
                ApplyToBothResolvedParts(part =>
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
                ApplyToBothResolvedParts(part =>
                {
                    if (i < part.RingParams.Count)
                        part.RingParams[i] = part.RingParams[i] with { CustomWeight = val };
                });
            };
    }

    private void RebuildTrailPanel()
    {
        m_trailPanel.Content.ClearChildren();
        var data = EditingSaber.Data;

        m_trailPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Use Custom Trails").SetComponent<ToggleComponent>().WithValue(data.UseCustomTrails)
            .OnValueChanged += val =>
            {
                ApplyToBothSabers(s => s.Data.SetUseCustomTrails(val));
                RebuildTrailPanel();
            };

        if (!data.UseCustomTrails)
            return;

        m_trailModeDropdown = m_trailPanel.Content.AddChild<DropdownComponent>().WithPreferredHeight(4);
        m_trailModeDropdown.SetOptions(new List<string> { "Tip Trails", "Blade Trail" }, m_selectedTrailMode);
        m_trailModeDropdown.OnSelectionChanged += index =>
        {
            m_selectedTrailMode = index;
            RebuildTrailPanel();
        };

        m_trailPanel.Content.AddSpace(1);

        switch (m_selectedTrailMode)
        {
            case 0:
                BuildTipTrailEditor();
                break;
            case 1:
                BuildBladeTrailEditor();
                break;
        }
    }

    private void BuildTipTrailEditor()
    {
        var data = EditingSaber.Data;
        ApplyToBothSabers(s => s.Data.EnsureDefaultTrails());

        if (m_selectedTipTrailIndex >= data.TipTrails.Count)
            m_selectedTipTrailIndex = data.TipTrails.Count - 1;
        if (m_selectedTipTrailIndex < 0)
            m_selectedTipTrailIndex = 0;

        var navRow = m_trailPanel.Content.AddChild<UIComponent>().WithPreferredHeight(4);
        var navLayout = navRow.AddChild<HorizontalLayoutGroupComponent>().ToFill();
        navLayout.WithSpacing(0.5f).WithPadding(0);
        navLayout.ChildControlWidth = true;
        navLayout.ChildControlHeight = true;
        navLayout.ChildForceExpandWidth = true;
        navLayout.ChildForceExpandHeight = true;

        var prevBtn = navLayout.AddChild<TextButtonComponent>().WithText("<");
        prevBtn.Color = new Color(0.3f, 0.3f, 0.35f, 1f);
        prevBtn.OnClick += () =>
        {
            if (m_selectedTipTrailIndex > 0)
            {
                m_selectedTipTrailIndex--;
                RebuildTrailPanel();
            }
        };

        m_tipTrailIndexText = navLayout.AddChild<TextComponent>();
        m_tipTrailIndexText.Alignment = TextAlignmentOptions.Center;
        m_tipTrailIndexText.Color = new Color(0.9f, 0.9f, 0.9f, 1f);
        m_tipTrailIndexText.FontSize = 3.5f;
        m_tipTrailIndexText.Text = data.TipTrails.Count > 0
            ? $"{m_selectedTipTrailIndex + 1}/{data.TipTrails.Count}"
            : "0/0";

        var nextBtn = navLayout.AddChild<TextButtonComponent>().WithText(">");
        nextBtn.Color = new Color(0.3f, 0.3f, 0.35f, 1f);
        nextBtn.OnClick += () =>
        {
            if (m_selectedTipTrailIndex < data.TipTrails.Count - 1)
            {
                m_selectedTipTrailIndex++;
                RebuildTrailPanel();
            }
        };

        var removeBtn = navLayout.AddChild<TextButtonComponent>().WithText("-");
        removeBtn.Color = new Color(0.6f, 0.2f, 0.2f, 1f);
        removeBtn.OnClick += () =>
        {
            data.RemoveTipTrail(m_selectedTipTrailIndex);
            RebuildTrailPanel();
        };
        removeBtn.IsInteractable = data.TipTrails.Count > 0;

        var addBtn = navLayout.AddChild<TextButtonComponent>().WithText("+");
        addBtn.Color = new Color(0.2f, 0.5f, 0.2f, 1f);
        addBtn.OnClick += () =>
        {
            data.AddTipTrail();
            m_selectedTipTrailIndex = data.TipTrails.Count - 1;
            RebuildTrailPanel();
        };

        if (data.TipTrails.Count == 0)
            return;

        var trail = data.TipTrails[m_selectedTipTrailIndex];

        m_trailPanel.Content.AddSubHeader("Position");
        m_trailPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("X").SetComponent<NumberInputComponent>().WithMinMaxStep(-1f, 1f, 0.005f).WithSensitivityCoef(0.25f)
            .WithTint(RedColor)
            .WithValue(trail.Position[0]).OnValueChanged += val =>
            {
                var t = data.TipTrails[m_selectedTipTrailIndex];
                t.Position[0] = val;
                ApplyToBothSabers(s => s.Data.SetTipTrail(m_selectedTipTrailIndex, t));
            };
        m_trailPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Y").SetComponent<NumberInputComponent>().WithMinMaxStep(-1f, 1f, 0.005f).WithSensitivityCoef(0.25f)
            .WithTint(GreenColor)
            .WithValue(trail.Position[1]).OnValueChanged += val =>
            {
                var t = data.TipTrails[m_selectedTipTrailIndex];
                t.Position[1] = val;
                ApplyToBothSabers(s => s.Data.SetTipTrail(m_selectedTipTrailIndex, t));
            };
        m_trailPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Z").SetComponent<NumberInputComponent>().WithMinMaxStep(-1f, 1f, 0.005f).WithSensitivityCoef(0.25f)
            .WithTint(BlueColor)
            .WithValue(trail.Position[2]).OnValueChanged += val =>
            {
                var t = data.TipTrails[m_selectedTipTrailIndex];
                t.Position[2] = val;
                ApplyToBothSabers(s => s.Data.SetTipTrail(m_selectedTipTrailIndex, t));
            };

        m_trailPanel.Content.AddSubHeader("Color");
        m_trailPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("R").SetComponent<NumberInputComponent>().WithMinMaxStep(-1f, 1f, 0.005f)
            .WithTint(RedColor)
            .WithValue(trail.Color[0]).OnValueChanged += val =>
            {
                var t = data.TipTrails[m_selectedTipTrailIndex];
                t.Color[0] = val;
                ApplyToBothSabers(s => s.Data.SetTipTrail(m_selectedTipTrailIndex, t));
            };
        m_trailPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("G").SetComponent<NumberInputComponent>().WithMinMaxStep(-1f, 1f, 0.005f)
            .WithTint(GreenColor)
            .WithValue(trail.Color[1]).OnValueChanged += val =>
            {
                var t = data.TipTrails[m_selectedTipTrailIndex];
                t.Color[1] = val;
                ApplyToBothSabers(s => s.Data.SetTipTrail(m_selectedTipTrailIndex, t));
            };
        m_trailPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("B").SetComponent<NumberInputComponent>().WithMinMaxStep(-1f, 1f, 0.005f)
            .WithTint(BlueColor)
            .WithValue(trail.Color[2]).OnValueChanged += val =>
            {
                var t = data.TipTrails[m_selectedTipTrailIndex];
                t.Color[2] = val;
                ApplyToBothSabers(s => s.Data.SetTipTrail(m_selectedTipTrailIndex, t));
            };
        m_trailPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Custom Blend").SetComponent<NumberInputComponent>().WithMinMaxStep(0f, 1f, 0.01f)
            .WithValue(trail.CustomBlend).OnValueChanged += val =>
            {
                var t = data.TipTrails[m_selectedTipTrailIndex];
                t.CustomBlend = val;
                ApplyToBothSabers(s => s.Data.SetTipTrail(m_selectedTipTrailIndex, t));
            };

        m_trailPanel.Content.AddSubHeader("Properties");
        m_trailPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Glow").SetComponent<NumberInputComponent>().WithMinMaxStep(0f, 1.5f, 0.005f)
            .WithValue(trail.Glow).OnValueChanged += val =>
            {
                var t = data.TipTrails[m_selectedTipTrailIndex];
                t.Glow = val;
                ApplyToBothSabers(s => s.Data.SetTipTrail(m_selectedTipTrailIndex, t));
            };
        m_trailPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Opacity").SetComponent<NumberInputComponent>().WithMinMaxStep(0f, 1f, 0.01f)
            .WithValue(trail.Opacity).OnValueChanged += val =>
            {
                var t = data.TipTrails[m_selectedTipTrailIndex];
                t.Opacity = val;
                ApplyToBothSabers(s => s.Data.SetTipTrail(m_selectedTipTrailIndex, t));
            };
        m_trailPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Width").SetComponent<NumberInputComponent>().WithMinMaxStep(0.001f, 0.05f, 0.001f).WithSensitivityCoef(0.02f)
            .WithValue(trail.Width).OnValueChanged += val =>
            {
                var t = data.TipTrails[m_selectedTipTrailIndex];
                t.Width = val;
                ApplyToBothSabers(s => s.Data.SetTipTrail(m_selectedTipTrailIndex, t));
            };
        m_trailPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Length (ms)").SetComponent<NumberInputComponent>().WithMinMaxStep(0f, 500f, 1f).WithSensitivityCoef(200)
            .WithValue(trail.Length).OnValueChanged += val =>
            {
                var t = data.TipTrails[m_selectedTipTrailIndex];
                t.Length = Mathf.RoundToInt(val);
                ApplyToBothSabers(s => s.Data.SetTipTrail(m_selectedTipTrailIndex, t));
            };
        m_trailPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Queue Offset").SetComponent<NumberInputComponent>().WithMinMaxStep(-10f, 10f, 1f)
            .WithValue(trail.QueueOffset).OnValueChanged += val =>
            {
                var t = data.TipTrails[m_selectedTipTrailIndex];
                t.QueueOffset = Mathf.RoundToInt(val);
                ApplyToBothSabers(s => s.Data.SetTipTrail(m_selectedTipTrailIndex, t));
            };
    }

    private void BuildBladeTrailEditor()
    {
        var data = EditingSaber.Data;
        data.EnsureDefaultTrails();

        var trail = data.BladeTrail!.Value;

        m_trailPanel.Content.AddSubHeader("Position");
        m_trailPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("X").SetComponent<NumberInputComponent>().WithMinMaxStep(-1f, 1f, 0.005f).WithSensitivityCoef(0.25f)
            .WithTint(RedColor)
            .WithValue(trail.Position[0]).OnValueChanged += val =>
            {
                var t = data.BladeTrail!.Value;
                t.Position[0] = val;
                data.SetBladeTrail(t);
            };
        m_trailPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Y").SetComponent<NumberInputComponent>().WithMinMaxStep(-1f, 1f, 0.005f).WithSensitivityCoef(0.25f)
            .WithTint(GreenColor)
            .WithValue(trail.Position[1]).OnValueChanged += val =>
            {
                var t = data.BladeTrail!.Value;
                t.Position[1] = val;
                data.SetBladeTrail(t);
            };
        m_trailPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Z").SetComponent<NumberInputComponent>().WithMinMaxStep(-1f, 1f, 0.005f).WithSensitivityCoef(0.25f)
            .WithTint(BlueColor)
            .WithValue(trail.Position[2]).OnValueChanged += val =>
            {
                var t = data.BladeTrail!.Value;
                t.Position[2] = val;
                data.SetBladeTrail(t);
            };

        m_trailPanel.Content.AddSubHeader("Color");
        m_trailPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("R").SetComponent<NumberInputComponent>().WithMinMaxStep(-1f, 1f, 0.005f)
            .WithTint(RedColor)
            .WithValue(trail.Color[0]).OnValueChanged += val =>
            {
                var t = data.BladeTrail!.Value;
                t.Color[0] = val;
                data.SetBladeTrail(t);
            };
        m_trailPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("G").SetComponent<NumberInputComponent>().WithMinMaxStep(-1f, 1f, 0.005f)
            .WithTint(GreenColor)
            .WithValue(trail.Color[1]).OnValueChanged += val =>
            {
                var t = data.BladeTrail!.Value;
                t.Color[1] = val;
                data.SetBladeTrail(t);
            };
        m_trailPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("B").SetComponent<NumberInputComponent>().WithMinMaxStep(-1f, 1f, 0.005f)
            .WithTint(BlueColor)
            .WithValue(trail.Color[2]).OnValueChanged += val =>
            {
                var t = data.BladeTrail!.Value;
                t.Color[2] = val;
                data.SetBladeTrail(t);
            };
        m_trailPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Custom Blend").SetComponent<NumberInputComponent>().WithMinMaxStep(0f, 1f, 0.01f)
            .WithValue(trail.CustomBlend).OnValueChanged += val =>
            {
                var t = data.BladeTrail!.Value;
                t.CustomBlend = val;
                data.SetBladeTrail(t);
            };

        m_trailPanel.Content.AddSubHeader("Properties");
        m_trailPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Glow").SetComponent<NumberInputComponent>().WithMinMaxStep(0f, 1.5f, 0.005f)
            .WithValue(trail.Glow).OnValueChanged += val =>
            {
                var t = data.BladeTrail!.Value;
                t.Glow = val;
                data.SetBladeTrail(t);
            };
        m_trailPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Opacity").SetComponent<NumberInputComponent>().WithMinMaxStep(0f, 1f, 0.01f)
            .WithValue(trail.Opacity).OnValueChanged += val =>
            {
                var t = data.BladeTrail!.Value;
                t.Opacity = val;
                data.SetBladeTrail(t);
            };
        m_trailPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Length (ms)").SetComponent<NumberInputComponent>().WithMinMaxStep(0f, 500f, 1f)
            .WithValue(trail.Length).OnValueChanged += val =>
            {
                var t = data.BladeTrail!.Value;
                t.Length = Mathf.RoundToInt(val);
                data.SetBladeTrail(t);
            };
        m_trailPanel.Content.AddChild<FieldComponent>().WithPreferredHeight(4)
            .WithLabel("Queue Offset").SetComponent<NumberInputComponent>().WithMinMaxStep(-10f, 10f, 1f)
            .WithValue(trail.QueueOffset).OnValueChanged += val =>
            {
                var t = data.BladeTrail!.Value;
                t.QueueOffset = Mathf.RoundToInt(val);
                data.SetBladeTrail(t);
            };
    }
}