using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using VainSabers.Config;
using VainSabers.Helpers;
using VainSabers.Sabers;
using VRUIControls;
using Zenject;

namespace VainSabers.Menu
{
    public class MenuPointerBlurController : IInitializable, ITickable, IDisposable, ILateTickable
    {
        internal static PluginConfig? StaticConfig;
        internal static bool ShouldHideLaser => StaticConfig != null && StaticConfig.Enabled && StaticConfig.LaserMode != LaserMode.Vanilla;
        internal static bool ShouldHidePointer => StaticConfig != null && StaticConfig.Enabled && StaticConfig.PointerMode != PointerMode.Vanilla;
        internal static bool ShouldHideAny => ShouldHideLaser || ShouldHidePointer;
        internal static bool IsAnyBlurEnabled => StaticConfig != null && StaticConfig.Enabled && (StaticConfig.LaserMode == LaserMode.VainSabers || StaticConfig.PointerMode == PointerMode.VainSabers);

        private readonly PluginConfig m_config;
        private readonly ColorSchemesSettings m_colorSchemesSettings;
        private VRPointer? m_vrPointer;

        private readonly Color defaultColorLeft = new Color32(0xC8, 0x14, 0x14, 0xFF);
        private readonly Color defaultColorRight = new Color32(0x28, 0x8E, 0xD2, 0xFF);

        private PointerBlurSet? m_leftSet;
        private PointerBlurSet? m_rightSet;

        private const float DefaultLaserLength = 10f;

        private static float s_lastPauseCheckTime;
        private static bool s_lastPauseResult;
        private static float s_lastPointerCheckTime;
        private float m_lastHideTime;

        // Registry populated by Harmony patches – no FindObjectsOfTypeAll every frame
        internal static readonly List<VRPointer> s_vrPointers = new();
        internal static readonly List<PauseMenuManager> s_pauseManagers = new();
        internal static readonly List<PauseController> s_pauseControllers = new();

        private class PointerBlurSet
        {
            public VRController controller = null!;
            public Transform viewAnchor = null!;
            public GameObject laserRoot = null!;
            public BlurSaberData laserData = null!;
            public BlurSaberPart laserPart = null!;
            public MovementTracker laserTracker = null!;
            public GameObject hitTrackerGO = null!;
            public GameObject dotSaberRoot = null!;
            public BlurSaber dotSaber = null!;
            public string lastDotPreset = "";
        }

        public MenuPointerBlurController(PluginConfig config, ColorSchemesSettings colorSchemesSettings)
        {
            m_config = config;
            m_colorSchemesSettings = colorSchemesSettings;
            StaticConfig = config;
        }

        private (Color left, Color right) GetSaberColors()
        {
            var selectedColorScheme = m_colorSchemesSettings.GetOverrideColorScheme();
            if (selectedColorScheme is null)
                return (defaultColorLeft, defaultColorRight);
            return (selectedColorScheme.saberAColor, selectedColorScheme.saberBColor);
        }

        private MenuStateHandler.ModPanelState m_lastPanelState;

        public void Initialize()
        {
            TryFindVRPointer();
            if (m_vrPointer == null)
                return;

            CreateSets();
            m_lastPanelState = new MenuStateHandler.ModPanelState(false, false, "");
            MenuStateHandler.ModPanelStateChanged += OnPanelStateChanged;
        }

        private void OnPanelStateChanged(MenuStateHandler.ModPanelState state)
        {
            // When exiting the editor (EditorOpen true -> false), respawn pointer presets
            // so any edits to the dot preset file (or current saber preset if dot uses it) are reloaded.
            bool wasEditorOpen = m_lastPanelState.EditorOpen;
            m_lastPanelState = state;
            if (wasEditorOpen && !state.EditorOpen)
            {
                RespawnPresets();
            }
        }

