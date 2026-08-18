using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Bridge.Core.Entities;
using Bridge.Import.Epic;
using Bridge.Import.Steam;
using Bridge.Resources;
using Bridge.Services;
using Bridge.ViewModels;
using SymbolIcon = Wpf.Ui.Controls.SymbolIcon;
using SymbolRegular = Wpf.Ui.Controls.SymbolRegular;

namespace Bridge;

public partial class MainWindow
{
    // Event handlers in Styles/MainWindowResources.xaml cannot be declared
    // inline (MC6024: ResourceDictionary requires x:Class). Wire them here so
    // they still live on MainWindow.
    private void WireMainWindowResourceHandlers()
    {
        if (FindResource("Bridge.GameContextMenu") is ContextMenu contextMenu)
            contextMenu.Opened += GameContextMenu_Opened;

        if (FindResource("Bridge.LinkMenuItemStyle") is Style linkStyle)
            linkStyle.Setters.Add(new EventSetter(System.Windows.Controls.MenuItem.ClickEvent, new RoutedEventHandler(OpenLinkMenuItem_Click)));

        if (FindResource("Bridge.CompletionStatusMenuItemStyle") is Style completionStyle)
            completionStyle.Setters.Add(new EventSetter(System.Windows.Controls.MenuItem.ClickEvent, new RoutedEventHandler(CompletionStatusMenuItem_Click)));
    }

