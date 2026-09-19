using System.Text.Json.Nodes;

/// <summary>
/// Turns a record's own structured fields into the modifiers a tracker can apply.
/// <para>Nothing here reads prose. The pull does not fetch rule text at all, by licence, so the
/// snapshot carries none: a spell's record states its tradition, its level and its traits, and
/// not one word of what it does. That rules out the obvious approach of reading "+1 status bonus
/// to attack rolls" out of a description, and it is why the hand-written registry in
/// Pf2e.Domain.Buffs exists at all.</para>
/// <para>What is left is the handful of places where Archives of Nethys publishes the number as a
/// field. An item bonus to a skill is one: 829 records state a value and the skill it applies to.
/// That is the whole of it today, and the shape below is the seed's contract with
/// RuleModifiers.Of on the read side.</para>
/// </summary>
static class Modifiers
{
    /// <summary>Null rather than an empty array, so a record that states nothing carries no key
    /// and the read path's cheap "is the word even in the text" check keeps working.</summary>
    public static JsonArray? Of(JsonObject record, string category)
    {
        if (category is not "item-bonus")
        {
            return null;
        }

        if (record["item_bonus_value"] is not JsonValue raw || !raw.TryGetValue<int>(out var value) || value == 0)
        {
            return null;
        }

        var skills = Skills(record["skill"]);
        if (skills.Count == 0)
        {
            // 158 of the 987 name no skill. Most say Perception in a note, qualified by the
            // action being taken ("Perception rolls for initiative"), which is a predicate this
            // engine cannot express. Emitting them against all Perception would be a wrong
            // number rather than a missing one.
            return null;
        }

        var applies = new JsonArray();
        foreach (var skill in skills)
        {
            applies.Add(new JsonObject
            {
                ["kind"] = "Exactly",
                ["stat"] = "Skill",
                ["skillName"] = skill,
            });
        }

        return
        [
            new JsonObject
            {
                ["type"] = "Item",
                ["value"] = value,
                ["applies"] = applies,
            },
        ];
    }

    static List<string> Skills(JsonNode? node) => node switch
    {
        JsonArray array =>
        [
            .. array
                .OfType<JsonValue>()
                .Select(value => value.TryGetValue<string>(out var text) ? text : null)
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .Select(text => text!),
        ],
        JsonValue value when value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text) => [text],
        _ => [],
    };
}
