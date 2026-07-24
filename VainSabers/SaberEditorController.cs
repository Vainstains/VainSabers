using UnityEngine;
using VainSabers.Config;
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
        
        panel = SimpleFloatingPanel.Create(new Vector2(20, 20), new Vector3(0, 1.2f, 1.5f));
        panel.Show();

        var bg = panel.AddChild<RoundRectComponent>().ToFill();
        bg.Color = new Color(0.1f, 0.1f, 0.1f, 1.0f);
        bg.IsRaycastTarget = true;

        var button = bg.AddChild<TextButtonComponent>().ToTopEdge()
            .InsetLeft(1).InsetRight(1).ExtendBottom(4).Move(0, -1).WithText("sus");

        button.OnClick += () =>
        {
            Plugin.Log.Info("amogus");
        };
        
        var dropdown = panel.AddChild<DropdownComponent>().ToTopEdge()
            .InsetLeft(1).InsetRight(1).ExtendBottom(4).Move(0, -6);
        dropdown.SetOptions(["Option A", "Option B", "Option C"]);
        dropdown.OnSelectionChanged += idx => Plugin.Log.Info($"Selected {idx}: {dropdown.SelectedValue}");
        
        // more complex stuff to be added later
    }
}