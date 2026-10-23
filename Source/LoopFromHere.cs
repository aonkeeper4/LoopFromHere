using System;
using LoopFromHere.Components;
using LoopFromHere.UI;
using Quintessential;

namespace LoopFromHere;

internal class LoopFromHere : QuintessentialMod, IDataMod {
    public override string ModId => "loop_from_here";

    public static LoopFromHere Instance { get; }
    public override Type SettingsType => typeof(LoopFromHereSettings);

    public override void Load() { }
    public override void PostLoad() { }
    public override void Unload() { }

    public override void LoadContent() {
        LoopRegionComponent.Register();

        LoopRegionUI.LoadContent();
    }
    public override void LoadCompatContent() { }
    public override void FinaliseContent() { }
}
