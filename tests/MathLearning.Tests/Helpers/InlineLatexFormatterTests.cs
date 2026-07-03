using MathLearning.Application.Helpers;

namespace MathLearning.Tests.Helpers;

public sealed class InlineLatexFormatterTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeMixedInlineMath_NullEmptyAndWhitespaceAreReturnedUnchanged(string? input)
    {
        Assert.Equal(input, InlineLatexFormatter.NormalizeMixedInlineMath(input));
    }

    [Fact]
    public void NormalizeMixedInlineMath_PlainNaturalLanguageRemainsUnchanged()
    {
        const string input = "Objasni sledeći korak bez matematičkog izraza.";

        Assert.Equal(input, InlineLatexFormatter.NormalizeMixedInlineMath(input));
    }

    [Fact]
    public void NormalizeMixedInlineMath_ExistingInlineMathIsPreservedExactly()
    {
        const string input = "Za $x+1=3$ dobijamo $x=2$.";

        var result = InlineLatexFormatter.NormalizeMixedInlineMath(input);

        Assert.Equal(input, result);
    }

    [Fact]
    public void NormalizeMixedInlineMath_ExistingInlineMathAtStringBoundariesIsPreserved()
    {
        const string input = "$f(x)=2x+1$";

        Assert.Equal(input, InlineLatexFormatter.NormalizeMixedInlineMath(input));
    }

    [Theory]
    [InlineData("f(x)=2x+3", "$f(x)=2x+3$")]
    [InlineData("g(2)=4.", "$g(2)=4$.")]
    [InlineData("Izračunaj f(x)=2x+3.", "Izračunaj $f(x)=2x+3$.")]
    [InlineData("f(x)=x+1 i g(x)=x^2.", "$f(x)=x+1$ i $g(x)=x^2$.")]
    [InlineData("f(x)=x+1 and g(x)=x^2.", "$f(x)=x+1$ and $g(x)=x^2$.")]
    public void NormalizeMixedInlineMath_WrapsFunctionEqualities(string input, string expected)
    {
        Assert.Equal(expected, InlineLatexFormatter.NormalizeMixedInlineMath(input));
    }

    [Theory]
    [InlineData("(f \\circ g)(x)", "$(f \\circ g)(x)$")]
    [InlineData("(f ∘ g)(2)", "$(f ∘ g)(2)$")]
    [InlineData("Odredi (f \\circ g)(x).", "Odredi $(f \\circ g)(x)$.")]
    public void NormalizeMixedInlineMath_WrapsCompositionCalls(string input, string expected)
    {
        Assert.Equal(expected, InlineLatexFormatter.NormalizeMixedInlineMath(input));
    }

    [Fact]
    public void NormalizeMixedInlineMath_MixedExistingAndPlainMathPreservesExistingAndWrapsOnlyPlainSegment()
    {
        const string input = "Za $x=1$ i f(x)=2x+3 dobijamo $f(1)=5$.";
        const string expected = "Za $x=1$ i $f(x)=2x+3 dobijamo $f(1)=5$.";

        var result = InlineLatexFormatter.NormalizeMixedInlineMath(input);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void NormalizeMixedInlineMath_IsIdempotent()
    {
        const string input = "Za $x=1$ i f(x)=2x+3, odredi (f \\circ g)(x).";

        var once = InlineLatexFormatter.NormalizeMixedInlineMath(input);
        var twice = InlineLatexFormatter.NormalizeMixedInlineMath(once);

        Assert.Equal(once, twice);
    }

    [Fact]
    public void NormalizeMixedInlineMath_DoesNotRemoveMultipleExistingExpressions()
    {
        const string input = "$a=1$, zatim $b=2$, pa je $a+b=3$.";

        var result = InlineLatexFormatter.NormalizeMixedInlineMath(input);

        Assert.Equal(input, result);
        Assert.Equal(6, result!.Count(character => character == '$'));
    }
}
