using System.Windows;
using System.Windows.Controls;
using Bridge.Core.Entities;

namespace Bridge.Controls;

/// <summary>
/// Picks the details-view template for one DescriptionBlock: text runs render as
/// wrapped paragraphs, image blocks as rounded screenshots. Registered in
/// MainWindow resources so the two DataTemplates can be shared with both the
/// main details panel and the compact Info panel.
/// </summary>
public class DescriptionBlockTemplateSelector : DataTemplateSelector
{
    public DataTemplate? TextTemplate { get; set; }
    public DataTemplate? ImageTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object? item, DependencyObject container)
        => item is DescriptionBlock block && block.IsImage ? ImageTemplate : TextTemplate;
}
