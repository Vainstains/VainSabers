using System;
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
        internal static bool IsBlurEnabled => StaticConfig != null && StaticConfig.MenuPointerBlurEnabled;

        private readonly PluginConfig m_config;
        private VRPointer? m_vrPointer;

        private PointerBlurSet? m_leftSet;
        private PointerBlurSet? m_rightSet;

        private const float DefaultLaserLength = 10f;

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

        public MenuPointerBlurController(PluginConfig config)
        {
            m_config = config;
            StaticConfig = config;
        }

        public void Initialize()
        {
            TryFindVRPointer();
            if (m_vrPointer == null)
                return;

            CreateSets();
        }

        private void TryFindVRPointer()
        {
            var pointers = Resources.FindObjectsOfTypeAll<VRPointer>();
            if (pointers.Length > 0)
                m_vrPointer = pointers[0];
            else
                m_vrPointer = UnityEngine.Object.FindObjectOfType<VRPointer>();
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

            set.laserData = set.laserRoot.AddInitComponent<BlurSaberData>(m_config);
            set.laserData.IsLeftSaber = isLeft;
            set.laserData.CustomColor = new Color(0f, 0.7f, 1f, 1f);

            set.laserTracker = set.laserRoot.AddInitComponent<MovementTracker>(viewAnchor, m_config);

            var laserPartGO = new GameObject("LaserPart");
            laserPartGO.transform.SetParent(set.laserRoot.transform, false);
            laserPartGO.transform.localPosition = Vector3.zero;
            laserPartGO.transform.localRotation = Quaternion.identity;
            set.laserPart = laserPartGO.AddComponent<BlurSaberPart>();
            set.laserPart.Config = m_config;
            AssignMaterials(set.laserPart);
            ConfigureLaserPart(set.laserPart);

            set.hitTrackerGO = new GameObject($"MenuDotHitTracker_{(isLeft ? "Left" : "Right")}");
            set.hitTrackerGO.transform.position = viewAnchor.position + viewAnchor.forward * DefaultLaserLength;
            set.hitTrackerGO.transform.rotation = viewAnchor.rotation;

            set.dotSaberRoot = new GameObject($"MenuDotSaberRoot_{(isLeft ? "Left" : "Right")}");
            set.dotSaberRoot.transform.position = Vector3.zero;
            set.dotSaberRoot.transform.rotation = Quaternion.identity;

            set.dotSaber = set.dotSaberRoot.AddInitComponent<BlurSaber>(set.hitTrackerGO.transform, m_config);
            string dotPreset = string.IsNullOrEmpty(m_config.MenuPointerDotPreset) ? "menupointer-dot" : m_config.MenuPointerDotPreset;
            set.dotSaber.SetPreset(dotPreset);
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

        private void ConfigureLaserPart(BlurSaberPart p)
        {
            p.GeometryHandling = BlurSaberPart.GeometryType.Simple;
            p.Length = DefaultLaserLength;
            p.StartRadius = 0.0018f;
            p.EndRadius = 0.0018f;
            p.StartColor = new Color(0f, 0.7f, 1f, 1f);
            p.EndColor = new Color(0f, 0.7f, 1f, 1f);
            p.StartCustomColorWeight = 0f;
            p.EndCustomColorWeight = 0f;
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
            if (m_vrPointer == null)
            {
                TryFindVRPointer();
                if (m_vrPointer == null) return;
                if (m_leftSet == null || m_rightSet == null)
                    CreateSets();
            }

            bool blurEnabled = m_config.MenuPointerBlurEnabled;

            if (!blurEnabled)
            {
                SetBlurActive(false);
                RestoreOriginalPointers();
                return;
            }

            HideOriginalPointers();

            SyncLaserBlurFactor();
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
            if (m_leftSet != null && m_leftSet.lastDotPreset != desired)
            {
                m_leftSet.lastDotPreset = desired;
                m_leftSet.dotSaber.SetPreset(desired);
                m_leftSet.dotSaber.ClearHistoryAndResetMotion();
            }
            if (m_rightSet != null && m_rightSet.lastDotPreset != desired)
            {
                m_rightSet.lastDotPreset = desired;
                m_rightSet.dotSaber.SetPreset(desired);
                m_rightSet.dotSaber.ClearHistoryAndResetMotion();
            }
        }

        private void UpdateSet(PointerBlurSet set, float distance, Vector3 hitPos, bool hasHit, bool active)
        {
            if (active)
            {
                set.laserRoot.SetActive(true);

                float targetLength = hasHit ? distance : DefaultLaserLength;
                if (Mathf.Abs(set.laserPart.Length - targetLength) > 0.001f)
                    set.laserPart.Length = Mathf.Clamp(targetLength, 0.01f, 40f);

                float targetEndOpacity = hasHit ? 1f : 0f;
                if (Mathf.Abs(set.laserPart.EndOpacity - targetEndOpacity) > 0.001f)
                    set.laserPart.EndOpacity = targetEndOpacity;

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

        private void HideOriginalPointers()
        {
            if (m_vrPointer == null) return;
            SetLaserRendererEnabled(m_vrPointer._leftLaserPointer, false);
            SetLaserRendererEnabled(m_vrPointer._rightLaserPointer, false);
            SetCursorRenderersEnabled(m_vrPointer._leftCursorTransform, false);
            SetCursorRenderersEnabled(m_vrPointer._rightCursorTransform, false);
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

        public void LateTick()
        {
            if (m_config.MenuPointerBlurEnabled)
            {
                HideOriginalPointers();
                Shader.SetGlobalFloat("_VainSaberBlurSoftness", m_config.BlurSoftness);
            }
        }

        public void Dispose()
        {
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
        static bool Prefix(VRPointer __instance, PointerEventData pointerData)
        {
            if (!MenuPointerBlurController.IsBlurEnabled)
                return true;
            
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

            var leftCursor = __instance._leftCursorTransform;
            var rightCursor = __instance._rightCursorTransform;
            if (leftCursor != null)
            {
                foreach (var r in leftCursor.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
                foreach (var g in leftCursor.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) g.enabled = false;
            }
            if (rightCursor != null)
            {
                foreach (var r in rightCursor.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
                foreach (var g in rightCursor.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) g.enabled = false;
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(VRPointer), "CreateLaserPointers")]
    internal static class VRPointerCreateLaserPatch
    {
        static void Postfix(VRPointer __instance)
        {
            if (!MenuPointerBlurController.IsBlurEnabled) return;
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
}