        public void RespawnPresets()
        {
            string desired = string.IsNullOrEmpty(m_config.MenuPointerDotPreset) ? "menupointer-dot" : m_config.MenuPointerDotPreset;
            var (colorLeft, colorRight) = GetSaberColors();
            foreach (var set in new[] { m_leftSet, m_rightSet })
            {
                if (set == null || set.dotSaber == null) continue;
                // Force reload even if preset name unchanged (file may have been overwritten by editor Save)
                set.dotSaber.SetPreset(desired);
                bool hasCustom = set.dotSaber.Data != null && set.dotSaber.Data.UseCustomTrails;
                set.dotSaber.SetSuppressDefaultTrails(!hasCustom);
                Color c = set == m_leftSet ? colorLeft : colorRight;
                set.dotSaber.SetColor(c);
                set.dotSaber.ClearHistoryAndResetMotion();
                set.lastDotPreset = desired;
            }
            // Also refresh laser colors in case color scheme changed while editor was open
            SyncLaserColors();
        }

        private void TryFindVRPointer()
        {
            // Prefer registry list (maintained by patches) – no allocation, no scene scan
            VRPointer? best = null;
            for (int i = 0; i < s_vrPointers.Count; i++)
            {
                var p = s_vrPointers[i];
                if (p != null && p.isActiveAndEnabled)
                {
                    best = p;
                    break;
                }
            }
            if (best == null && s_vrPointers.Count > 0)
                best = s_vrPointers[0];

            if (best == null)
            {
                // Fallback for early init before Awake patch ran
                var pointers = Resources.FindObjectsOfTypeAll<VRPointer>();
                foreach (var p in pointers)
                {
                    if (p != null && p.isActiveAndEnabled) { best = p; break; }
                }
                if (best == null && pointers.Length > 0) best = pointers[0];
                if (best == null) best = UnityEngine.Object.FindObjectOfType<VRPointer>();
            }

            m_vrPointer = best;
        }

        private void EnsureSetsUpToDate()
        {
            if (m_vrPointer == null) return;
            var curLeft = m_vrPointer._leftVRController;
            var curRight = m_vrPointer._rightVRController;
            if (curLeft == null || curRight == null) return;

            bool leftStale = m_leftSet == null || m_leftSet.controller != curLeft || m_leftSet.viewAnchor != curLeft.viewAnchorTransform;
            bool rightStale = m_rightSet == null || m_rightSet.controller != curRight || m_rightSet.viewAnchor != curRight.viewAnchorTransform;

            if (leftStale || rightStale)
            {
                // VRPointer or its controllers/viewAnchors changed (menu -> gameplay, or replay). Rebuild sets to follow current anchors.
                if (m_leftSet != null)
                {
                    UnityEngine.Object.Destroy(m_leftSet.laserRoot);
                    UnityEngine.Object.Destroy(m_leftSet.dotSaberRoot);
                    UnityEngine.Object.Destroy(m_leftSet.hitTrackerGO);
                    m_leftSet = null;
                }
                if (m_rightSet != null)
                {
                    UnityEngine.Object.Destroy(m_rightSet.laserRoot);
                    UnityEngine.Object.Destroy(m_rightSet.dotSaberRoot);
                    UnityEngine.Object.Destroy(m_rightSet.hitTrackerGO);
                    m_rightSet = null;
                }
                CreateSets();
                return;
            }

            // Same controllers but MovementTracker target may have been stale if we reused sets – ensure it points at current viewAnchor
            if (m_leftSet != null && m_leftSet.laserTracker != null && m_leftSet.laserTracker.Target != m_leftSet.viewAnchor)
            {
                m_leftSet.laserTracker.Target = m_leftSet.viewAnchor;
                m_leftSet.laserTracker.ClearHistory();
            }
            if (m_rightSet != null && m_rightSet.laserTracker != null && m_rightSet.laserTracker.Target != m_rightSet.viewAnchor)
            {
                m_rightSet.laserTracker.Target = m_rightSet.viewAnchor;
                m_rightSet.laserTracker.ClearHistory();
            }
        }

        private void CreateSets()
        {
            if (m_vrPointer == null) return;

            var leftController = m_vrPointer._leftVRController;
            var rightController = m_vrPointer._rightVRController;

            m_leftSet = CreateSet(leftController, true);
            m_rightSet = CreateSet(rightController, false);
        }

