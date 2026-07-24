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
using VainSabers.Helpers;
using VainSabers.Menu;
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
            panel.Destroy();
        
        if (!state.EditorOpen)
            return;
        
        panel = SimpleFloatingPanel.Create(new Vector2(20, 20), new Vector3(0, 1.2f, 1.5f));
        panel.Show();

        var image = panel.gameObject.AddInitChild<ImageComponent>();
        image.Color = new Color(0.5f, 0.5f, 0.6f, 1.0f);
    }
}