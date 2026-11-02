using System.Globalization;
using Esteira.Infrastructure.Media.Rendering;

namespace Esteira.UnitTests.Media;

public class SpringEasingTests
{
    private const double Duration = 0.5;

    [Fact]
    public void Starts_at_zero_and_settles_at_one_at_the_end_of_the_duration()
    {
        Assert.Equal(0, SpringEasing.Evaluate(0, Duration), 9);
        Assert.InRange(SpringEasing.Evaluate(Duration, Duration), 1 - 1e-3, 1);
    }

    [Fact]
    public void Damping_200_is_monotonic_and_never_overshoots()
    {
        var previous = 0.0;
        for (var i = 0; i <= 300; i++)
        {
            var value = SpringEasing.Evaluate(Duration * i / 300.0, Duration);
            Assert.InRange(value, previous, 1);
            previous = value;
        }
    }

    [Theory]
    [InlineData(-1.0, 0.0)]
    [InlineData(-0.01, 0.0)]
    [InlineData(0.6, 1.0)]
    [InlineData(10.0, 1.0)]
    public void Clamps_both_ends(double t, double expected)
    {
        Assert.Equal(expected, SpringEasing.Evaluate(t, Duration), 9);
    }

    [Fact]
    public void Interpolate_stays_within_from_and_to()
    {
        for (var i = -10; i <= 40; i++)
        {
            var value = SpringEasing.Interpolate(i * 0.02, Duration, 1.0, 1.2);
            Assert.InRange(value, 1.0, 1.2);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(15)]
    [InlineData(30)]
    [InlineData(90)]
    public void Zoompan_expression_matches_interpolate_per_frame(int frame)
    {
        const int fps = 30;
        var expression = SpringEasing.ZoompanExpression(1.0, 1.2, Duration, fps);

        Assert.Contains("clip(", expression);
        Assert.Contains("exp(", expression);
        var value = ExpressionEvaluator.Evaluate(expression, new Dictionary<string, double> { ["on"] = frame });
        Assert.Equal(SpringEasing.Interpolate((double)frame / fps, Duration, 1.0, 1.2), value, 6);
    }

    [Theory]
    [InlineData(-0.2)]
    [InlineData(0.1)]
    [InlineData(0.25)]
    [InlineData(2.0)]
    public void Scale_expression_matches_interpolate_per_time(double t)
    {
        var expression = SpringEasing.ScaleExpression(1.2, 1.0, Duration);

        var value = ExpressionEvaluator.Evaluate(expression, new Dictionary<string, double> { ["t"] = t });
        Assert.Equal(SpringEasing.Interpolate(t, Duration, 1.2, 1.0), value, 6);
    }

    /// <summary>Minimal evaluator for the ffmpeg expression subset we emit: numbers, variables, + - * /, exp, clip.</summary>
    private sealed class ExpressionEvaluator
    {
        private readonly string _s;
        private readonly IReadOnlyDictionary<string, double> _vars;
        private int _i;

        private ExpressionEvaluator(string s, IReadOnlyDictionary<string, double> vars) => (_s, _vars) = (s, vars);

        public static double Evaluate(string s, IReadOnlyDictionary<string, double> vars)
        {
            var e = new ExpressionEvaluator(s, vars);
            var value = e.Sum();
            Assert.Equal(s.Length, e._i);
            return value;
        }

        private double Sum()
        {
            var value = Product();
            while (_i < _s.Length && (_s[_i] == '+' || _s[_i] == '-'))
                value = _s[_i++] == '+' ? value + Product() : value - Product();
            return value;
        }

        private double Product()
        {
            var value = Unary();
            while (_i < _s.Length && (_s[_i] == '*' || _s[_i] == '/'))
                value = _s[_i++] == '*' ? value * Unary() : value / Unary();
            return value;
        }

        private double Unary()
        {
            if (_s[_i] == '-') { _i++; return -Unary(); }
            if (_s[_i] == '(') { _i++; var inner = Sum(); Expect(')'); return inner; }
            var start = _i;
            if (char.IsDigit(_s[_i]) || _s[_i] == '.')
            {
                while (_i < _s.Length && (char.IsDigit(_s[_i]) || _s[_i] == '.' || _s[_i] == 'E' || ((_s[_i] == '-' || _s[_i] == '+') && _s[_i - 1] == 'E'))) _i++;
                return double.Parse(_s[start.._i], CultureInfo.InvariantCulture);
            }
            while (_i < _s.Length && char.IsLetter(_s[_i])) _i++;
            var name = _s[start.._i];
            if (name == "exp") { Expect('('); var x = Sum(); Expect(')'); return Math.Exp(x); }
            if (name == "clip")
            {
                Expect('('); var x = Sum(); Expect(','); var lo = Sum(); Expect(','); var hi = Sum(); Expect(')');
                return Math.Clamp(x, lo, hi);
            }
            return _vars[name];
        }

        private void Expect(char c) => Assert.Equal(c, _s[_i++]);
    }
}