        private PointerBlurSet CreateSet(VRController controller, bool isLeft)
        {
            var viewAnchor = controller.viewAnchorTransform;
            var set = new PointerBlurSet
            {
                controller = controller,
                viewAnchor = viewAnchor
            };

            set.laserRoot = new GameObject($"MenuLaserBlurRoot_{(isLeft ? "Left" : "Right")}");
            set.laserRoot.transform.position = Vector3.zero;
            set.laserRoot.transform.rotation = Quaternion.identity;

            var (colorLeft, colorRight) = GetSaberColors();
            Color saberColor = isLeft ? colorLeft : colorRight;

            set.laserData = set.laserRoot.AddInitComponent<BlurSaberData>(m_config);
            set.laserData.IsLeftSaber = isLeft;
            set.laserData.CustomColor = saberColor;

            set.laserTracker = set.laserRoot.AddInitComponent<MovementTracker>(viewAnchor, m_config);

            var laserPartGO = new GameObject("LaserPart");
            laserPartGO.transform.SetParent(set.laserRoot.transform, false);
            laserPartGO.transform.localPosition = Vector3.zero;
            laserPartGO.transform.localRotation = Quaternion.identity;
            set.laserPart = laserPartGO.AddComponent<BlurSaberPart>();
            set.laserPart.Config = m_config;
            AssignMaterials(set.laserPart);
            ConfigureLaserPart(set.laserPart, saberColor);

            set.hitTrackerGO = new GameObject($"MenuDotHitTracker_{(isLeft ? "Left" : "Right")}");
            set.hitTrackerGO.transform.position = viewAnchor.position + viewAnchor.forward * DefaultLaserLength;
            set.hitTrackerGO.transform.rotation = viewAnchor.rotation;

            set.dotSaberRoot = new GameObject($"MenuDotSaberRoot_{(isLeft ? "Left" : "Right")}");
            set.dotSaberRoot.transform.position = Vector3.zero;
            set.dotSaberRoot.transform.rotation = Quaternion.identity;

            set.dotSaber = set.dotSaberRoot.AddInitComponent<BlurSaber>(set.hitTrackerGO.transform, m_config);
            string dotPreset = string.IsNullOrEmpty(m_config.MenuPointerDotPreset) ? "menupointer-dot" : m_config.MenuPointerDotPreset;
            set.dotSaber.SetPreset(dotPreset);
            // For menu pointers: use custom trails if present, but suppress default trails when no custom trails
            bool hasCustomTrails = set.dotSaber.Data != null && set.dotSaber.Data.UseCustomTrails;
            set.dotSaber.SetSuppressDefaultTrails(!hasCustomTrails);
            set.dotSaber.SetColor(saberColor);
            set.lastDotPreset = dotPreset;
            set.dotSaber.ClearHistoryAndResetMotion();

            set.laserRoot.SetActive(false);
            set.dotSaberRoot.SetActive(false);
            set.hitTrackerGO.SetActive(true);

            return set;
        }

        private static void AssignMaterials(BlurSaberPart part)
        {
            if (VainSabersAssets.NormalSaberMaterial != null)
                part.Material = VainSabersAssets.NormalSaberMaterial;
            if (VainSabersAssets.InvertedSaberMaterial != null)
                part.InvertedMaterial = VainSabersAssets.InvertedSaberMaterial;
            if (VainSabersAssets.NormalLitSaberMaterial != null)
                part.LitMaterial = VainSabersAssets.NormalLitSaberMaterial;
            if (VainSabersAssets.InvertedLitSaberMaterial != null)
                part.LitInvertedMaterial = VainSabersAssets.InvertedLitSaberMaterial;
        }

        private void ConfigureLaserPart(BlurSaberPart p, Color saberColor)
        {
            p.GeometryHandling = BlurSaberPart.GeometryType.Simple;
            p.Length = DefaultLaserLength;
            p.StartRadius = 0.0018f;
            p.EndRadius = 0.0018f;
            p.StartColor = saberColor;
            p.EndColor = saberColor;
            p.StartCustomColorWeight = 1f;
            p.EndCustomColorWeight = 1f;
            p.StartGlow = 1.5f;
            p.EndGlow = 1.5f;
            p.StartOpacity = 1f;
            p.EndOpacity = 1f;
            p.BlurFactor = Mathf.Clamp01(m_config.MenuPointerLaserBlurFactor);
            p.BlurFadeFactor = 16f;
            p.EnableEndCaps = true;
            p.EndCapExtension = 0.25f;
            p.BulgeAmount = 0f;
            p.MinimumRings = 4;
            p.Side = BlurSaberPart.SaberSide.Both;
            p.Lit = false;
            p.Inverted = false;
            p.DepthOffset = 0f;
            p.DisableGlowPass = false;
            p.DisableDepthPrepass = false;
        }