    private void ToggleSortDirection_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.SortDescending = !vm.SortDescending;
    }

    // Opens the sender's ContextMenu on left-click (used by icon
    // buttons in the top panel).
    internal void HandleMenuButtonClick(object sender, RoutedEventArgs e) => MenuButton_Click(sender, e);

    private void MenuButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.ContextMenu is { } menu)
        {
            menu.PlacementTarget = element;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            // The context menu lives in a Popup outside the visual tree, so
            // ElementName bindings can't reach the window. Feed it the
            // window's DataContext (the MainViewModel) explicitly — the
            // buttons live under panels whose DataContext is SelectedGame.
            menu.DataContext = Window.GetWindow(element)?.DataContext;
            menu.IsOpen = true;
        }
    }

    private void FilterPresetMenu_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm
            && sender is MenuItem item
            && item.Tag is string tag)
        {
            vm.FilterPreset = tag switch
            {
                "Favorite" => Bridge.Core.Enums.LibraryFilterPreset.Favorite,
                "Installed" => Bridge.Core.Enums.LibraryFilterPreset.Installed,
                "NotPlayed" => Bridge.Core.Enums.LibraryFilterPreset.NotPlayed,
                "RecentlyPlayed" => Bridge.Core.Enums.LibraryFilterPreset.RecentlyPlayed,
                _ => Bridge.Core.Enums.LibraryFilterPreset.All
            };

            // A checkable MenuItem toggles its own IsChecked on click even
            // when the source value doesn't change (clicking the already
            // active entry), which would visually untick it while the filter
            // stays on. Re-assert every entry's check from the real state.
            ReassertMenuChecks(item, tag);
        }
    }

    private void SortFieldMenu_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm
            && sender is MenuItem item
            && item.Tag is string tag)
        {
            vm.SortField = tag switch
            {
                "PlaytimeSeconds" => Bridge.Core.Enums.GameSortField.PlaytimeSeconds,
                "PlayCount" => Bridge.Core.Enums.GameSortField.PlayCount,
                "LastPlayed" => Bridge.Core.Enums.GameSortField.LastPlayed,
                "ReleaseDate" => Bridge.Core.Enums.GameSortField.ReleaseDate,
                "Developer" => Bridge.Core.Enums.GameSortField.Developer,
                "Publisher" => Bridge.Core.Enums.GameSortField.Publisher,
                "Source" => Bridge.Core.Enums.GameSortField.Source,
                "CriticScore" => Bridge.Core.Enums.GameSortField.CriticScore,
                _ => Bridge.Core.Enums.GameSortField.Name
            };

            ReassertMenuChecks(item, tag);
        }
    }

    private void GroupFieldMenu_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm
            && sender is MenuItem item
            && item.Tag is string tag)
        {
            vm.GroupField = tag switch
            {
                "Library" => Bridge.Core.Enums.GameGroupField.Library,
                "Developer" => Bridge.Core.Enums.GameGroupField.Developer,
                "Publisher" => Bridge.Core.Enums.GameGroupField.Publisher,
                "Platform" => Bridge.Core.Enums.GameGroupField.Platform,
                "Genre" => Bridge.Core.Enums.GameGroupField.Genre,
                "IsInstalled" => Bridge.Core.Enums.GameGroupField.IsInstalled,
                "CompletionStatus" => Bridge.Core.Enums.GameGroupField.CompletionStatus,
                "PlaytimeSeconds" => Bridge.Core.Enums.GameGroupField.PlaytimeSeconds,
                "PlayCount" => Bridge.Core.Enums.GameGroupField.PlayCount,
                "ReleaseYear" => Bridge.Core.Enums.GameGroupField.ReleaseYear,
                "LastPlayed" => Bridge.Core.Enums.GameGroupField.LastPlayed,
                _ => Bridge.Core.Enums.GameGroupField.None
            };

            ReassertMenuChecks(item, tag);
        }
    }

    // A checkable MenuItem toggles its own IsChecked on click even when the
    // bound source value doesn't change (e.g. clicking the already-active
    // filter/sort/group). That would visually untick the active entry while
    // it stays applied. Re-assert the check on every sibling entry from the
    // tag of the one that was just clicked.
    private static void ReassertMenuChecks(System.Windows.Controls.MenuItem clicked, string activeTag)
    {
        // Find the ItemsControl (ContextMenu or submenu) that owns the
        // clicked item, then tick exactly the entry whose Tag matches.
        ItemsControl? owner =
            ItemsControl.ItemsControlFromItemContainer(clicked);
        if (owner is null)
        {
            return;
        }

        foreach (System.Windows.Controls.MenuItem sibling in owner.Items.OfType<System.Windows.Controls.MenuItem>())
        {
            sibling.IsChecked = sibling.Tag is string siblingTag && siblingTag == activeTag;
        }
    }

    // The shared right-click menu (Bridge.GameContextMenu) is one instance
    // shared by every row in the three list views. Its PlacementTarget is the
    // row that was right-clicked; resolve the clicked game from its
    // DataContext (Game in List/Covers, GameDetailRow in Table) so the menu
    // commands act on THAT game, not whatever was last selected. Feeding the
    // menu the window's DataContext makes the command bindings resolve — a
    // ContextMenu lives in a Popup outside the visual tree, so ElementName
    // bindings can't reach the window on their own.
    private void GameContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu menu
            || menu.PlacementTarget is not FrameworkElement row
            || DataContext is not MainViewModel vm)
        {
            return;
        }

        var game = row.DataContext switch
        {
            Game g => g,
            GameDetailRow detail => detail.Game,
            _ => null
        };
        if (game is not null)
        {
            vm.SelectedGame = game;
        }

        menu.DataContext = DataContext;
    }

    // Opens a game link from a More-menu "Links" submenu item. The submenu
    // items are generated from SelectedGameLinks, so the item's DataContext
    // is the Link itself.
    private void OpenLinkMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm
            && sender is MenuItem { DataContext: Link link })
        {
            vm.OpenLinkCommand.Execute(link);
        }
    }

    // Applies a completion status from the More-menu submenu. The items are
    // generated from CompletionStatuses, so the DataContext is the status
    // name string.
    private void CompletionStatusMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm
            && sender is MenuItem { DataContext: string status })
        {
            vm.SetCompletionStatusCommand.Execute(status);
        }
    }

    // View > Sidebar: show/hide the sidebar (and its divider). The state
    // lives in the VM so the menus' icons (Eye / EyeOff) reflect it.
    private void ToggleSidebar_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.SidebarVisible = !vm.SidebarVisible;
            ApplySidebarVisibility();
        }
    }

    private void ApplySidebarVisibility()
    {
        var visible = (DataContext as MainViewModel)?.SidebarVisible ?? true;
        SidebarHost.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        SidebarSeparator.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    // Keeps both Sidebar menus (main menu + right-click) in sync with the
    // actual sidebar state when they open.
    private void SidebarMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is ContextMenu menu)
        {
            SyncSidebarMenu(menu);
        }
    }

    private void MainMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is ContextMenu menu)
        {
            foreach (var child in menu.Items.OfType<MenuItem>())
            {
                if (child.Header?.ToString() == Strings.Sidebar)
                {
                    SyncSidebarMenu(child);
                }
                else if (child.Header?.ToString() == Strings.Theme)
                {
                    SyncThemeMenu(child);
                }
                else if (child.Header?.ToString() == Strings.ThirdPartyClients)
                {
                    SyncThirdPartyClientsMenu(child);
                }
            }
        }
    }

    private void SyncThemeMenu(ItemsControl themeMenu)
    {
        var current = ThemeManager.ToHex(ThemeManager.CurrentAccent);
        foreach (var item in themeMenu.Items.OfType<MenuItem>())
        {
            item.IsChecked = item.Tag?.ToString() == current;
        }
    }

    private void SyncSidebarMenu(ItemsControl menu)
    {
        foreach (var child in menu.Items.OfType<MenuItem>())
        {
            if (child.Header?.ToString() == Strings.Position)
            {
                foreach (var position in child.Items.OfType<MenuItem>())
                {
                    position.IsChecked = position.Tag?.ToString() == _sidebarPosition;
                }
            }
        }
    }

    // "3rd party clients": show the launcher clients that are installed on
    // this machine (Steam and/or Epic) and open the one the user picks.
    private static void SyncThirdPartyClientsMenu(ItemsControl menu)
    {
        menu.Items.Clear();

        var steamPath = SteamPaths.GetInstallationPath();
        if (!string.IsNullOrWhiteSpace(steamPath))
        {
            var steamExe = Path.Combine(steamPath, "steam.exe");
            if (File.Exists(steamExe))
            {
                menu.Items.Add(CreateClientMenuItem("Steam", steamExe));
            }
        }

        var epicPath = EpicPaths.GetInstallationPath();
        if (!string.IsNullOrWhiteSpace(epicPath))
        {
            var epicExe = EpicPaths.GetExecutablePath(epicPath);
            if (File.Exists(epicExe))
            {
                menu.Items.Add(CreateClientMenuItem("Epic", epicExe));
            }
        }
    }

    private static System.Windows.Controls.MenuItem CreateClientMenuItem(string name, string executable)
    {
        var item = new System.Windows.Controls.MenuItem
        {
            Header = name,
            Tag = executable
        };
        item.Click += OpenThirdPartyClient_Click;
        item.Icon = new SymbolIcon
        {
            Symbol = SymbolRegular.Cloud24,
            FontSize = 16,
            Foreground = System.Windows.Application.Current.TryFindResource("SystemAccentColorPrimaryBrush") as System.Windows.Media.Brush
                ?? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray)
        };
        return item;
    }

    private static void OpenThirdPartyClient_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: string executable }
            && File.Exists(executable))
        {
            Process.Start(new ProcessStartInfo { FileName = executable, UseShellExecute = true });
        }
    }

    // View > Sidebar > Position: dock the sidebar on any edge. The sidebar
    // stays a vertical rail for Left/Right and becomes a horizontal bar for
    // Top/Bottom; the divider follows to the facing edge.
    private void SetSidebarPosition_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string position } item)
        {
            return;
        }

        if (item.Parent is MenuItem positionMenu)
        {
            foreach (var child in positionMenu.Items.OfType<MenuItem>())
            {
                child.IsChecked = false;
            }

            item.IsChecked = true;
        }

        _sidebarPosition = position;

        switch (position)
        {
            case "Right":
                DockPanel.SetDock(SidebarHost, Dock.Right);
                DockPanel.SetDock(SidebarSeparator, Dock.Right);
                SidebarHost.Width = 52;
                SidebarHost.Height = double.NaN;
                SidebarSeparator.Width = 1;
                SidebarSeparator.Height = double.NaN;
                SidebarStack.Orientation = Orientation.Vertical;
                break;
            case "Top":
                DockPanel.SetDock(SidebarHost, Dock.Top);
                DockPanel.SetDock(SidebarSeparator, Dock.Top);
                SidebarHost.Height = 52;
                SidebarHost.Width = double.NaN;
                SidebarSeparator.Height = 1;
                SidebarSeparator.Width = double.NaN;
                SidebarStack.Orientation = Orientation.Horizontal;
                break;
            case "Bottom":
                DockPanel.SetDock(SidebarHost, Dock.Bottom);
                DockPanel.SetDock(SidebarSeparator, Dock.Bottom);
                SidebarHost.Height = 52;
                SidebarHost.Width = double.NaN;
                SidebarSeparator.Height = 1;
                SidebarSeparator.Width = double.NaN;
                SidebarStack.Orientation = Orientation.Horizontal;
                break;
            default: // Left
                DockPanel.SetDock(SidebarHost, Dock.Left);
                DockPanel.SetDock(SidebarSeparator, Dock.Left);
                SidebarHost.Width = 52;
                SidebarHost.Height = double.NaN;
                SidebarSeparator.Width = 1;
                SidebarSeparator.Height = double.NaN;
                SidebarStack.Orientation = Orientation.Vertical;
                break;
        }

        // The active-item indicator border follows the sidebar edge.
        var indicator = position switch
        {
            "Right" => new Thickness(0, 0, 3, 0),
            "Top" => new Thickness(0, 3, 0, 0),
            "Bottom" => new Thickness(0, 0, 0, 3),
            _ => new Thickness(3, 0, 0, 0)
        };
        foreach (var button in SidebarStack.Children.OfType<Button>())
        {
            button.BorderThickness = indicator;
        }
    }

    // Theme menu: apply a preset accent (the whole palette recomputes).
    private void SetThemeColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: string hex }
            && ThemeManager.TryParseHex(hex, out var color))
        {
            ThemeManager.Apply(color);
        }
    }

    // Theme menu: open the custom color picker.
    private void CustomThemeColor_Click(object sender, RoutedEventArgs e)
    {
        var window = new ThemeColorWindow { Owner = this };
        window.ShowDialog();
    }

    private void ShowLibrary_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.NavigationSection = Bridge.Core.Enums.NavigationSection.Library;
    }

    private void ShowFavorites_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.NavigationSection = Bridge.Core.Enums.NavigationSection.Favorites;
    }

    private void ShowSources_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.NavigationSection = Bridge.Core.Enums.NavigationSection.Sources;
    }

    private void ShowStatistics_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.NavigationSection = Bridge.Core.Enums.NavigationSection.Statistics;
    }

    private void ShowSettings_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.NavigationSection = Bridge.Core.Enums.NavigationSection.Settings;
    }

    // Selects a random game from whatever the current view shows
    // (respects the active search/filter/sort).
    private void SelectRandomGame_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
            return;

        // GamesView, when grouped, enumerates CollectionViewGroup wrappers —
        // OfType<Game> would come up empty. Enumerate Games and apply the
        // same filter predicate the view uses, so grouping can't break random.
        var visible = vm.Games
            .Where(g => vm.GamesView.Filter is null || vm.GamesView.Filter(g))
            .ToList();
        if (visible.Count == 0)
        {
            return;
        }

        vm.SelectedGame = visible[Random.Shared.Next(visible.Count)];
    }
}
