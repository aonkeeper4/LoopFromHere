using System;
using Quintessential;
using Quintessential.Components;
using Quintessential.Serialization;

namespace LoopFromHere.Components;

public class LoopFromHereComponent(int loopFromIndex) : IPartComponent {
    public static readonly Identifier Id = LoopFromHere.Instance.GetIdentifier("loop_from_here");
    Identifier IComponentBase<Part>.Id => Id;

    public static readonly Codec<IPartComponent> Codec = Codec<IPartComponent>.Create(
        Codecs.INT.Seal<IPartComponent>(nameof(LoopFromIndex), component => (component as LoopFromHereComponent)!.LoopFromIndex), // cmon c# you can infer the type you can do it
        loopFromIndex => new LoopFromHereComponent(loopFromIndex)
    );

    public readonly int LoopFromIndex = loopFromIndex;

    public void OnBind(Part self) {
        if (!self.GetType().isProgrammable)
            throw new InvalidOperationException($"{nameof(LoopFromHereComponent)} can only be added to a programmable part!");
    }
    public void OnUnbind(Part self) { }

    public void OnClone(ref Part cloned, Solution solution)
        => cloned.AddComponent(new LoopFromHereComponent(LoopFromIndex));
    public void OnReset() { }

    public static Maybe<int> LoopFromIndexForPart(Part part)
        => part.TryGetComponent(Id, out IPartComponent delayedLoopingComponent)
           && delayedLoopingComponent is LoopFromHereComponent { LoopFromIndex: var loopFromIndex }
            ? loopFromIndex
            : MaybeHelper.empty;

    public static void Register()
        => Part.RegisterComponent(Id, Codec);
}
