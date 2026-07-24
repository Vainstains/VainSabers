#if false

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using HMUI;
using VRUIControls;
using IPA.Logging;
using VainSabers;

namespace FloatingPanelExample
{
    // From-scratch floating panel - NO BSML dependency
    public class CustomFloatingPanel : MonoBehaviour
    {
        private Canvas _canvas;
        private CanvasGroup _canvasGroup;

        public static CustomFloatingPanel Create(Vector2 size, Vector3 position)
        {
            var go = new GameObject("CustomFloatingPanel");
            go.layer = 5; // UI layer

            var panel = go.AddComponent<CustomFloatingPanel>();
            panel.Setup(size, position);

            return panel;
        }

        private void Setup(Vector2 size, Vector3 position)
        {
            // Main RectTransform
            var rect = gameObject.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;

            // Canvas for rendering
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord2;

            // Canvas scaler for proper sizing
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 3.44f;
            scaler.referencePixelsPerUnit = 10;

            // VR raycaster for pointer interaction
            gameObject.AddComponent<VRGraphicRaycaster>();

            // Canvas group for modal behavior
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();

            // Set world position and scale
            transform.position = position;
            transform.localScale = new Vector3(0.02f, 0.02f, 0.02f);

            // Set canvas size
            rect.sizeDelta = size;

            // Add curved canvas settings for VR (radius 140 like Beat Saber)
            var curvedSettings = gameObject.AddComponent<CurvedCanvasSettings>();
            curvedSettings.SetRadius(140f);
        }

        public void SetContent(Transform contentParent)
        {
            contentParent.SetParent(transform, false);

            var contentRect = contentParent.GetComponent<RectTransform>();
            if (contentRect != null)
            {
                contentRect.anchorMin = Vector2.zero;
                contentRect.anchorMax = Vector2.one;
                contentRect.sizeDelta = Vector2.zero;
                contentRect.anchoredPosition = Vector2.zero;
            }
        }

        public void Show()
        {
            gameObject.SetActive(true);
            _canvasGroup.alpha = 1f;
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;
        }

        public void Hide()
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }

        public void Destroy()
        {
            Destroy(gameObject);
        }
    }

    // Example usage - builds UI on the custom floating panel
    public class FloatingPanelUIBuilder
    {
        public static void BuildUI(CustomFloatingPanel panel)
        {
            // Create a container for our UI elements
            var container = new GameObject("UIContainer");
            container.transform.SetParent(panel.transform, false);

            var containerRect = container.AddComponent<RectTransform>();
            containerRect.anchorMin = Vector2.zero;
            containerRect.anchorMax = Vector2.one;
            containerRect.sizeDelta = Vector2.zero;

            var layout = container.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 5f;
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            // 1. Hello World Button
            CreateButton(container.transform, "Hello World", "Click me!", () =>
            {
                Plugin.Log.Info("Button clicked!");
            });

            // 2. Lorem Ipsum Text
            CreateText(container.transform,
                "Lorem ipsum dolor sit amet, consectetur adipiscing elit. " +
                "Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.");

            // 3. Image
            CreateImage(container.transform);

            // 4. Dropdown
            CreateDropdown(container.transform,
                new List<string> { "Option A", "Option B", "Option C" },
                (idx, val) => Plugin.Log.Info($"Selected: {val}"));
        }

        private static void CreateButton(Transform parent, string label, string text, Action onClick)
        {
            var btn = new GameObject(label);
            btn.transform.SetParent(parent, false);

            var rect = btn.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 10);

            var layout = btn.AddComponent<LayoutElement>();
            layout.minHeight = 10;
            layout.preferredHeight = 10;

            var button = btn.AddComponent<Button>();

            // Background
            var bg = new GameObject("BG");
            bg.transform.SetParent(btn.transform, false);
            var bgRect = bg.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            var bgImg = bg.AddComponent<ImageView>();
            bgImg.color = new Color(0.2f, 0.4f, 0.8f);

            // Text
            var txt = new GameObject("Text");
            txt.transform.SetParent(btn.transform, false);
            var txtRect = txt.AddComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.sizeDelta = Vector2.zero;
            var tmp = txt.AddComponent<CurvedTextMeshPro>();
            tmp.text = text;
            tmp.fontSize = 5f;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;

            button.onClick.AddListener(() => onClick?.Invoke());
        }

        private static void CreateText(Transform parent, string content)
        {
            var txt = new GameObject("Text");
            txt.transform.SetParent(parent, false);
            var rect = txt.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 20);

            var layout = txt.AddComponent<LayoutElement>();
            layout.minHeight = 20;
            layout.preferredHeight = 20;

            var tmp = txt.AddComponent<CurvedTextMeshPro>();
            tmp.text = content;
            tmp.fontSize = 4f;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.TopLeft;
            tmp.enableWordWrapping = true;
        }

        private static void CreateImage(Transform parent)
        {
            var img = new GameObject("Image");
            img.transform.SetParent(parent, false);
            var rect = img.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 30);

            var layout = img.AddComponent<LayoutElement>();
            layout.minHeight = 30;
            layout.preferredHeight = 30;

            var imageView = img.AddComponent<ImageView>();

            // Placeholder texture
            var tex = new Texture2D(2, 2);
            tex.SetPixels(new[] { Color.red, Color.green, Color.blue, Color.yellow });
            tex.Apply();

            imageView.sprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), Vector2.one * 0.5f);
        }

        private static void CreateDropdown(Transform parent, List<string> options, Action<int, string> onChange)
        {
            var obj = new GameObject("Dropdown");
            obj.transform.SetParent(parent, false);
            var rect = obj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 12);

            var layout = obj.AddComponent<LayoutElement>();
            layout.minHeight = 12;
            layout.preferredHeight = 12;

            var dropdown = obj.AddComponent<TMP_Dropdown>();

            // Background
            var bg = new GameObject("BG");
            bg.transform.SetParent(obj.transform, false);
            var bgRect = bg.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            var bgImg = bg.AddComponent<ImageView>();
            bgImg.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);

            // Caption text
            var caption = new GameObject("Caption");
            caption.transform.SetParent(obj.transform, false);
            var captionRect = caption.AddComponent<RectTransform>();
            captionRect.anchorMin = Vector2.zero;
            captionRect.anchorMax = Vector2.one;
            captionRect.sizeDelta = Vector2.zero;
            var captionTmp = caption.AddComponent<CurvedTextMeshPro>();
            captionTmp.fontSize = 4f;
            captionTmp.color = Color.white;
            captionTmp.alignment = TextAlignmentOptions.Left;

            // Configure dropdown
            dropdown.captionText = captionTmp;
            dropdown.options = options.Select(o => new TMP_Dropdown.OptionData(o)).ToList();
            dropdown.onValueChanged.AddListener((idx) =>
            {
                onChange?.Invoke(idx, options[idx]);
            });
        }
    }
}

#endif