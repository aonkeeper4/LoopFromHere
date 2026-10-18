using Quintessential;

namespace TemplateMod;

internal class TemplateMod : QuintessentialMod, IDataMod {
    public static TemplateMod Instance { get; }
    public override string ModId => "template_modid";

    public override void Load() { }

    public override void LoadContent() {

        this.AddAtomType(new AtomType() {
            isGlassy = true,
            shadow = Assets.textures.atoms.elements.fire_shadow,
            symbol = Assets.textures.atoms.elements.quintessence_symbol,
            glassyTextures = new() {
                base1 = Assets.textures.atoms.elements.fire_base,
                base2 = Assets.textures.transparent,
                fog = Assets.textures.atoms.elements.air_fog,
            }
        }, "test_atom");
    }
    public override void LoadCompatContent() { }
    public override void FinaliseContent() { }

    public override void PostLoad() { }
	public override void Unload() { }
}
