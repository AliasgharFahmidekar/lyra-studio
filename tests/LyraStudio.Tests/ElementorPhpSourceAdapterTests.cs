using System.IO;
using System.IO.Compression;
using System.Text;
using LyraStudio.Services;
using Xunit;

namespace LyraStudio.Tests;

public sealed class ElementorPhpSourceAdapterTests
{
    private readonly ProviderPackageImporter _importer = new();

    [Fact]
    public async Task DiscoversVariableWidgetCountControlsAndRecipeFromNestedPluginZip()
    {
        var archivePath = await CreatePluginZipAsync();

        try
        {
            var result = await _importer.InspectAsync(archivePath);

            Assert.Equal("elementor.php-source.v1", result.AdapterId);
            Assert.NotNull(result.Manifest);
            Assert.Equal("Sample Plugin", result.Manifest!.ProviderName);
            Assert.Equal("2.4.0", result.Manifest.ProviderVersion);
            Assert.Equal(2, result.Manifest.Widgets.Count);
            Assert.Single(result.Manifest.Recipes);

            var card = Assert.Single(result.Manifest.Widgets, widget => widget.Id == "sample-card");
            Assert.Equal("Sample Card", card.Name);
            Assert.Equal("text", Assert.Single(card.Controls!, control => control.Name == "title").Kind);

            var repeater = Assert.Single(card.Controls!, control => control.Name == "items");
            Assert.Equal("repeater", repeater.Kind);
            Assert.Equal(2, repeater.Children!.Count);
            Assert.Equal("title", repeater.Children[0].Name);
            Assert.Equal("textarea", repeater.Children[1].Kind);

            var recipe = Assert.Single(result.Manifest.Recipes);
            Assert.Equal("demo-layout", recipe.Id);
            Assert.Contains("Recipe headline", recipe.PreviewHtml);
            Assert.Contains("sample-card", recipe.PreviewHtml);

            Assert.NotNull(result.PreviewAssets);
            Assert.Contains("data:font/woff2;base64", result.PreviewAssets!.StylesheetCss);
            Assert.Contains("display:none", result.PreviewAssets.IconSpriteMarkup);
            Assert.Contains("assets/demo/leo-logo-reference.jpg", result.PreviewAssets.DataAssets.Keys);
        }
        finally
        {
            File.Delete(archivePath);
        }
    }

    [Fact]
    public async Task UsesLocalLyraRendererForKnownLyraWidgetIds()
    {
        var archivePath = await CreatePluginZipAsync(includeLyraWidget: true);

        try
        {
            var result = await _importer.InspectAsync(archivePath);

            var widget = Assert.Single(result.Manifest!.Widgets, item => item.Id == "lyra-section-heading");
            Assert.Equal("lyra-core.elementor/lyra-section-heading", widget.PreviewRendererId);
            Assert.Contains(result.Manifest.Widgets, item => item.Id == "sample-card");

            var previewBuilder = new LocalWidgetPreviewBuilder();
            var html = previewBuilder.BuildDocument(
                widget,
                new Dictionary<string, object?>
                {
                    ["title"] = "Local preview title",
                    ["eyebrow"] = "Adapter test",
                    ["description"] = "Rendered with package CSS and local fonts",
                    ["align"] = "center"
                },
                1280,
                result.PreviewAssets);

            Assert.Contains("Local preview title", html);
            Assert.Contains("font-family:\"Peyda\"", html);
            Assert.Contains("data:font/woff2;base64", html);
            Assert.Contains("lyra-i-star", html);
        }
        finally
        {
            File.Delete(archivePath);
        }
    }

    private static async Task<string> CreatePluginZipAsync(bool includeLyraWidget = false)
    {
        var path = Path.Combine(Path.GetTempPath(), $"lyra-elementor-adapter-{Guid.NewGuid():N}.zip");
        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);

