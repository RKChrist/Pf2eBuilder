using Pf2e.Application.Features.Rules;

namespace Pf2e.Unit.Tests;

public class RuleProseTests
{
    const string Page = """
        <title level="1" right="Cantrip 1" pfs="Standard">
        [Courageous Anthem](/Spells.aspx?ID=1763)
        <actions string="Single Action" />
        </title>

        <traits>
        <trait label="Bard" url="/Traits.aspx?ID=19" />
        </traits>

        <column gap="tiny">

        **Source** [Player Core](/Sources.aspx?ID=1) pg. 386

        </column>

        ---

        You inspire yourself and your allies. You gain a +1 [status bonus](/Rules.aspx?ID=1) to attack rolls.

        ---

        **Heightened (+2)** The bonus goes up.
        """;

    [Fact]
    public void TheHeaderTheSeedAlreadyHoldsIsLeftBehind()
    {
        var prose = RuleProse.Of(Page)!;

        Assert.StartsWith("You inspire yourself", prose);
        Assert.DoesNotContain("Source", prose);
        Assert.DoesNotContain("Bard", prose);
    }

    [Fact]
    public void ALinkKeepsItsWordsAndLosesTheirAddress()
    {
        var prose = RuleProse.Of(Page)!;

        Assert.Contains("a +1 status bonus to attack rolls", prose);
        Assert.DoesNotContain("aspx", prose);
    }

    [Fact]
    public void ARuleInsideTheBodySurvives()
    {
        Assert.Contains("---\n\n**Heightened (+2)**", RuleProse.Of(Page)!);
    }

    [Fact]
    public void NoTagOfTheirsReachesThePanel()
    {
        Assert.DoesNotContain("<", RuleProse.Of("---\n<row gap=\"medium\">Text <br /> here</row>")!);
    }

    [Fact]
    public void AListOfTheirsBecomesAListOfOurs()
    {
        var prose = RuleProse.Of("---\n<ul><li>**Cantrip** [_ignition_](/Spells.aspx?ID=1)</li><li>**1st** [_breathe fire_](/Spells.aspx?ID=2)</li></ul>")!;

        Assert.Equal("- **Cantrip** _ignition_\n- **1st** _breathe fire_", prose);
    }

    [Fact]
    public void APageThatIsAllHeaderHasNoDescription()
    {
        Assert.Null(RuleProse.Of("<title>Thing</title>\n\n---\n\n"));
    }
}
