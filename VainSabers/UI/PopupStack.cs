using System.Collections.Generic;
using System.Linq;
using HMUI;
using IPA.Utilities;
using UnityEngine;
using UnityEngine.UI;
using VRUIControls;

namespace VainSabers.UI;

/// <summary>
/// Manages stacking of world-space popup canvases so child popups render above parents
/// and only the topmost blocker receives click-away events.
/// </summary>
public static class PopupStack
{
    private class Entry
    {
        public Canvas Canvas = null!;
        public ButtonComponent Blocker = null!;
    }

    private static readonly List<Entry> s_stack = new();
    private const int BaseOrder = 20;
    private const int Step = 5;

    public static void Register(Canvas canvas, ButtonComponent blocker)
    {
        if (canvas == null) return;
        if (s_stack.Any(e => e.Canvas == canvas))
            return;

        s_stack.Add(new Entry { Canvas = canvas, Blocker = blocker });
        Refresh();
    }

    public static void Unregister(Canvas canvas)
    {
        if (canvas == null) return;
        int idx = s_stack.FindIndex(e => e.Canvas == canvas);
        if (idx < 0) return;
        // disable blocker immediately for the closing popup
        var entry = s_stack[idx];
        if (entry.Blocker != null)
        {
            entry.Blocker.IsInteractable = false;
            var bc = entry.Blocker.GetComponent<Canvas>();
            if (bc != null) bc.enabled = false;
        }

        s_stack.RemoveAt(idx);
        Refresh();
    }

    public static bool IsTopmost(Canvas canvas)
    {
        if (s_stack.Count == 0) return false;
        return s_stack[^1].Canvas == canvas;
    }

    public static bool IsTopmost(ButtonComponent blocker)
    {
        if (s_stack.Count == 0) return false;
        return s_stack[^1].Blocker == blocker;
    }

    private static void Refresh()
    {
        for (int i = 0; i < s_stack.Count; i++)
        {
            var e = s_stack[i];
            if (e.Canvas == null) continue;
            e.Canvas.overrideSorting = true;
            e.Canvas.sortingOrder = BaseOrder + i * Step;

            bool isTop = i == s_stack.Count - 1;
            if (e.Blocker != null)
            {
                // Only topmost blocker should be interactable.
                e.Blocker.IsInteractable = isTop;

                // Ensure blocker has its own canvas so it renders above all lower popup content
                // regardless of hierarchy sibling order. Blocker canvas is just below its popup.
                EnsureBlockerCanvas(e.Blocker, e.Canvas.sortingOrder - 1);
                // Also ensure blocker canvas is enabled only when its popup is topmost
                var blockerCanvas = e.Blocker.GetComponent<Canvas>();
                if (blockerCanvas != null)
                    blockerCanvas.enabled = isTop;
            }
        }
    }

    private static void EnsureBlockerCanvas(ButtonComponent blocker, int sortingOrder)
    {
        if (blocker == null) return;
        var go = blocker.gameObject;
        var canvas = go.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord2;
            var scaler = go.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = go.AddComponent<CanvasScaler>();
                scaler.dynamicPixelsPerUnit = 3.44f;
                scaler.referencePixelsPerUnit = 10;
            }
            var raycaster = go.GetComponent<VRGraphicRaycaster>();
            if (raycaster == null)
            {
                raycaster = go.AddComponent<VRGraphicRaycaster>();
                raycaster.SetField("_physicsRaycaster", UIResources.Raycaster);
            }
        }
        canvas.overrideSorting = true;
        canvas.sortingOrder = sortingOrder;
        canvas.enabled = true;
    }

    // Debug helper
    public static int Count => s_stack.Count;
}
