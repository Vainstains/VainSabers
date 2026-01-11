using System;
using System.Collections.Generic;
using System.Linq;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components.Settings;
using BeatSaberMarkupLanguage.FloatingScreen;
using BeatSaberMarkupLanguage.ViewControllers;
using HMUI;
using UnityEngine;
using VainSabers.Config;
using VainSabers.Menu;
using VainSabers.Sabers;

namespace VainSabers;

internal class SaberEditorController : MonoBehaviour
{
    [ViewDefinition("VainSabers.editor.bsml")]
    private class EditorController : BSMLAutomaticViewController
    {
        private static List<object> GetPartNames()
        {
            var parts = new List<string>();

            var rightSaber = MenuStateHandler.Sabers.right;
            if (rightSaber == null)
                return parts.Cast<object>().ToList();

            var data = rightSaber.Data;
            if (data == null)
                return parts.Cast<object>().ToList();
            
            data.RefreshComponentList();
            foreach (var part in data.Components)
            {
                if (part != null)
                    parts.Add(part.gameObject.name);
            }

            return parts.Cast<object>().ToList();
        }

        internal void UpdatePartDropDown()
        {
            if (PartDropDown == null)
                return;
            
            PartNames = GetPartNames();

            PartDropDown.Values = PartNames;
            PartDropDown.UpdateChoices();
            
            if (PartNames.Count > 0)
            {
                SelectedPartName = PartNames[0].ToString();
                PartDropDown.Value = SelectedPartName;
            }
            else
            {
                SelectedPartName = "";
                PartDropDown.Value = null;
                m_currentPartRight = null;
                m_currentPartLeft = null;
                RefreshUI();
            }
        }

        [UIComponent("PartDropdown")]
#pragma warning disable CS0649
        public DropDownListSetting PartDropDown = null!;
#pragma warning restore CS0649

        [UIValue("PartNames")] private List<object> PartNames = new List<object>();

        private string m_selectedPartName = "";

        private BlurSaberPart? m_currentPartRight;
        private BlurSaberPart? m_currentPartLeft;

        [UIValue("SelectedPartName")]
        private string SelectedPartName
        {
            get => m_selectedPartName;
            set
            {
                if (m_selectedPartName == value)
                    return;

                m_selectedPartName = value;

                var right = MenuStateHandler.Sabers.right;
                var left = MenuStateHandler.Sabers.left;

                m_currentPartRight = right?.Data?.FindComponent(value);
                m_currentPartLeft = left?.Data?.FindComponent(value);

                RefreshUI();
            }
        }

        private void ApplyToBoth(Action<BlurSaberPart> action)
        {
            if (m_currentPartRight != null) action(m_currentPartRight);
            if (m_currentPartLeft != null) action(m_currentPartLeft);
        }
        
        // thank chatgpt for the following code:

        // --- UI-bound properties --- //
        // Position
        [UIValue("PartPositionX")]
        private float PartPositionX
        {
            get => m_currentPartRight != null ? m_currentPartRight.transform.localPosition.x : 0f;
            set => ApplyToBoth(p =>
            {
                var pos = p.transform.localPosition;
                pos.x = value;
                p.transform.localPosition = pos;
            });
        }

        [UIValue("PartPositionY")]
        private float PartPositionY
        {
            get => m_currentPartRight != null ? m_currentPartRight.transform.localPosition.y : 0f;
            set => ApplyToBoth(p =>
            {
                var pos = p.transform.localPosition;
                pos.y = value;
                p.transform.localPosition = pos;
            });
        }

        [UIValue("PartPositionZ")]
        private float PartPositionZ
        {
            get => m_currentPartRight != null ? m_currentPartRight.transform.localPosition.z : 0f;
            set => ApplyToBoth(p =>
            {
                var pos = p.transform.localPosition;
                pos.z = value;
                p.transform.localPosition = pos;
            });
        }

        // ---------------- Rotation ----------------
        [UIValue("PartRotationX")]
        private float PartRotationX
        {
            get => m_currentPartRight != null ? m_currentPartRight.transform.localEulerAngles.x : 0f;
            set => ApplyToBoth(p =>
            {
                var rot = p.transform.localEulerAngles;
                rot.x = value;
                p.transform.localEulerAngles = rot;
            });
        }

