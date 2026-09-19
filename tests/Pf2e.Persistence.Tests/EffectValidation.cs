using Pf2e.Application.Features.Tracker;
using Pf2e.Contracts.Tracker;
using Pf2e.Domain;

namespace Pf2e.Persistence.Tests;

/// <summary>
/// The gate between the picker and the table. It resolved keys through the conditions alone, so
/// every buff a player tapped came back a 400 while the picker happily went on offering it.
/// </summary>
public class EffectValidation
{
    public static TheoryData<string, int> EveryBuff
    {
        get
        {
            var data = new TheoryData<string, int>();
            foreach (var buff in Buffs.All)
            {
                data.Add(buff.Key, buff.HasValue ? 2 : 0);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(EveryBuff))]
    public void TheApiAcceptsEveryBuffTheRegistryOffers(string key, int value)
    {
        var spec = new EffectSpec(Effects.Find(key)!.Name, "Seeded", key, value, null, []);

        var result = new EffectSpecValidator().Validate(spec);

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Fact]
    public void AValuedBuffStillNeedsAValueAndAFlatOneStillRefusesOne()
    {
        var validator = new EffectSpecValidator();

        Assert.False(validator.Validate(new EffectSpec("Raise a Shield", "Seeded", "raise-a-shield", 0, null, [])).IsValid);
        Assert.False(validator.Validate(new EffectSpec("Bless", "Seeded", "bless", 1, null, [])).IsValid);
        Assert.False(validator.Validate(new EffectSpec("Nothing", "Seeded", "no-such-buff", 0, null, [])).IsValid);
    }
}