        private void SyncLaserColors()
        {
            var (colorLeft, colorRight) = GetSaberColors();
            if (m_leftSet != null)
            {
                m_leftSet.laserData.CustomColor = colorLeft;
                if (m_leftSet.laserPart != null)
                {
                    m_leftSet.laserPart.StartColor = colorLeft;
                    m_leftSet.laserPart.EndColor = colorLeft;
                }
                m_leftSet.dotSaber?.SetColor(colorLeft);
            }
            if (m_rightSet != null)
            {
                m_rightSet.laserData.CustomColor = colorRight;
                if (m_rightSet.laserPart != null)
                {
                    m_rightSet.laserPart.StartColor = colorRight;
                    m_rightSet.laserPart.EndColor = colorRight;
                }
                m_rightSet.dotSaber?.SetColor(colorRight);
            }
        }

        private static void ConfigureDotPart(BlurSaberPart p)
        {
            p.GeometryHandling = BlurSaberPart.GeometryType.Simple;
            p.Length = 0.025f;
            p.StartRadius = 0.008f;
            p.EndRadius = 0.008f;
            p.StartColor = new Color(0f, 0.2f, 1f, 1f);
            p.EndColor = new Color(0f, 0.2f, 1f, 1f);
            p.StartCustomColorWeight = 0f;
            p.EndCustomColorWeight = 0f;
            p.StartGlow = 0.7f;
            p.EndGlow = 0.7f;
            p.StartOpacity = 1f;
            p.EndOpacity = 1f;
            p.BlurFactor = 1f;
            p.BlurFadeFactor = 0.4f;
            p.EnableEndCaps = true;
            p.EndCapExtension = 1.15f;
            p.BulgeAmount = 1.0f;
            p.MinimumRings = 5;
            p.Side = BlurSaberPart.SaberSide.Both;
            p.Lit = false;
            p.Inverted = false;
            p.DepthOffset = 0f;
        }

        public void Tick()
        {
            // Refresh VRPointer only when necessary – use registry list, throttled.
            bool needPointerCheck = false;
            if (m_vrPointer == null || m_vrPointer.Equals(null))
                needPointerCheck = true;
            else if (Time.time - s_lastPointerCheckTime > 0.5f)
                needPointerCheck = true;

            if (needPointerCheck)
            {
                s_lastPointerCheckTime = Time.time;
                VRPointer? previous = m_vrPointer;
                if (previous == null || previous.Equals(null))
                {
                    TryFindVRPointer();
                }
                else
                {
                    VRPointer? active = null;
                    for (int i = 0; i < s_vrPointers.Count; i++)
                    {
                        var p = s_vrPointers[i];
                        if (p != null && p.isActiveAndEnabled) { active = p; break; }
                    }
                    if (active != null && active != previous)
                        TryFindVRPointer();
                }
            }

            if (m_vrPointer == null) return;
            EnsureSetsUpToDate();
            if (m_leftSet == null || m_rightSet == null) return;

            if (!m_config.Enabled)
            {
                SetBlurActive(false);
                RestoreOriginalPointers();
                return;
            }

            bool laserVanilla = m_config.LaserMode == LaserMode.Vanilla;
            bool pointerVanilla = m_config.PointerMode == PointerMode.Vanilla;
            bool bothVanilla = laserVanilla && pointerVanilla;

            if (bothVanilla)
            {
                SetBlurActive(false);
                RestoreOriginalPointers();
                return;
            }

            if (!m_vrPointer.isActiveAndEnabled && !IsPauseMenuActive())
            {
                SetBlurActive(false);
                return;
            }

            SyncOriginalVisibility();

            SyncLaserBlurFactor();
            SyncLaserColors();
            SyncDotPresets();

            var raycast = GetCurrentRaycast(out var lastController, out var lastWasRight);
            float distance = DefaultLaserLength;
            Vector3 hitPos = Vector3.zero;
            bool hasHit = false;
            if (raycast.HasValue)
            {
                var r = raycast.Value;
                if (r.gameObject != null)
                {
                    distance = r.distance;
                    hitPos = r.worldPosition;
                    hasHit = true;
                }
                else
                {
                    distance = DefaultLaserLength;
                    hasHit = false;
                }

                if (float.IsNaN(hitPos.x) || !hasHit)
                {
                    var anchor = lastController != null ? lastController.viewAnchorTransform : null;
                    if (anchor != null)
                        hitPos = anchor.position + anchor.forward * distance;
                }
            }
            else
            {
                distance = DefaultLaserLength;
                hasHit = false;
                var anchor = lastController != null ? lastController.viewAnchorTransform : null;
                if (anchor != null)
                    hitPos = anchor.position + anchor.forward * distance;
            }

            bool isLeftActive = !lastWasRight;
            if (lastController == null)
            {
                if (m_leftSet != null) UpdateSet(m_leftSet, distance, hasHit ? hitPos : m_leftSet.viewAnchor.position + m_leftSet.viewAnchor.forward * distance, hasHit, true);
                if (m_rightSet != null) UpdateSet(m_rightSet, distance, hasHit ? hitPos : m_rightSet.viewAnchor.position + m_rightSet.viewAnchor.forward * distance, hasHit, true);
                return;
            }

            if (m_leftSet != null)
            {
                bool active = isLeftActive;
                Vector3 leftHit = hasHit && active ? hitPos : m_leftSet.viewAnchor.position + m_leftSet.viewAnchor.forward * distance;
                UpdateSet(m_leftSet, distance, leftHit, hasHit && active, active);
            }
            if (m_rightSet != null)
            {
                bool active = !isLeftActive;
                Vector3 rightHit = hasHit && active ? hitPos : m_rightSet.viewAnchor.position + m_rightSet.viewAnchor.forward * distance;
                UpdateSet(m_rightSet, distance, rightHit, hasHit && active, active);
            }
        }

