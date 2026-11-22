using LyraStudio.Services;
using Xunit;

namespace LyraStudio.Tests;

public sealed class PhpStaticValueParserTests
{
    [Fact]
    public void ParsesQuotedMethodReturnAndTranslationWrapper()
    {
        var source = """
        public function get_title() {
            return __( 'Sample Card', 'sample-domain' );
        }
        """;

        Assert.Equal("Sample Card", PhpStaticValueParser.ParseReturnExpression(source));
    }

    [Fact]
    public void IgnoresReturnTextInsideDocBlocksAndComments()
    {
        var source = """
        /**
         * @return array
         * Example: return array('not-a-real-recipe');
         */
        // return array('also-not-real');
        return array('slug' => 'real-recipe');
        """;

        var result = Assert.IsType<Dictionary<string, object?>>(
            PhpStaticValueParser.ParseReturnExpression(source));
        Assert.Equal("real-recipe", result["slug"]);
    }

    [Fact]
    public void ParsesNestedLyraRecipeContainerAndWidgetCalls()
    {
        var source = """
        return array(
            'slug' => 'example-landing',
            'title' => 'Example landing',
            'elements' => array(
                $b::container(
                    array(
                        'flex_direction' => 'column',
                        'flex_gap' => $b::gap(48),
                        'padding' => $b::dims(64, 16),
                    ),
                    array(
                        $b::widget('lyra-section-heading', array('title' => 'Build pages')),
                    )
                )
            )
        );
        """;

        var result = Assert.IsType<Dictionary<string, object?>>(
            PhpStaticValueParser.ParseReturnExpression(source));
        Assert.Equal("example-landing", result["slug"]);

        var elements = Assert.IsType<List<object?>>(result["elements"]);
        var container = Assert.IsType<Dictionary<string, object?>>(elements[0]);
        Assert.Equal("container", container["elType"]);
        var settings = Assert.IsType<Dictionary<string, object?>>(container["settings"]);
        Assert.Equal("column", settings["flex_direction"]);

        var gap = Assert.IsType<Dictionary<string, object?>>(settings["flex_gap"]);
        Assert.Equal(48L, gap["size"]);
        Assert.Equal("px", gap["unit"]);

        var padding = Assert.IsType<Dictionary<string, object?>>(settings["padding"]);
        Assert.Equal("64", padding["top"]);
        Assert.Equal("16", padding["right"]);

        var children = Assert.IsType<List<object?>>(container["elements"]);
        var widget = Assert.IsType<Dictionary<string, object?>>(children[0]);
        Assert.Equal("lyra-section-heading", widget["widgetType"]);
    }

    [Fact]
    public void ParsesRepeaterFieldCallExpressionWithoutEvaluatingPhp()
    {
        var config = """
        array(
            'label' => 'Items',
            'type' => \Elementor\Controls_Manager::REPEATER,
            'fields' => $repeater->get_controls(),
            'default' => array(
                array('title' => 'One'),
                array('title' => 'Two'),
            ),
        )
        """;

        var result = Assert.IsType<Dictionary<string, object?>>(
            PhpStaticValueParser.ParseExpression(config));
        Assert.Equal("REPEATER", result["type"]);
        Assert.Equal("$repeater->get_controls()", result["fields"]);

        var defaults = Assert.IsType<List<object?>>(result["default"]);
        Assert.Equal(2, defaults.Count);
    }
}
