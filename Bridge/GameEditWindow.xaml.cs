using System.Windows;
using System.Windows.Controls;
using Bridge.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using Wpf.Ui.Controls;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;

namespace Bridge
{
    public partial class GameEditWindow : FluentWindow
    {
        private readonly GameEditViewModel _viewModel;

        public GameEditWindow(GameEditViewModel viewModel, string? backgroundImage = null)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = viewModel;
            BackgroundArt.SourceUrl = backgroundImage;
            Title = viewModel.IsNewGame ? "New Game" : "Edit Game";
            WindowTitleText.Text = Title;
            WindowIcon.Symbol = viewModel.IsNewGame
                ? Wpf.Ui.Controls.SymbolRegular.Add24
                : Wpf.Ui.Controls.SymbolRegular.Edit24;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!_viewModel.Save())
            {
                MessageBox.Show(this, "Name is required.", "Save", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
        }

        // Details tab: create a new reference entity (genre/developer/publisher/
        // platform) on the fly, like Playnite's "+" button next to each list.
        private void AddReference_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { Tag: string kind })
            {
                var name = PromptForName(this, kind);
                if (string.IsNullOrWhiteSpace(name))
                {
                    return;
                }

                var created = kind switch
                {
                    "Genre" => _viewModel.CreateNewGenre(name),
                    "Developer" => _viewModel.CreateNewDeveloper(name),
                    "Publisher" => _viewModel.CreateNewPublisher(name),
                    "Platform" => _viewModel.CreateNewPlatform(name),
                    _ => false
                };

                if (!created)
                {
                    MessageBox.Show(this, $"Could not create {kind.ToLowerInvariant()}.", "Add reference", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private static string PromptForName(Window owner, string kind)
        {
            var input = new System.Windows.Controls.TextBox { Margin = new Thickness(0, 6, 0, 0) };
            var dialog = new Window
            {
                Title = $"Add {kind}",
                Owner = owner,
                Width = 320,
                SizeToContent = SizeToContent.Height,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ShowInTaskbar = false,
                Background = System.Windows.Application.Current.TryFindResource("ApplicationBackgroundBrush") as System.Windows.Media.Brush
                    ?? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x15, 0x1A, 0x28)),
                Foreground = System.Windows.Application.Current.TryFindResource("TextFillColorPrimaryBrush") as System.Windows.Media.Brush
                    ?? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE0, 0xE0, 0xE0))
            };
            var panel = new StackPanel { Margin = new Thickness(14) };
            panel.Children.Add(new System.Windows.Controls.TextBlock { Text = $"Name for the new {kind.ToLowerInvariant()}:" });
            panel.Children.Add(input);
            var ok = new System.Windows.Controls.Button { Content = "OK", Width = 70, IsDefault = true, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
            var cancel = new System.Windows.Controls.Button { Content = "Cancel", Width = 70, IsCancel = true, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 8, 0) };
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            buttons.Children.Add(cancel);
            buttons.Children.Add(ok);
            panel.Children.Add(buttons);
            dialog.Content = panel;
            dialog.Loaded += (_, _) => input.Focus();

            string result = string.Empty;
            ok.Click += (_, _) => { result = input.Text.Trim(); dialog.DialogResult = true; };
            if (dialog.ShowDialog() == true)
            {
                result = input.Text.Trim();
            }

            return result;
        }

        // Media tab: search for an image on the web and put the chosen URL into
        // the bound field (Playnite's Google-image-picker, keyless via DDG).
        private void SearchWeb_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { Tag: string field })
            {
                var searchService = App.Services.GetRequiredService<Services.WebImageSearchService>();
                var window = new ImageSearchWindow(searchService, _viewModel.Name) { Owner = this };
                if (window.ShowDialog() == true && window.SelectedImageUrl is { } url)
                {
                    switch (field)
                    {
                        case "Icon":
                            _viewModel.Icon = url;
                            break;
                        case "CoverImage":
                            _viewModel.CoverImage = url;
                            break;
                        case "BackgroundImage":
                            _viewModel.BackgroundImage = url;
                            break;
                    }
                }
            }
        }

        // Media tab: pick a local image file and put its path into the bound field.
        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { Tag: string field })
            {
                var dialog = new OpenFileDialog
                {
                    Title = "Select image",
                    Filter = "Images (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp"
                };

                if (dialog.ShowDialog(this) == true)
                {
                    switch (field)
                    {
                        case "Icon":
                            _viewModel.Icon = dialog.FileName;
                            break;
                        case "CoverImage":
                            _viewModel.CoverImage = dialog.FileName;
                            break;
                        case "BackgroundImage":
                            _viewModel.BackgroundImage = dialog.FileName;
                            break;
                    }
                }
            }
        }
    }
}