        private void SyncLaserBlurFactor()
        {
            float target = Mathf.Clamp01(m_config.MenuPointerLaserBlurFactor);
            if (m_leftSet != null && Mathf.Abs(m_leftSet.laserPart.BlurFactor - target) > 0.001f)
                m_leftSet.laserPart.BlurFactor = target;
            if (m_rightSet != null && Mathf.Abs(m_rightSet.laserPart.BlurFactor - target) > 0.001f)
                m_rightSet.laserPart.BlurFactor = target;
        }

        private void SyncDotPresets()
        {
            string desired = string.IsNullOrEmpty(m_config.MenuPointerDotPreset) ? "menupointer-dot" : m_config.MenuPointerDotPreset;
            var (colorLeft, colorRight) = GetSaberColors();
            if (m_leftSet != null && m_leftSet.lastDotPreset != desired)
            {
                m_leftSet.lastDotPreset = desired;
                m_leftSet.dotSaber.SetPreset(desired);
                bool hasCustom = m_leftSet.dotSaber.Data != null && m_leftSet.dotSaber.Data.UseCustomTrails;
                m_leftSet.dotSaber.SetSuppressDefaultTrails(!hasCustom);
                m_leftSet.dotSaber.SetColor(colorLeft);
                m_leftSet.dotSaber.ClearHistoryAndResetMotion();
            }
            if (m_rightSet != null && m_rightSet.lastDotPreset != desired)
            {
                m_rightSet.lastDotPreset = desired;
                m_rightSet.dotSaber.SetPreset(desired);
                bool hasCustom = m_rightSet.dotSaber.Data != null && m_rightSet.dotSaber.Data.UseCustomTrails;
                m_rightSet.dotSaber.SetSuppressDefaultTrails(!hasCustom);
                m_rightSet.dotSaber.SetColor(colorRight);
                m_rightSet.dotSaber.ClearHistoryAndResetMotion();
            }
        }