        [UIValue("PartRotationY")]
        private float PartRotationY
        {
            get => m_currentPartRight != null ? m_currentPartRight.transform.localEulerAngles.y : 0f;
            set => ApplyToBoth(p =>
            {
                var rot = p.transform.localEulerAngles;
                rot.y = value;
                p.transform.localEulerAngles = rot;
            });
        }

        [UIValue("PartRotationZ")]
        private float PartRotationZ
        {
            get => m_currentPartRight != null ? m_currentPartRight.transform.localEulerAngles.z : 0f;
            set => ApplyToBoth(p =>
            {
                var rot = p.transform.localEulerAngles;
                rot.z = value;
                p.transform.localEulerAngles = rot;
            });
        }

        // ---------------- Dimensions ----------------
        [UIValue("PartLength")]
        private float PartLength
        {
            get
            {
                float real = m_currentPartRight?.Length ?? 0.1f;
                return Mathf.Pow(real, 0.3333333f);
            }
            set
            {
                float real = value * value * value;
                ApplyToBoth(p => p.Length = Mathf.Clamp(real, 0.0001f, 1.0f));
            }
        }

        [UIAction("LengthFormatter")]
        private string LengthFormatter(float sliderValue)
        {
            float real = sliderValue * sliderValue * sliderValue;
            return real.ToString("0.###");
        }
        
        [UIValue("PartHueShift")]
        private float PartHueShift
        {
            get => m_currentPartRight?.HueShift ?? 0.0f;
            set => ApplyToBoth(p => p.HueShift = value);
        }

        // ---------------- Start Properties ----------------
        [UIValue("PartStartRadius")]
        private float PartStartRadius
        {
            get => m_currentPartRight?.StartRadius ?? 0.01f;
            set => ApplyToBoth(p => p.StartRadius = Mathf.Max(0.0001f, value));
        }

        [UIValue("PartStartColorR")]
        private float PartStartColorR
        {
            get => m_currentPartRight?.StartColor.r ?? 1f;
            set => ApplyToBoth(p =>
            {
                var c = p.StartColor;
                c.r = value;
                p.StartColor = c;
            });
        }

        [UIValue("PartStartColorG")]
        private float PartStartColorG
        {
            get => m_currentPartRight?.StartColor.g ?? 1f;
            set => ApplyToBoth(p =>
            {
                var c = p.StartColor;
                c.g = value;
                p.StartColor = c;
            });
        }

        [UIValue("PartStartColorB")]
        private float PartStartColorB
        {
            get => m_currentPartRight?.StartColor.b ?? 1f;
            set => ApplyToBoth(p =>
            {
                var c = p.StartColor;
                c.b = value;
                p.StartColor = c;
            });
        }

        [UIValue("PartStartCustomWeight")]
        private float PartStartCustomWeight
        {
            get => m_currentPartRight?.StartCustomColorWeight ?? 1f;
            set => ApplyToBoth(p => p.StartCustomColorWeight = Mathf.Clamp01(value));
        }

        [UIValue("PartStartGlow")]
        private float PartStartGlow
        {
            get => m_currentPartRight?.StartGlow ?? 1f;
            set => ApplyToBoth(p => p.StartGlow = Mathf.Max(0f, value));
        }

        // ---------------- End Properties ----------------
        [UIValue("PartEndRadius")]
        private float PartEndRadius
        {
            get => m_currentPartRight?.EndRadius ?? 0.01f;
            set => ApplyToBoth(p => p.EndRadius = Mathf.Max(0.0001f, value));
        }

        [UIValue("PartEndColorR")]
        private float PartEndColorR
        {
            get => m_currentPartRight?.EndColor.r ?? 1f;
            set => ApplyToBoth(p =>
            {
                var c = p.EndColor;
                c.r = value;
                p.EndColor = c;
            });
        }

