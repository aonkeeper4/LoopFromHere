namespace LoopFromHere.Helpers;

internal static class MathHelper {
    public static int Gcd(int a, int b)
        => b == 0 ? a : Gcd(b, a % b); // euclidean algorithm babyyy

    public static int Lcm(int a, int b)
        => a * b / Gcd(a, b); // this is a neat identity
}