        private void UpdateSet(PointerBlurSet set, float distance, Vector3 hitPos, bool hasHit, bool active)
        {
            bool laserBlur = m_config.LaserMode == LaserMode.VainSabers;
            bool pointerBlur = m_config.PointerMode == PointerMode.VainSabers;

            if (active)
            {
                if (laserBlur)
                {
                    set.laserRoot.SetActive(true);
                    float targetLength = hasHit ? distance : DefaultLaserLength;
                    if (Mathf.Abs(set.laserPart.Length - targetLength) > 0.001f)
                        set.laserPart.Length = Mathf.Clamp(targetLength, 0.01f, 40f);

                    float targetEndOpacity = hasHit ? 1f : 0f;
                    if (Mathf.Abs(set.laserPart.EndOpacity - targetEndOpacity) > 0.001f)
                        set.laserPart.EndOpacity = targetEndOpacity;
                }
                else
                {
                    if (set.laserRoot.activeSelf)
                        set.laserPart.ResetMotion();
                    set.laserRoot.SetActive(false);
                }

                if (pointerBlur)
                {
                    set.hitTrackerGO.transform.position = hitPos;
                    set.hitTrackerGO.transform.rotation = set.viewAnchor.rotation;

                    bool wasShowing = set.dotSaberRoot.activeSelf;
                    bool shouldShow = hasHit;

                    if (shouldShow)
                    {
                        if (!wasShowing)
                        {
                            set.hitTrackerGO.transform.position = hitPos;
                            set.hitTrackerGO.transform.rotation = set.viewAnchor.rotation;
                            set.dotSaber.ClearHistoryAndResetMotion();
                        }
                        set.dotSaberRoot.SetActive(true);
                    }
                    else
                    {
                        if (wasShowing)
                            set.dotSaber.ClearHistoryAndResetMotion();
                        else
                            set.dotSaber.ClearHistoryAndResetMotion();
                        set.dotSaberRoot.SetActive(false);
                    }
                }
                else
                {
                    if (set.dotSaberRoot.activeSelf)
                        set.dotSaber.ClearHistoryAndResetMotion();
                    set.dotSaberRoot.SetActive(false);
                }
            }
            else
            {
                if (set.laserRoot.activeSelf)
                    set.laserPart.ResetMotion();
                set.laserRoot.SetActive(false);
                if (set.dotSaberRoot.activeSelf)
                    set.dotSaber.ClearHistoryAndResetMotion();
                set.dotSaberRoot.SetActive(false);
            }
        }

        private void SetBlurActive(bool active)
        {
            if (m_leftSet != null)
            {
                m_leftSet.laserRoot.SetActive(active);
                if (!active) m_leftSet.laserPart.ResetMotion();
                m_leftSet.dotSaberRoot.SetActive(active);
                if (!active)
                    m_leftSet.dotSaber.ClearHistoryAndResetMotion();
            }
            if (m_rightSet != null)
            {
                m_rightSet.laserRoot.SetActive(active);
                if (!active) m_rightSet.laserPart.ResetMotion();
                m_rightSet.dotSaberRoot.SetActive(active);
                if (!active)
                    m_rightSet.dotSaber.ClearHistoryAndResetMotion();
            }
        }

        private void SyncOriginalVisibility()
        {
            if (m_vrPointer == null) return;
            bool laserShouldBeVisible = !m_config.Enabled || m_config.LaserMode == LaserMode.Vanilla;
            bool pointerShouldBeVisible = !m_config.Enabled || m_config.PointerMode == PointerMode.Vanilla;

            bool needSync = Time.time - m_lastHideTime > 0.25f;
            if (!needSync)
            {
                var l = m_vrPointer._leftLaserPointer;
                var r = m_vrPointer._rightLaserPointer;
                bool laserVisible = (l != null && l._renderer != null && l._renderer.enabled) || (r != null && r._renderer != null && r._renderer.enabled);
                if (laserVisible != laserShouldBeVisible) needSync = true;
            }
            if (!needSync) return;
            m_lastHideTime = Time.time;
            SetLaserRendererEnabled(m_vrPointer._leftLaserPointer, laserShouldBeVisible);
            SetLaserRendererEnabled(m_vrPointer._rightLaserPointer, laserShouldBeVisible);
            SetCursorRenderersEnabled(m_vrPointer._leftCursorTransform, pointerShouldBeVisible);
            SetCursorRenderersEnabled(m_vrPointer._rightCursorTransform, pointerShouldBeVisible);
        }