        [UIValue("PartEndColorG")]
        private float PartEndColorG
        {
            get => m_currentPartRight?.EndColor.g ?? 1f;
            set => ApplyToBoth(p =>
            {
                var c = p.EndColor;
                c.g = value;
                p.EndColor = c;
            });
        }

        [UIValue("PartEndColorB")]
        private float PartEndColorB
        {
            get => m_currentPartRight?.EndColor.b ?? 1f;
            set => ApplyToBoth(p =>
            {
                var c = p.EndColor;
                c.b = value;
                p.EndColor = c;
            });
        }

        [UIValue("PartEndCustomWeight")]
        private float PartEndCustomWeight
        {
            get => m_currentPartRight?.EndCustomColorWeight ?? 1f;
            set => ApplyToBoth(p => p.EndCustomColorWeight = Mathf.Clamp01(value));
        }

        [UIValue("PartEndGlow")]
        private float PartEndGlow
        {
            get => m_currentPartRight?.EndGlow ?? 1f;
            set => ApplyToBoth(p => p.EndGlow = Mathf.Max(0f, value));
        }

        // ---------------- Other ----------------
        [UIValue("PartInverted")]
        private bool PartInverted
        {
            get => m_currentPartRight?.Inverted ?? false;
            set => ApplyToBoth(p => p.Inverted = value);
        }
        
        [UIValue("PartLit")]
        private bool PartLit
        {
            get => m_currentPartRight?.Lit ?? false;
            set => ApplyToBoth(p => p.Lit = value);
        }

        [UIValue("PartBlurFactor")]
        private float PartBlurFactor
        {
            get => m_currentPartRight?.BlurFactor ?? 1f;
            set => ApplyToBoth(p => p.BlurFactor = Mathf.Clamp(value, 0f, 1f));
        }
        
        [UIValue("PartBlurFadeFactor")]
        private float PartBlurFadeFactor
        {
            get => m_currentPartRight?.BlurFadeFactor ?? 1f;
            set => ApplyToBoth(p => p.BlurFadeFactor = Mathf.Clamp(value, 0f, 5f));
        }
        
        [UIValue("PartUseEndCaps")]
        private bool PartUseEndCaps
        {
            get => m_currentPartRight?.EnableEndCaps ?? true;
            set => ApplyToBoth(p => p.EnableEndCaps = value);
        }
        
        [UIValue("EndCapExtension")]
        private float EndCapExtension
        {
            get => m_currentPartRight?.EndCapExtension ?? 1f;
            set => ApplyToBoth(p => p.EndCapExtension = Mathf.Clamp(value, 0f, 3f));
        }
        
        [UIValue("PartBulgeAmount")]
        private float PartBulgeAmount
        {
            get => m_currentPartRight?.BulgeAmount ?? 0f;
            set => ApplyToBoth(p => p.BulgeAmount = Mathf.Clamp(value, -1f, 1f));
        }

        [UIValue("PartMinimumRings")]
        private float PartMinimumRings
        {
            get => m_currentPartRight?.MinimumRings ?? 4;
            set => ApplyToBoth(p => p.MinimumRings = Mathf.Clamp((int)value, 2, 10));
        }
        
        [UIValue("PartRenderQueueOffset")]
        private float PartRenderQueueOffset
        {
            get => m_currentPartRight?.RenderQueueOffset ?? 0;
            set => ApplyToBoth(p => p.RenderQueueOffset = Mathf.RoundToInt(value));
        }

        [UIAction("#post-parse")]
        private void PostParse()
        {
            UpdatePartDropDown();
        }
        
#pragma warning disable CS0649
        [UIComponent("PosX")] private SliderSetting PosX = null!;
        [UIComponent("PosY")] private SliderSetting PosY = null!;
        [UIComponent("PosZ")] private SliderSetting PosZ = null!;

        [UIComponent("RotX")] private SliderSetting RotX = null!;
        [UIComponent("RotY")] private SliderSetting RotY = null!;
        [UIComponent("RotZ")] private SliderSetting RotZ = null!;

        [UIComponent("Length")] private SliderSetting Length = null!;
        
        [UIComponent("HueShift")] private SliderSetting HueShift = null!;

