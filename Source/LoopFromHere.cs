using LoopFromHere.Components;
using Quintessential;

namespace LoopFromHere;

internal class LoopFromHere : QuintessentialMod, IDataMod {
    public static LoopFromHere Instance { get; }
    public override string ModId => "loop_from_here";

    public override void Load() { }
    public override void PostLoad() { }
    public override void Unload() { }

    public override void LoadContent() {
        LoopFromHereComponent.Register();
    }
    public override void LoadCompatContent() { }
    public override void FinaliseContent() { }
}
