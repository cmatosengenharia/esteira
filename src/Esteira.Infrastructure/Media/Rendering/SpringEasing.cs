using System.Globalization;

namespace Esteira.Infrastructure.Media.Rendering;

/// <summary>
/// Spring easing (damped oscillator, mass/stiffness/damping) stretched to a fixed duration and clamped to [0,1],
/// plus the same curve as an ffmpeg expression for the <c>zoompan</c> <c>z</c> or a <c>scale</c> factor.
/// Only the overdamped case (damping² &gt; 4·mass·stiffness, e.g. the default damping 200) is supported: no overshoot.
/// </summary>
public static class SpringEasing
{
    // Physical time at which the remaining displacement drops to this residual is mapped to the end of the duration.
    private const double SettleResidual = 1e-3;

    public static double Evaluate(double t, double durationSeconds, double damping = 200, double stiffness = 100, double mass = 1)
    {
        var (c1, k1, c2, k2, norm) = Coefficients(durationSeconds, damping, stiffness, mass);
        t = Math.Clamp(t, 0, durationSeconds);
        return Math.Clamp((1 - (c1 * Math.Exp(k1 * t) + c2 * Math.Exp(k2 * t))) / norm, 0, 1);
    }

    public static double Interpolate(double t, double durationSeconds, double from, double to) =>
        from + (to - from) * Evaluate(t, durationSeconds);

    /// <summary>Expression for <c>zoompan=z='…'</c>, time taken from the output frame number <c>on</c>.</summary>
    public static string ZoompanExpression(double from, double to, double durationSeconds, int fps) =>
        Expression(from, to, durationSeconds, $"on/{fps.ToString(CultureInfo.InvariantCulture)}");

    /// <summary>Scale factor expression in <c>t</c> (seconds), for <c>scale</c> with <c>eval=frame</c>.</summary>
    public static string ScaleExpression(double from, double to, double durationSeconds) =>
        Expression(from, to, durationSeconds, "t");

    private static string Expression(double from, double to, double durationSeconds, string time)
    {
        var (c1, k1, c2, k2, norm) = Coefficients(durationSeconds, 200, 100, 1);
        time = $"clip({time},0,{N(durationSeconds)})";
        return $"{N(from)}+({N(to - from)})*clip((1-({N(c1)}*exp({N(k1)}*{time})+{N(c2)}*exp({N(k2)}*{time})))/{N(norm)},0,1)";
    }

    private static (double C1, double K1, double C2, double K2, double Norm) Coefficients(
        double durationSeconds, double damping, double stiffness, double mass)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(durationSeconds);
        var discriminant = damping * damping - 4 * mass * stiffness;
        if (discriminant <= 0)
            throw new ArgumentOutOfRangeException(nameof(damping), "Only overdamped springs are supported.");

        // Displacement y(s) = c1·e^(r1·s) + c2·e^(r2·s), with y(0) = 1 and y'(0) = 0.
        var r1 = (-damping + Math.Sqrt(discriminant)) / (2 * mass);
        var r2 = (-damping - Math.Sqrt(discriminant)) / (2 * mass);
        var c1 = r2 / (r2 - r1);
        var c2 = -r1 / (r2 - r1);
        var settle = Math.Log(c1 / SettleResidual) / -r1;
        var norm = 1 - (c1 * Math.Exp(r1 * settle) + c2 * Math.Exp(r2 * settle));
        var stretch = settle / durationSeconds;
        return (c1, r1 * stretch, c2, r2 * stretch, norm);
    }

    private static string N(double value) => value.ToString("R", CultureInfo.InvariantCulture);
}
