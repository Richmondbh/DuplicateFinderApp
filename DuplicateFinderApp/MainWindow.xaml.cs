using System.Windows;
using Microsoft.Win32;

namespace DuplicateFinderApp
{
    /// <summary>
    /// The main window. It only takes care of the user interface:
    /// reading the user's choices and showing the results.
    /// All the file work is done by the DuplicateFinder class.
    /// </summary>
    public partial class MainWindow : Window
    {

        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Opens a dialog where the user selects the folder to search.
        /// OpenFolderDialog is built into WPF since .NET 8, so no NuGet package is needed.
        /// </summary>
        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select the folder to search"
            };

            if (dialog.ShowDialog() == true)
            {
                txtFolder.Text = dialog.FolderName;
                txtStatus.Text = "Folder selected. Choose your options and click Find duplicates.";
            }
        }
    }
}
