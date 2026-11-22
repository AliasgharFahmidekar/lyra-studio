using System.Text.Json;
using LyraStudio.Models;
using LyraStudio.Services;
using Xunit;

namespace LyraStudio.Tests;

public sealed class ConditionEvaluatorTests
{
    [Fact]
    public void SupportsEqualityAndNegation()
    {
        var values = new Dictionary<string, object?> { ["show_button"] = true };

        Assert.True(ConditionEvaluator.Matches(
            [new ControlCondition("show_button", "equals", true)], values));
        Assert.False(ConditionEvaluator.Matches(
            [new ControlCondition("show_button", "not_equals", true)], values));
    }

    [Fact]
    public void SupportsTruthyAndFalsyJsonValues()
    {
        using var document = JsonDocument.Parse("""{"enabled":false,"count":0,"caption":"visible"}""");
        var values = document.RootElement.EnumerateObject()
            .ToDictionary(x => x.Name, x => (object?)x.Value.Clone());

        Assert.False(ConditionEvaluator.Matches(
            [new ControlCondition("enabled", "truthy", null)], values));
        Assert.True(ConditionEvaluator.Matches(
            [new ControlCondition("enabled", "falsy", null)], values));
        Assert.False(ConditionEvaluator.Matches(
            [new ControlCondition("count", "truthy", null)], values));
        Assert.True(ConditionEvaluator.Matches(
            [new ControlCondition("caption", "truthy", null)], values));
    }

    [Fact]
    public void ResolvesSiblingFieldsInsideNestedGroups()
    {
        var values = new Dictionary<string, object?> { ["settings.show"] = "true" };

        Assert.True(ConditionEvaluator.Matches(
            [new ControlCondition("show", "equals", true)], values, "settings"));
    }
}
