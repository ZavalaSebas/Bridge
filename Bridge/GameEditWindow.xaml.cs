using System.Windows;
using Bridge.ViewModels;
using Microsoft.Win32;

namespace Bridge
{
    public partial class GameEditWindow : Window
    {
        private readonly GameEditViewModel _viewModel;

        public GameEditWindow(GameEditViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = viewModel;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.Save();
            DialogResult = true;
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
