using System.Windows;
using System.Windows.Controls;
using Bridge.Core.Entities;

namespace Bridge.Controls;

/// <summary>
/// Picks the details-view template for one DescriptionBlock: text runs render as
/// wrapped paragraphs, headings as large bold titles, list items as bulleted
/// entries, image blocks as rounded screenshots. Registered in MainWindow
/// resources so the DataTemplates can be shared with both the main details panel
/// and the compact Info panel.
/// </summary>
public class DescriptionBlockTemplateSelector : DataTemplateSelector
{
    public DataTemplate? TextTemplate { get; set; }
    public DataTemplate? HeadingTemplate { get; set; }
    public DataTemplate? SubheadingTemplate { get; set; }
    public DataTemplate? ListTemplate { get; set; }
    public DataTemplate? ImageTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object? item, DependencyObject container)
    {
        if (item is not DescriptionBlock block)
            return TextTemplate;

        if (block.IsImage)
            return ImageTemplate;

        return block.Kind switch
        {
            DescriptionBlockKind.Heading => HeadingTemplate,
            DescriptionBlockKind.Subheading => SubheadingTemplate,
            DescriptionBlockKind.List => ListTemplate,
            _ => TextTemplate
        };
    }
}
