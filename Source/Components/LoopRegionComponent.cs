using System;
using Quintessential;
using Quintessential.Components;
using Quintessential.Serialization;

namespace LoopFromHere.Components;

internal class LoopRegionComponent : IPartComponent {
    public static readonly Identifier Id = LoopFromHere.Instance.GetIdentifier("loop_region");
    Identifier IComponentBase<Part>.Id => Id;

    public static readonly Codec<IPartComponent> Codec = Codec<IPartComponent>.Create(
        Codecs.INT.Seal<IPartComponent>(nameof(LoopStartCycle), component => (component as LoopRegionComponent)!.LoopStartCycle), // cmon c# you can infer the type you can do it
        loopFromIndex => new LoopRegionComponent { LoopStartCycle = loopFromIndex }
    );

    public int LoopStartCycle;

    public void OnBind(Part self) {
        if (!self.GetType().isProgrammable)
            throw new InvalidOperationException($"{nameof(LoopRegionComponent)} can only be added to a programmable part!");
    }
    public void OnUnbind(Part self) { }

    public void OnClone(ref Part cloned, Solution solution)
        => cloned.AddComponent(new LoopRegionComponent { LoopStartCycle = LoopStartCycle });
    public void OnReset() { }

    public static void Register()
        => Part.RegisterComponent(Id, Codec);
}