        private void HideOriginalPointers()
        {
            if (m_vrPointer == null) return;
            bool laserShouldBeVisible = !m_config.Enabled || m_config.LaserMode == LaserMode.Vanilla;
            bool pointerShouldBeVisible = !m_config.Enabled || m_config.PointerMode == PointerMode.Vanilla;
            
            if (!laserShouldBeVisible)
            {
                SetLaserRendererEnabled(m_vrPointer._leftLaserPointer, false);
                SetLaserRendererEnabled(m_vrPointer._rightLaserPointer, false);
            }
            else
            {
                SetLaserRendererEnabled(m_vrPointer._leftLaserPointer, true);
                SetLaserRendererEnabled(m_vrPointer._rightLaserPointer, true);
            }
            if (!pointerShouldBeVisible)
            {
                SetCursorRenderersEnabled(m_vrPointer._leftCursorTransform, false);
                SetCursorRenderersEnabled(m_vrPointer._rightCursorTransform, false);
            }
            else
            {
                SetCursorRenderersEnabled(m_vrPointer._leftCursorTransform, true);
                SetCursorRenderersEnabled(m_vrPointer._rightCursorTransform, true);
            }
        }

        private void RestoreOriginalPointers()
        {
            if (m_vrPointer == null) return;
            SetLaserRendererEnabled(m_vrPointer._leftLaserPointer, true);
            SetLaserRendererEnabled(m_vrPointer._rightLaserPointer, true);
            SetCursorRenderersEnabled(m_vrPointer._leftCursorTransform, true);
            SetCursorRenderersEnabled(m_vrPointer._rightCursorTransform, true);
        }

        private static void SetLaserRendererEnabled(VRLaserPointer? laser, bool enabled)
        {
            if (laser == null) return;
            if (laser._renderer != null)
                laser._renderer.enabled = enabled;
            foreach (var r in laser.GetComponentsInChildren<Renderer>(true))
                r.enabled = enabled;
        }

        private static void SetCursorRenderersEnabled(Transform? cursor, bool enabled)
        {
            if (cursor == null) return;
            foreach (var r in cursor.GetComponentsInChildren<Renderer>(true))
                r.enabled = enabled;
            foreach (var g in cursor.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                g.enabled = enabled;
            foreach (var cr in cursor.GetComponentsInChildren<CanvasRenderer>(true))
                cr.cull = !enabled;
        }

        private RaycastResult? GetCurrentRaycast(out VRController? controller, out bool wasRight)
        {
            controller = null;
            wasRight = true;
            if (m_vrPointer == null)
                return null;
            controller = m_vrPointer._lastSelectedVrController;
            wasRight = m_vrPointer._lastSelectedControllerWasRight;
            var data = m_vrPointer._currentPointerData;
            if (data == null) return null;
            return data.pointerCurrentRaycast;
        }

        private static bool IsPauseMenuActive()
        {
            // No FindObjectsOfTypeAll – iterate registry lists populated by patches
            for (int i = 0; i < s_pauseManagers.Count; i++)
            {
                var m = s_pauseManagers[i];
                if (m != null && m.gameObject.activeInHierarchy && m.enabled)
                    return true;
            }
            for (int i = 0; i < s_pauseControllers.Count; i++)
            {
                var c = s_pauseControllers[i];
                if (c != null && c.gameObject.activeInHierarchy && c.enabled && c._paused != PauseController.PauseState.Playing)
                    return true;
            }
            // Fallback throttled scan only if registry is empty (early init)
            if (s_pauseManagers.Count == 0 && s_pauseControllers.Count == 0)
            {
                if (Time.time - s_lastPauseCheckTime < 0.25f)
                    return s_lastPauseResult;
                s_lastPauseCheckTime = Time.time;
                try
                {
                    var managers = Resources.FindObjectsOfTypeAll<PauseMenuManager>();
                    foreach (var m in managers)
                        if (m != null && m.gameObject.activeInHierarchy && m.enabled) { s_lastPauseResult = true; return true; }
                    var controllers = Resources.FindObjectsOfTypeAll<PauseController>();
                    foreach (var c in controllers)
                        if (c != null && c.gameObject.activeInHierarchy && c.enabled && c._paused != PauseController.PauseState.Playing) { s_lastPauseResult = true; return true; }
                }
                catch { }
                s_lastPauseResult = false;
            }
            return false;
        }

        public void LateTick()
        {
            if (ShouldHideAny)
            {
                SyncOriginalVisibility();
            }
            if (IsAnyBlurEnabled)
            {
                Shader.SetGlobalFloat("_VainSaberBlurSoftness", m_config.BlurSoftness);
            }
        }

        public void Dispose()
        {
            MenuStateHandler.ModPanelStateChanged -= OnPanelStateChanged;
            RestoreOriginalPointers();
            if (m_leftSet != null)
            {
                UnityEngine.Object.Destroy(m_leftSet.laserRoot);
                UnityEngine.Object.Destroy(m_leftSet.dotSaberRoot);
                UnityEngine.Object.Destroy(m_leftSet.hitTrackerGO);
            }
            if (m_rightSet != null)
            {
                UnityEngine.Object.Destroy(m_rightSet.laserRoot);
                UnityEngine.Object.Destroy(m_rightSet.dotSaberRoot);
                UnityEngine.Object.Destroy(m_rightSet.hitTrackerGO);
            }
        }
    }

