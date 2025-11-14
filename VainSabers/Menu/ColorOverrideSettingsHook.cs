using SiraUtil.Affinity;

namespace VainSabers.Menu;

// stolen from:
// https://github.com/qqrz997/MenuSaberColors/blob/master/MenuSaberColors/Menu/ColorOverrideSettingsHook.cs
// thanks :3
internal class ColorOverrideSettingsHook : IAffinity
{
    private readonly MenuSaberManager saberManager;

    private ColorOverrideSettingsHook(MenuSaberManager saberColorManager) => 
        saberManager = saberColorManager;

    [AffinityPatch(typeof(ColorsOverrideSettingsPanelController),
        nameof(ColorsOverrideSettingsPanelController.HandleOverrideColorsToggleValueChanged))]
    private void HandleOverrideColorsToggleValueChanged_Postfix()
    {
        saberManager.NotifyColorSchemeUpdated();
    }

    [AffinityPatch(typeof(ColorsOverrideSettingsPanelController),
        nameof(ColorsOverrideSettingsPanelController.HandleDropDownDidSelectCellWithIdx))]
    private void HandleDropDownDidSelectCellWithIdx_PostFix()
    {
        saberManager.NotifyColorSchemeUpdated();
    }
}