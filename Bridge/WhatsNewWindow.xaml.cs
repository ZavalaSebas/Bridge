using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Bridge.Resources;
using Bridge.Services;

namespace Bridge;

public partial class WhatsNewWindow
{
    public WhatsNewWindow(WhatsNewRelease release)
    {
        InitializeComponent();
        App.ApplyWindowIcon(this);
        Populate(release);
    }

    private void Populate(WhatsNewRelease release)
    {
        Title = Strings.Format(nameof(Strings.WhatsNewTitleFormat), release.Version.ToString(3));
        TitleText.Text = Title;
        GotItButton.Content = Strings.WhatsNewGotIt;

        if (!string.IsNullOrWhiteSpace(release.ReleaseDate))
        {
            NotesHost.Children.Add(new TextBlock
            {
                Text = release.ReleaseDate,
                FontSize = 12,
                Foreground = (Brush)FindResource("TextFillColorTertiaryBrush"),
                Margin = new Thickness(0, 0, 0, 16)
            });
        }

        for (var sectionIndex = 0; sectionIndex < release.Sections.Count; sectionIndex++)
        {
            var section = release.Sections[sectionIndex];
            NotesHost.Children.Add(new TextBlock
            {
                Text = LocalizeSectionName(section.Name),
                FontWeight = FontWeights.SemiBold,
                FontSize = 14,
                Foreground = (Brush)FindResource("TextFillColorPrimaryBrush"),
                Margin = new Thickness(0, sectionIndex == 0 ? 0 : 18, 0, 8)
            });

            foreach (var item in section.Items)
            {
                var bullet = new TextBlock
                {
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 13,
                    Foreground = (Brush)FindResource("TextFillColorSecondaryBrush"),
                    Margin = new Thickness(0, 0, 0, 10),
                    LineHeight = 19
                };

                bullet.Inlines.Add(new Run("• "));
                bullet.Inlines.Add(new Run(item.Title) { FontWeight = FontWeights.SemiBold });
                if (!string.IsNullOrWhiteSpace(item.Description))
                {
                    bullet.Inlines.Add(new Run(" — "));
                    bullet.Inlines.Add(new Run(Summarize(item.Description)));
                }

                NotesHost.Children.Add(bullet);
            }
        }
    }

    private static string LocalizeSectionName(string sectionName) =>
        sectionName switch
        {
            "Added" => Strings.WhatsNewSectionAdded,
            "Changed" => Strings.WhatsNewSectionChanged,
            "Fixed" => Strings.WhatsNewSectionFixed,
            _ => sectionName
        };

    private static string Summarize(string description)
    {
        const int maxLength = 180;
        if (description.Length <= maxLength)
            return description;

        var cut = description[..maxLength];
        var lastSpace = cut.LastIndexOf(' ');
        if (lastSpace > 80)
            cut = cut[..lastSpace];

        return cut.TrimEnd('.', ' ') + "…";
    }

    private void GotIt_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
