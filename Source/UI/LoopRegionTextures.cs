namespace LoopFromHere.UI;

internal static class LoopRegionTextures {
    public static Texture LoopRegionLeft, LoopRegionRight;

    public static void LoadContent() {
        // todo: make actual textures
        LoopRegionLeft = AssetLoaderHelper.LoadTexture("textures/loop_from_here/ui/loop_region_left");
        LoopRegionRight = AssetLoaderHelper.LoadTexture("textures/loop_from_here/ui/loop_region_right");
    }
}
