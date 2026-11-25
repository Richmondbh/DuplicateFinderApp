using System.Windows;
using System.Windows.Controls;
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
        private readonly DuplicateFinder _finder = new DuplicateFinder();

        // The result of the latest search.
        private List<DuplicateGroup> _groups = new List<DuplicateGroup>();

        // Used to cancel a search that is running. Null when no search runs.
        private CancellationTokenSource? _cancellation;

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
                ClearResults();
                txtStatus.Text = "Folder selected. Choose your options and click Find duplicates.";
            }
        }

        /// <summary>
        /// Starts the search. The method is async and awaits FindDuplicatesAsync,
        /// so the window does not freeze while the files are being searched.
        /// </summary>
        private async void BtnFind_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtFolder.Text))
            {
                MessageBox.Show("Please select a folder first.", "No folder",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            CompareSettings settings = GetCompareSettings();
            if (!settings.AnySelected)
            {
                MessageBox.Show("Please select at least one attribute to compare.", "No attribute",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _cancellation = new CancellationTokenSource();
            SetSearching(true);
            ClearResults();
            txtStatus.Text = "Searching for duplicates...";

            try
            {
                _groups = await _finder.FindDuplicatesAsync(txtFolder.Text, settings, GetFileType(),
                                                            _cancellation.Token);
                ShowResults();
            }
            catch (OperationCanceledException)
            {
                // The user clicked Cancel.
                ClearResults();
                txtStatus.Text = "The search was cancelled.";
            }
            catch (Exception ex)
            {
                ClearResults();
                txtStatus.Text = "The search failed.";
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SetSearching(false);
                _cancellation.Dispose();
                _cancellation = null;
            }
        }

        /// <summary>
        /// Asks the running search to stop.
        /// </summary>
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            _cancellation?.Cancel();
            txtStatus.Text = "Cancelling...";
        }

        /// <summary>
        /// Deletes all duplicates after the user has confirmed.
        /// The original (first occurrence) of every file is kept.
        /// </summary>
        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            int duplicateCount = _groups.Sum(g => g.Duplicates.Count);
            if (duplicateCount == 0)
            {
                return;
            }

            // Always ask first, so no files are deleted by accident.
            MessageBoxResult answer = MessageBox.Show(
                $"Do you want to delete {duplicateCount} duplicate file(s)?\n\n" +
                "One copy of every file (the list on the left) is kept.\n" +
                "Deleted files are NOT moved to the Recycle Bin.",
                "Confirm delete", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);

            if (answer != MessageBoxResult.Yes)
            {
                txtStatus.Text = "Nothing was deleted.";
                return;
            }

            btnDelete.IsEnabled = false;
            try
            {
                int deleted = await _finder.DeleteDuplicatesAsync(_groups);
                ClearResults();
                txtStatus.Text = $"{deleted} of {duplicateCount} duplicate file(s) deleted.";
            }
            catch (Exception ex)
            {
                txtStatus.Text = "Deleting failed.";
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Shows the duplicates of the file the user selects in list one.
        /// </summary>
        private void LvOriginals_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lvOriginals.SelectedItem is DuplicateGroup group)
            {
                lvDuplicates.ItemsSource = group.Duplicates;
            }
            else
            {
                lvDuplicates.ItemsSource = null;
            }
        }

        /// <summary>
        /// Reads which attributes the user has checked.
        /// </summary>
        private CompareSettings GetCompareSettings()
        {
            return new CompareSettings
            {
                Name = chkName.IsChecked == true,
                Size = chkSize.IsChecked == true,
                DateModified = chkDate.IsChecked == true,
                Hash = chkHash.IsChecked == true
            };
        }

        /// <summary>
        /// Reads which file type the user has selected.
        /// </summary>
        private FileType GetFileType()
        {
            if (rbPictures.IsChecked == true)
            {
                return FileType.Pictures;
            }
            if (rbPdf.IsChecked == true)
            {
                return FileType.Pdf;
            }
            if (rbText.IsChecked == true)
            {
                return FileType.Text;
            }
            return FileType.AllFiles;
        }

        /// <summary>
        /// Shows the result of a search in the two lists.
        /// </summary>
        private void ShowResults()
        {
            lvOriginals.ItemsSource = _groups;

            int duplicateCount = _groups.Sum(g => g.Duplicates.Count);
            if (_groups.Count == 0)
            {
                txtStatus.Text = "No duplicates found.";
            }
            else
            {
                txtStatus.Text = $"Found {_groups.Count} file(s) that have {duplicateCount} duplicate(s) in total. " +
                                 "Select a file on the left to see its duplicates.";
                lvOriginals.SelectedIndex = 0;
            }

            btnDelete.IsEnabled = duplicateCount > 0;
        }

        /// <summary>
        /// Empties both lists.
        /// </summary>
        private void ClearResults()
        {
            _groups = new List<DuplicateGroup>();
            lvOriginals.ItemsSource = null;
            lvDuplicates.ItemsSource = null;
            btnDelete.IsEnabled = false;
        }

        /// <summary>
        /// Turns buttons on or off while a search is running.
        /// </summary>
        private void SetSearching(bool searching)
        {
            btnFind.IsEnabled = !searching;
            btnBrowse.IsEnabled = !searching;
            btnCancel.IsEnabled = searching;
            progressBar.Visibility = searching ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