        [UIComponent("StartRadius")] private SliderSetting StartRadius = null!;
        [UIComponent("StartColorR")] private SliderSetting StartColorR = null!;
        [UIComponent("StartColorG")] private SliderSetting StartColorG = null!;
        [UIComponent("StartColorB")] private SliderSetting StartColorB = null!;
        [UIComponent("StartWeight")] private SliderSetting StartWeight = null!;
        [UIComponent("StartGlow")] private SliderSetting StartGlow = null!;

        [UIComponent("EndRadius")] private SliderSetting EndRadius = null!;
        [UIComponent("EndColorR")] private SliderSetting EndColorR = null!;
        [UIComponent("EndColorG")] private SliderSetting EndColorG = null!;
        [UIComponent("EndColorB")] private SliderSetting EndColorB = null!;
        [UIComponent("EndWeight")] private SliderSetting EndWeight = null!;
        [UIComponent("EndGlow")] private SliderSetting EndGlow = null!;

        [UIComponent("InvertedToggle")] private ToggleSetting InvertedToggle = null!;
        [UIComponent("LitToggle")] private ToggleSetting LitToggle = null!;
        [UIComponent("BlurFactor")] private SliderSetting BlurFactor = null!;
        [UIComponent("BlurFadeFactor")] private SliderSetting BlurFadeFactor = null!;
        [UIComponent("UseEndCapsToggle")] private ToggleSetting UseEndCapsToggle = null!;
        [UIComponent("EndCapExtensionFactor")] private SliderSetting EndCapExtensionFactor = null!;
        [UIComponent("BulgeAmount")] private SliderSetting BulgeAmount = null!;
        [UIComponent("MinimumRings")] private SliderSetting MinimumRings = null!;
        [UIComponent("RenderQueueOffset")] private SliderSetting RenderQueueOffset = null!;
        
#pragma warning restore CS0649
        // Refresh all bound UI values
        private void RefreshUI()
        {
            var part = m_currentPartRight ?? m_currentPartLeft;
            if (part == null)
            {
                PosX.Value = PosY.Value = PosZ.Value = 0f;
                RotX.Value = RotY.Value = RotZ.Value = 0f;
                Length.Value = 0.1f;

                HueShift.Value = 0.0f;

                StartRadius.Value = 0.01f;
                StartColorR.Value = StartColorG.Value = StartColorB.Value = 0f;
                StartWeight.Value = StartGlow.Value = 0f;

                EndRadius.Value = 0.01f;
                EndColorR.Value = EndColorG.Value = EndColorB.Value = 0f;
                EndWeight.Value = EndGlow.Value = 0f;

                InvertedToggle.Value = false;
                BlurFactor.Value = 1f;
                BlurFadeFactor.Value = 1f;
                UseEndCapsToggle.Value = true;
                BulgeAmount.Value = 0f;
                MinimumRings.Value = 4;
                RenderQueueOffset.Value = 0f;
                LitToggle.Value = false;
                
                return;
            }

            var t = part.transform;
            PosX.Value = t.localPosition.x;
            PosY.Value = t.localPosition.y;
            PosZ.Value = t.localPosition.z;

            RotX.Value = t.localEulerAngles.x;
            RotY.Value = t.localEulerAngles.y;
            RotZ.Value = t.localEulerAngles.z;

            Length.Value = part.Length;
            
            HueShift.Value = part.HueShift;

            StartRadius.Value = part.StartRadius;
            StartColorR.Value = part.StartColor.r;
            StartColorG.Value = part.StartColor.g;
            StartColorB.Value = part.StartColor.b;
            StartWeight.Value = part.StartCustomColorWeight;
            StartGlow.Value = part.StartGlow;

            EndRadius.Value = part.EndRadius;
            EndColorR.Value = part.EndColor.r;
            EndColorG.Value = part.EndColor.g;
            EndColorB.Value = part.EndColor.b;
            EndWeight.Value = part.EndCustomColorWeight;
            EndGlow.Value = part.EndGlow;

            InvertedToggle.Value = part.Inverted;
            BlurFactor.Value = part.BlurFactor;
            BlurFadeFactor.Value = part.BlurFadeFactor;
            UseEndCapsToggle.Value = part.EnableEndCaps;
            EndCapExtensionFactor.Value = part.EndCapExtension;
            BulgeAmount.Value = part.BulgeAmount;
            MinimumRings.Value = part.MinimumRings;
            RenderQueueOffset.Value = part.RenderQueueOffset;
            
            LitToggle.Value = part.Lit;
        }
        
