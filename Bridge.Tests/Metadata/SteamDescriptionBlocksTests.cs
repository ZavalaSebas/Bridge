using Bridge.Core.Entities;
using Bridge.Metadata;

namespace Bridge.Tests.Metadata;

public class SteamDescriptionBlocksTests
{
    private static List<DescriptionBlock> Parse(string html)
    {
        var method = typeof(SteamMetadataProvider).GetMethod(
            "ParseDescriptionBlocks",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);
        return (List<DescriptionBlock>)method!.Invoke(null, [html])!;
    }

    [Fact]
    public void Paragraphs_are_kept_in_order()
    {
        var blocks = Parse("<p>First paragraph.</p><p>Second paragraph.</p>");

        Assert.Equal(2, blocks.Count);
        Assert.All(blocks, b => Assert.Equal(DescriptionBlockKind.Paragraph, b.Kind));
        Assert.Equal("First paragraph.", blocks[0].Text);
        Assert.Equal("Second paragraph.", blocks[1].Text);
    }

    [Fact]
    public void Headings_map_to_heading_kinds()
    {
        var blocks = Parse("<h2>System Requirements</h2><h3>Minimum</h3><p>OS: Windows 10</p>");

        Assert.Equal(DescriptionBlockKind.Heading, blocks[0].Kind);
        Assert.Equal("System Requirements", blocks[0].Text);
        Assert.Equal(DescriptionBlockKind.Subheading, blocks[1].Kind);
        Assert.Equal("Minimum", blocks[1].Text);
        Assert.Equal(DescriptionBlockKind.Paragraph, blocks[2].Kind);
    }

    [Fact]
    public void List_items_become_bulleted_blocks_in_order()
    {
        var blocks = Parse("<ul><li>Single-player</li><li>Steam Achievements</li></ul>");

        Assert.Equal(2, blocks.Count);
        Assert.All(blocks, b => Assert.Equal(DescriptionBlockKind.List, b.Kind));
        Assert.Equal("Single-player", blocks[0].Text);
        Assert.Equal("Steam Achievements", blocks[1].Text);
    }

    [Fact]
    public void Images_interleave_with_typed_text_blocks()
    {
        var html = """
            <h2>About</h2>
            <p>Intro text.</p>
            <img src="https://cdn.akamai.steamstatic.com/shot1.jpg" width="600" height="338" />
            <ul><li>Feature A</li></ul>
            """;

        var blocks = Parse(html);

        Assert.Equal(DescriptionBlockKind.Heading, blocks[0].Kind);
        Assert.Equal(DescriptionBlockKind.Paragraph, blocks[1].Kind);
        Assert.True(blocks[2].IsImage);
        Assert.Equal("https://cdn.akamai.steamstatic.com/shot1.jpg", blocks[2].Url);
        Assert.Equal(DescriptionBlockKind.List, blocks[3].Kind);
    }

    [Fact]
    public void Inline_markup_is_stripped_but_text_kept()
    {
        var blocks = Parse("<p>Visit <b>our site</b> at <a href=\"https://x\">link</a> now.</p>");

        var p = Assert.Single(blocks);
        Assert.Equal("Visit our site at link now.", p.Text);
    }
}
