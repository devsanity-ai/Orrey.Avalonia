namespace Orrery;

/// <summary>
/// The seeded linear congruential generator the simulation uses for every random decision.
/// </summary>
/// <remarks>
/// Ported from d3-force (ISC, © 2010-2021 Mike Bostock); see THIRD-PARTY-NOTICES.txt.
/// <para>
/// The generator is seeded and reproducible on purpose. Layouts must be deterministic: the same
/// graph laid out twice produces the same picture, which is what makes a persisted set of node
/// positions meaningful and makes physics regressions testable.
/// </para>
/// </remarks>
public static class Lcg
{
    private const long Multiplier = 1664525;
    private const long Increment = 1013904223;
    private const long Modulus = 4294967296; // 2^32

    /// <summary>Creates a generator producing values in [0, 1) from a fixed seed.</summary>
    /// <param name="seed">Starting state. The default matches d3-force.</param>
    public static Func<double> Create(long seed = 1)
    {
        var state = seed;
        return () =>
        {
            state = (Multiplier * state + Increment) % Modulus;
            return (double)state / Modulus;
        };
    }
}
