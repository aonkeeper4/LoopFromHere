using Quintessential.Settings;

namespace LoopFromHere;

internal class LoopFromHereSettings {
    public static LoopFromHereSettings Instance => LoopFromHere.Instance.Settings as LoopFromHereSettings;

    [SettingsLabelKey("loop_from_here.settings.edit_loop_regions")]
    public Keybinding EditLoopRegions = new("L");
    [SettingsLabelKey("loop_from_here.settings.instruction_edits_affect_loop_regions_by_default")]
    public bool InstructionEditsAffectLoopRegionsByDefault = false; // long name go brrr
}
