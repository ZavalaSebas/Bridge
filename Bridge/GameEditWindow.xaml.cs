using System.Windows;
using System.Windows.Controls;
using Bridge.Core.Entities;
using Bridge.Metadata;
using Bridge.Resources;
using Bridge.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using Wpf.Ui.Controls;
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
            BackgroundArt.SourceUrl = HeroBackground.IsCustom(backgroundImage) ? backgroundImage : null;
            Title = viewModel.IsNewGame ? Strings.NewGame : Strings.EditGame;
            WindowTitleText.Text = Title;
            WindowIcon.Symbol = viewModel.IsNewGame
                ? Wpf.Ui.Controls.SymbolRegular.Add24
                : Wpf.Ui.Controls.SymbolRegular.Edit24;
        }

        public void SelectMediaTab() => MediaTab.IsSelected = true;

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!_viewModel.Save())
            {
                MessageDialogWindow.ShowWarning(Strings.NameRequired, Strings.Save, this);
                return;
            }

            DialogResult = true;
        }

        // Create genre/developer/publisher/platform entries from the edit dialog.
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
                    MessageDialogWindow.ShowWarning(Strings.Format(nameof(Strings.CouldNotCreateReferenceFormat), kind.ToLowerInvariant()), Strings.AddReferenceTitle, this);
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
            var ok = new System.Windows.Controls.Button { Content = Strings.OK, Width = 70, IsDefault = true, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
            var cancel = new System.Windows.Controls.Button { Content = Strings.Cancel, Width = 70, IsCancel = true, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 8, 0) };
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
        // Image search for the bound artwork field (DuckDuckGo, no API key).
        private void SearchWeb_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { Tag: string field })
            {
                var searchService = App.Services.GetRequiredService<Services.WebImageSearchService>();
                var query = Services.WebImageSearchService.BuildMediaSearchQuery(_viewModel.Name, field);
                var window = new ImageSearchWindow(searchService, query, field) { Owner = this };
                if (window.ShowDialog() == true && window.SelectedImageUrl is { } url)
                    ApplyMediaUrl(field, url);
            }
        }

        // Media tab: pick a local image file and put its path into the bound field.
        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { Tag: string field })
            {
                var dialog = new OpenFileDialog
                {
                    Title = Strings.SelectImageTitle,
                    Filter = "Images (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp"
                };

                if (dialog.ShowDialog(this) == true)
                    ApplyMediaUrl(field, dialog.FileName);
            }
        }

        private void SteamGridDb_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { Tag: string field })
                return;

            if (!_viewModel.IsSteamGridDbConfigured)
            {
                OpenSteamGridDbSettings();
                return;
            }

            var client = App.Services.GetRequiredService<SteamGridDbClient>();
            var query = string.IsNullOrWhiteSpace(_viewModel.Name) ? field : _viewModel.Name;
            var window = new SteamGridDbPickerWindow(client, query, field) { Owner = this };
            if (window.ShowDialog() == true && window.SelectedImageUrl is { } url)
                ApplyMediaUrl(field, url);
        }

        private void SetupSteamGridDb_Click(object sender, RoutedEventArgs e) => OpenSteamGridDbSettings();

        private void OpenSteamGridDbSettings()
        {
            var viewModel = App.Services.GetRequiredService<SteamGridDbSettingsViewModel>();
            if (new SteamGridDbSettingsWindow(viewModel) { Owner = this }.ShowDialog() == true)
                _viewModel.NotifySteamGridDbConfigurationChanged();
        }

        private void ClearMedia_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { Tag: string field })
                return;

            switch (field)
            {
                case "Icon":
                    _viewModel.ClearIcon();
                    break;
                case "CoverImage":
                    _viewModel.ClearCover();
                    break;
                case "BackgroundImage":
                    _viewModel.HeroBackgroundKind = HeroBackground.Kind.Default;
                    break;
            }
        }

        private void HeroBackgroundKind_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not RadioButton { Tag: string tag })
                return;

            if (Enum.TryParse<HeroBackground.Kind>(tag, ignoreCase: true, out var kind))
                _viewModel.HeroBackgroundKind = kind;
        }

        private void ApplyMediaUrl(string field, string url)
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
                    _viewModel.HeroBackgroundKind = HeroBackground.Kind.Custom;
                    _viewModel.BackgroundImage = url;
                    break;
            }
        }
    }
}