        using (var archive = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: true))
        {
            await Add("sample-plugin/lyra-core.php", """
                <?php
                /**
                 * Plugin Name: Sample Plugin
                 * Version: 2.4.0
                 */
                """);

            await Add("sample-plugin/includes/class-lyra-elementor.php", """
                <?php
                class Sample_Elementor {
                    private static $manifest = array(
                        'Sample_Card' => 'general/class-sample-card.php',
                    );
                }
                """);

            await Add("sample-plugin/widgets/general/class-sample-card.php", """
                <?php
                class Sample_Card extends \Elementor\Widget_Base {
                    public function get_name() { return 'sample-card'; }
                    public function get_title() { return __( 'Sample Card', 'sample-domain' ); }

                    protected function register_controls() {
                        $this->add_control(
                            'title',
                            array(
                                'label' => __( 'Title', 'sample-domain' ),
                                'type' => \Elementor\Controls_Manager::TEXT,
                                'default' => __( 'Hello', 'sample-domain' ),
                            )
                        );

                        $repeater = new \Elementor\Repeater();
                        $repeater->add_control(
                            'title',
                            array(
                                'label' => 'Item title',
                                'type' => \Elementor\Controls_Manager::TEXT,
                            )
                        );
                        $repeater->add_control(
                            'description',
                            array(
                                'label' => 'Description',
                                'type' => \Elementor\Controls_Manager::TEXTAREA,
                            )
                        );
                        $this->add_control(
                            'items',
                            array(
                                'label' => 'Items',
                                'type' => \Elementor\Controls_Manager::REPEATER,
                                'fields' => $repeater->get_controls(),
                                'default' => array(
                                    array('title' => 'One'),
                                    array('title' => 'Two'),
                                ),
                            )
                        );
                    }
                }
                """);

            await Add("sample-plugin/widgets/layout/class-second-widget.php", """
                <?php
                class Second_Widget extends \Elementor\Widget_Base {
                    public function get_name() { return 'second-widget'; }
                    public function get_title() { return 'Second Widget'; }
                    protected function register_controls() {
                        $this->add_responsive_control(
                            'columns',
                            array(
                                'label' => 'Columns',
                                'type' => \Elementor\Controls_Manager::SELECT,
                                'options' => array('1' => 'One', '2' => 'Two'),
                                'default' => '2',
                            )
                        );
                    }
                }
                """);

            if (includeLyraWidget)
            {
                await Add("sample-plugin/widgets/general/class-lyra-section-heading.php", """
                    <?php
                    class Lyra_Section_Heading extends \Elementor\Widget_Base {
                        public function get_name() { return 'lyra-section-heading'; }
                        public function get_title() { return 'Section Heading'; }
                        protected function register_controls() {
                            $this->add_control('title', array(
                                'label' => 'Title',
                                'type' => \Elementor\Controls_Manager::TEXT,
                                'default' => 'Hello',
                            ));
                        }
                    }
                    """);
            }

            await Add("sample-plugin/includes/recipes/demo-layout.php", """
                <?php
                /**
                 * @return array
                 */
                if ( ! defined( 'ABSPATH' ) ) {
                    exit;
                }
                $b = 'Lyra_Page_Builder';
                return array(
                    'slug' => 'demo-layout',
                    'title' => 'Demo Layout',
                    'elements' => array(
                        $b::container(
                            array(
                                'flex_direction' => 'column',
                                'flex_gap' => $b::gap(24),
                                'padding' => $b::dims(16, 12),
                            ),
                            array(
                                $b::widget('sample-card', array(
                                    'title' => 'Recipe headline',
                                )),
                            )
                        ),
                    ),
                );
                """);

            await Add("sample-plugin/assets/css/tokens.css", """
                :root { --lyra-background: #fdfbf7; --lyra-text: #24301d; }
                """);
            await Add("sample-plugin/assets/css/components.css", """
                .lyra-section-heading { padding: 24px; }
                """);
            await Add("sample-plugin/assets/css/fonts.css", """
                @font-face { font-family: Sample; src: url('../fonts/sample.woff2') format('woff2'); }
                """);
            await Add("sample-plugin/assets/fonts/sample.woff2", "font-data");
            await Add("sample-plugin/assets/icons/sprite.svg", """
                <svg xmlns="http://www.w3.org/2000/svg"><symbol id="lyra-i-star" viewBox="0 0 24 24"><path d="M12 1L15 9H23L17 14L19 23L12 18L5 23L7 14L1 9H9Z"/></symbol></svg>
                """);
            await Add("sample-plugin/assets/demo/leo-logo-reference.jpg", "fake-image");

            async Task Add(string name, string content)
            {
                var entry = archive.CreateEntry(name);
                await using var stream = entry.Open();
                var bytes = Encoding.UTF8.GetBytes(content);
                await stream.WriteAsync(bytes);
            }
        }

        return path;
    }
}