    [HarmonyPatch(typeof(VRPointer), "RefreshLaserPointerAndLaserHit")]
    internal static class VRPointerRefreshPatch
    {
        static void Postfix(VRPointer __instance)
        {
            if (!MenuPointerBlurController.ShouldHideAny)
                return;

            bool hideLaser = MenuPointerBlurController.ShouldHideLaser;
            bool hidePointer = MenuPointerBlurController.ShouldHidePointer;

            if (hideLaser)
            {
                var leftLaser = __instance._leftLaserPointer;
                var rightLaser = __instance._rightLaserPointer;
                if (leftLaser != null)
                {
                    if (leftLaser._renderer != null) leftLaser._renderer.enabled = false;
                    foreach (var r in leftLaser.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
                }
                if (rightLaser != null)
                {
                    if (rightLaser._renderer != null) rightLaser._renderer.enabled = false;
                    foreach (var r in rightLaser.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
                }
            }

            if (hidePointer)
            {
                var leftCursor = __instance._leftCursorTransform;
                var rightCursor = __instance._rightCursorTransform;
                if (leftCursor != null)
                {
                    foreach (var r in leftCursor.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
                    foreach (var g in leftCursor.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) g.enabled = false;
                    foreach (var cr in leftCursor.GetComponentsInChildren<CanvasRenderer>(true)) cr.cull = true;
                }
                if (rightCursor != null)
                {
                    foreach (var r in rightCursor.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
                    foreach (var g in rightCursor.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) g.enabled = false;
                    foreach (var cr in rightCursor.GetComponentsInChildren<CanvasRenderer>(true)) cr.cull = true;
                }
            }
        }
    }

    [HarmonyPatch(typeof(VRPointer), "CreateLaserPointers")]
    internal static class VRPointerCreateLaserPatch
    {
        static void Postfix(VRPointer __instance)
        {
            if (!MenuPointerBlurController.ShouldHideLaser) return;
            var lf = __instance._leftLaserPointer;
            var rf = __instance._rightLaserPointer;
            if (lf != null)
            {
                if (lf._renderer != null) lf._renderer.enabled = false;
                foreach (var r in lf.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            }
            if (rf != null)
            {
                if (rf._renderer != null) rf._renderer.enabled = false;
                foreach (var r in rf.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            }
        }
    }

    // Registry patches – add each component to the static lists once, no per-frame FindObjectsOfTypeAll
    [HarmonyPatch(typeof(VRPointer), "Awake")]
    internal static class VRPointerRegistryPatch
    {
        static void Postfix(VRPointer __instance)
        {
            var list = MenuPointerBlurController.s_vrPointers;
            if (!list.Contains(__instance))
                list.Add(__instance);
        }
    }

    [HarmonyPatch(typeof(PauseMenuManager), "Awake")]
    internal static class PauseMenuManagerRegistryPatch
    {
        static void Postfix(PauseMenuManager __instance)
        {
            var list = MenuPointerBlurController.s_pauseManagers;
            if (!list.Contains(__instance))
                list.Add(__instance);
        }
    }

    [HarmonyPatch(typeof(PauseController), "Start")]
    internal static class PauseControllerRegistryPatch
    {
        static void Postfix(PauseController __instance)
        {
            var list = MenuPointerBlurController.s_pauseControllers;
            if (!list.Contains(__instance))
                list.Add(__instance);
        }
    }
}
