using Quintessential.Settings;

namespace LoopFromHere;

public class LoopFromHereSettings {
    public static LoopFromHereSettings Instance => LoopFromHere.Instance.Settings as LoopFromHereSettings;

    [SettingsLabelKey("loop_from_here.settings.edit_loop_regions")]
    public Keybinding EditLoopRegions = new("L");
}