        [UIAction("AddNewPart")]
        private void AddNewPart()
        {
            var right = MenuStateHandler.Sabers.right;
            var left = MenuStateHandler.Sabers.left;

            if (right?.Data == null || left?.Data == null)
                return;

            // Create new part on both sabers
            var newRightPart = right.Data.AddComponent($"Part {right.Data.ComponentCount + 1}");
            var newLeftPart = left.Data.AddComponent($"Part {left.Data.ComponentCount + 1}");

            // Refresh dropdown
            UpdatePartDropDown();

            // Select the newly created part
            SelectedPartName = newRightPart.gameObject.name;
            PartDropDown.Value = SelectedPartName;

            // Refresh UI values
            RefreshUI();
        }

        [UIAction("RemoveSelectedPart")]
        private void RemoveSelectedPart()
        {
            if (string.IsNullOrEmpty(SelectedPartName))
                return;

            var right = MenuStateHandler.Sabers.right;
            var left = MenuStateHandler.Sabers.left;

            var rightData = right?.Data;
            var leftData = left?.Data;

            if (rightData == null || leftData == null)
                return;

            var partRight = rightData.FindComponent(SelectedPartName);
            var partLeft = leftData.FindComponent(SelectedPartName);

            // Remove from both
            if (partRight != null)
                rightData.RemoveComponent(partRight);
            if (partLeft != null)
                leftData.RemoveComponent(partLeft);

            // Refresh dropdown and UI
            UpdatePartDropDown();
        }
        
        // end chatgpt segment
    }

    private bool wasOpen = false;
    private readonly EditorController editorViewController = BeatSaberUI.CreateViewController<EditorController>();
    private FloatingScreen? floatingScreen;

    private PluginConfig config = null!;

    public void Init(PluginConfig config)
    {
        this.config = config;
    }
    private void Awake()
    {
        floatingScreen = FloatingScreen.CreateFloatingScreen(
            screenSize: new Vector2(100f, 210.0f),
            createHandle: false,
            position: new Vector3(0f, -69420f, 0f),
            rotation: Quaternion.Euler(20f, 0f, 0f));

        floatingScreen.SetRootViewController(editorViewController, ViewController.AnimationType.None);
        floatingScreen.transform.localScale *= 0.35f;
        MenuStateHandler.ModPanelStateChanged += StateChanged;
    }

    private void Start()
    {
        Invoke(nameof(UpdateUI), 0.1f);
    }

    private void UpdateUI()
    {
        editorViewController.UpdatePartDropDown();
    }

    private void OnDestroy()
    {
        MenuStateHandler.ModPanelStateChanged -= StateChanged;
    }

    private void StateChanged(MenuStateHandler.ModPanelState state)
    {
        if (floatingScreen == null)
            return;

        if (state.EditorOpen)
        {
            floatingScreen.transform.position = new Vector3(0f, 0.85f, 0.8f);
            wasOpen = true;
        }
        else
        {
            floatingScreen.transform.localPosition = new Vector3(0f, -69420f, 0f);
            if (wasOpen)
            {
                try
                {
                    if (state.EditingPreset != "")
                    {
                        MenuStateHandler.Sabers.right.Data.SaveToFile(
                            Config.ConfigUtil.GetSaberProfile(state.EditingPreset));

                        MenuStateHandler.Sabers.right.SetPreset(state.EditingPreset);
                        MenuStateHandler.Sabers.left.SetPreset(state.EditingPreset);
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Log?.Error($"Failed saving saber profile on editor close: {ex}");
                }
            }

            wasOpen = false;
        }
        
        editorViewController.UpdatePartDropDown();
    }
}