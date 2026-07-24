using HMUI;
using IPA.Utilities;
using UnityEngine;
using UnityEngine.UI;
using VainSabers.Helpers;
using VRUIControls;

namespace VainSabers.UI;

public class SimpleFloatingPanel : MonoBehaviour, IUIParent
{
    private Canvas m_canvas = null!;
    private CanvasGroup m_canvasGroup = null!;

    private void Init(Vector2 size)
    {
        var rect = gameObject.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;

        m_canvas = gameObject.AddComponent<Canvas>();
        m_canvas.renderMode = RenderMode.WorldSpace;
        m_canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord2;
        m_canvas.sortingOrder = 10;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 3.44f;
        scaler.referencePixelsPerUnit = 10;

        var raycaster = gameObject.AddComponent<VRGraphicRaycaster>();
        raycaster.SetField("_physicsRaycaster", UIResources.Raycaster);

        m_canvasGroup = gameObject.AddComponent<CanvasGroup>();
        m_canvasGroup.alpha = 0f;
        m_canvasGroup.interactable = false;
        m_canvasGroup.blocksRaycasts = false;

        rect.sizeDelta = size;
        transform.localScale = new Vector3(0.02f, 0.02f, 0.02f);

        var curvedSettings = gameObject.AddComponent<CurvedCanvasSettings>();
        curvedSettings.SetRadius(140f);
    }

    public void Show()
    {
        gameObject.SetActive(true);
        m_canvasGroup.alpha = 1f;
        m_canvasGroup.interactable = true;
        m_canvasGroup.blocksRaycasts = true;
    }

    public void Hide()
    {
        m_canvasGroup.alpha = 0f;
        m_canvasGroup.interactable = false;
        m_canvasGroup.blocksRaycasts = false;
    }
    
    public void Destroy()
    {
        Destroy(gameObject);
    }

    public static SimpleFloatingPanel Create(Vector2 size, Vector3 position)
    {
        var go = new GameObject("SimpleFloatingPanel");
        go.layer = 5;

        var panel = go.AddInitComponent<SimpleFloatingPanel>(size);

        go.transform.SetParent(null);
        go.transform.position = position;

        return panel;
    }

    public T AddChild<T>() where T : UIComponent
    {
        return gameObject.AddInitChild<T>();
    }
}