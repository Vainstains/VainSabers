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
            get => m_currentPartRight != null ? m_currentPartRight.RotX : 0f;
            set => ApplyToBoth(p => p.RotX = value);
        }

        [UIValue("PartRotationY")]
        private float PartRotationY
        {
            get => m_currentPartRight != null ? m_currentPartRight.RotY : 0f;
            set => ApplyToBoth(p => p.RotY = value);
        }

        [UIValue("PartRotationZ")]
        private float PartRotationZ
        {
            get => m_currentPartRight != null ? m_currentPartRight.RotZ : 0f;
            set => ApplyToBoth(p => p.RotZ = value);
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

        [UIValue("PartStartOpacity")]
        private float PartStartOpacity
        {
            get => m_currentPartRight?.StartOpacity ?? 1f;
            set => ApplyToBoth(p => p.StartOpacity = Mathf.Clamp01(value));
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

        [UIValue("PartEndOpacity")]
        private float PartEndOpacity
        {
            get => m_currentPartRight?.EndOpacity ?? 1f;
            set => ApplyToBoth(p => p.EndOpacity = Mathf.Clamp01(value));
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

        [UIValue("PartEnableRoundedNormals")]
        private bool PartEnableRoundedNormals
        {
            get => m_currentPartRight?.EnableRoundedNormals ?? true;
            set => ApplyToBoth(p => p.EnableRoundedNormals = value);
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

        [UIValue("PartDepthOffset")]
        private float PartDepthOffset
        {
            get => m_currentPartRight?.DepthOffset ?? 0f;
            set => ApplyToBoth(p => p.DepthOffset = value);
        }

        [UIValue("PartRimFactor")]
        private float PartRimFactor
        {
            get => m_currentPartRight?.RimFactor ?? 0f;
            set => ApplyToBoth(p => p.RimFactor = value);
        }

        [UIValue("PartRimPower")]
        private float PartRimPower
        {
            get => m_currentPartRight?.RimPower ?? 3f;
            set => ApplyToBoth(p => p.RimPower = value);
        }

        [UIValue("PartRimPerpendicular")]
        private float PartRimPerpendicular
        {
            get => m_currentPartRight?.RimPerpendicular ?? 0f;
            set => ApplyToBoth(p => p.RimPerpendicular = value);
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
        [UIComponent("StartOpacity")] private SliderSetting StartOpacity = null!;

        [UIComponent("EndRadius")] private SliderSetting EndRadius = null!;
        [UIComponent("EndColorR")] private SliderSetting EndColorR = null!;
        [UIComponent("EndColorG")] private SliderSetting EndColorG = null!;
        [UIComponent("EndColorB")] private SliderSetting EndColorB = null!;
        [UIComponent("EndWeight")] private SliderSetting EndWeight = null!;
        [UIComponent("EndGlow")] private SliderSetting EndGlow = null!;
        [UIComponent("EndOpacity")] private SliderSetting EndOpacity = null!;

        [UIComponent("InvertedToggle")] private ToggleSetting InvertedToggle = null!;
        [UIComponent("LitToggle")] private ToggleSetting LitToggle = null!;
        [UIComponent("BlurFactor")] private SliderSetting BlurFactor = null!;
        [UIComponent("BlurFadeFactor")] private SliderSetting BlurFadeFactor = null!;
        [UIComponent("UseEndCapsToggle")] private ToggleSetting UseEndCapsToggle = null!;
        [UIComponent("EnableRoundedNormalsToggle")] private ToggleSetting EnableRoundedNormalsToggle = null!;
        [UIComponent("EndCapExtensionFactor")] private SliderSetting EndCapExtensionFactor = null!;
        [UIComponent("BulgeAmount")] private SliderSetting BulgeAmount = null!;
        [UIComponent("MinimumRings")] private SliderSetting MinimumRings = null!;
        [UIComponent("RenderQueueOffset")] private SliderSetting RenderQueueOffset = null!;
        [UIComponent("DepthOffset")] private SliderSetting DepthOffset = null!;

        [UIComponent("RimFactor")] private SliderSetting PartRimFactorSetting = null!;
        [UIComponent("RimPower")] private SliderSetting PartRimPowerSetting = null!;
        [UIComponent("RimPerpendicular")] private SliderSetting PartRimPerpendicularSetting = null!;
        
#pragma warning restore CS0649
        // Refresh all bound UI values
        private void RefreshUI()
        {
            var part = m_currentPartRight ?? m_currentPartLeft;
            float length = 0.1f;
            if (part == null)
            {
                PosX.Value = PosY.Value = PosZ.Value = 0f;
                RotX.Value = RotY.Value = RotZ.Value = 0f;
                Length.Value = Mathf.Pow(length, 1f / 3f);

                HueShift.Value = 0.0f;

                StartRadius.Value = 0.01f;
                StartColorR.Value = StartColorG.Value = StartColorB.Value = 0f;
                StartWeight.Value = StartGlow.Value = 0f;
                StartOpacity.Value = 1f;

                EndRadius.Value = 0.01f;
                EndColorR.Value = EndColorG.Value = EndColorB.Value = 0f;
                EndWeight.Value = EndGlow.Value = 0f;
                EndOpacity.Value = 1f;

                InvertedToggle.Value = false;
                BlurFactor.Value = 1f;
                BlurFadeFactor.Value = 1f;
                UseEndCapsToggle.Value = true;
                EnableRoundedNormalsToggle.Value = true;
                BulgeAmount.Value = 0f;
                MinimumRings.Value = 4;
                RenderQueueOffset.Value = 0f;
                DepthOffset.Value = 0f;

                PartRimFactorSetting.Value = 0f;
                PartRimPowerSetting.Value = 3f;
                PartRimPerpendicularSetting.Value = 0f;

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
            
            length = part.Length;
            Length.Value = Mathf.Pow(length, 1f / 3f);
            
            HueShift.Value = part.HueShift;

            StartRadius.Value = part.StartRadius;
            StartColorR.Value = part.StartColor.r;
            StartColorG.Value = part.StartColor.g;
            StartColorB.Value = part.StartColor.b;
            StartWeight.Value = part.StartCustomColorWeight;
            StartGlow.Value = part.StartGlow;
            StartOpacity.Value = part.StartOpacity;

            EndRadius.Value = part.EndRadius;
            EndColorR.Value = part.EndColor.r;
            EndColorG.Value = part.EndColor.g;
            EndColorB.Value = part.EndColor.b;
            EndWeight.Value = part.EndCustomColorWeight;
            EndGlow.Value = part.EndGlow;
            EndOpacity.Value = part.EndOpacity;

            InvertedToggle.Value = part.Inverted;
            BlurFactor.Value = part.BlurFactor;
            BlurFadeFactor.Value = part.BlurFadeFactor;
            UseEndCapsToggle.Value = part.EnableEndCaps;
            EnableRoundedNormalsToggle.Value = part.EnableRoundedNormals;
            EndCapExtensionFactor.Value = part.EndCapExtension;
            BulgeAmount.Value = part.BulgeAmount;
            MinimumRings.Value = part.MinimumRings;
            RenderQueueOffset.Value = part.RenderQueueOffset;
            DepthOffset.Value = part.DepthOffset;

            PartRimFactorSetting.Value = part.RimFactor;
            PartRimPowerSetting.Value = part.RimPower;
            PartRimPerpendicularSetting.Value = part.RimPerpendicular;
            
            LitToggle.Value = part.Lit;
        }
        
        private string GetUniquePartName(BlurSaberData data)
        {
            var existing = new HashSet<string>(data.Components.Select(c => c.gameObject.name));
            var i = 1;
            while (existing.Contains($"Part {i}"))
                i++;
            return $"Part {i}";
        }
        
        [UIAction("AddNewPart")]
        private void AddNewPart()
        {
            var right = MenuStateHandler.Sabers.right;
            var left = MenuStateHandler.Sabers.left;

            if (right?.Data == null || left?.Data == null)
                return;

            // Create new part on both sabers
            var newName = GetUniquePartName(right.Data);
            
            var newRightPart = right.Data.AddComponent(newName);
            var newLeftPart = left.Data.AddComponent(newName);

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

            if (partRight != null) rightData.RemoveComponent(partRight);
            if (partLeft != null) leftData.RemoveComponent(partLeft);
            
            m_currentPartRight = null;
            m_currentPartLeft = null;
            m_selectedPartName = string.Empty;

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
            screenSize: new Vector2(180f, 250.0f),
            createHandle: false,
            position: new Vector3(0f, -69420f, 0f),
            rotation: Quaternion.Euler(0f, 0f, 0f));
        
        floatingScreen.GetComponent<Canvas>().sortingOrder = 10;

        floatingScreen.SetRootViewController(editorViewController, ViewController.AnimationType.None);
        floatingScreen.transform.localScale *= 0.45f;
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
            floatingScreen.transform.position = new Vector3(0f, 1.2f, 1.5f);
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