using System.IO;

namespace DuplicateFinderApp
{
    /// <summary>
    /// Finds duplicate files in a folder and its subfolders.
    /// This class has no GUI code (no windows or message boxes), so the
    /// MainWindow can use it and the unit test projects can test it.
    /// </summary>
    public class DuplicateFinder
    {
        /// <summary>
        /// Gets all files in a folder and in all its subfolders, sorted by path.
        /// </summary>
        /// <param name="folderPath">The folder to search.</param>
        /// <param name="token">Lets the caller cancel the search.</param>
        /// <returns>A list with one FileInfo per file.</returns>
        /// <exception cref="DirectoryNotFoundException">The folder does not exist.</exception>
       
        public async Task<List<FileInfo>> GetFilesAsync(string folderPath, CancellationToken token = default)
        {
            if (!Directory.Exists(folderPath))
            {
                throw new DirectoryNotFoundException($"The folder '{folderPath}' does not exist.");
            }

            // Reading the disk can take a long time, so i do it on a
            // background thread. The window stays responsive meanwhile.
            return await Task.Run(() =>
            {
                var options = new EnumerationOptions
                {
                    RecurseSubdirectories = true, // also look in subfolders
                    IgnoreInaccessible = true     // skip folders we may not read
                };

                var files = new List<FileInfo>();
                foreach (string path in Directory.EnumerateFiles(folderPath, "*", options))
                {
                    token.ThrowIfCancellationRequested();
                    files.Add(new FileInfo(path));
                }

                // Sort by path, so the "first occurrence" is always the same file.
                return files.OrderBy(f => f.FullName, StringComparer.OrdinalIgnoreCase).ToList();
            }, token);
        }

        /// <summary>
        /// Keeps only the files of the selected file type.
        /// </summary>
        /// <param name="files">The files to filter.</param>
        /// <param name="fileType">The file type to keep.</param>
        /// <returns>A new list with only the matching files.</returns>
        public async Task<List<FileInfo>> FilterByFileTypeAsync(List<FileInfo> files, FileType fileType)
        {
            string[] extensions = GetExtensions(fileType);

            return await Task.Run(() =>
            {
                if (fileType == FileType.AllFiles)
                {
                    return files.ToList();
                }

                return files
                    .Where(f => extensions.Contains(f.Extension.ToLowerInvariant()))
                    .ToList();
            });
        }

        /// <summary>
        /// Gets the file extensions that belong to a file type.
        /// AllFiles returns an empty array, because it has no filter.
        /// </summary>
        public static string[] GetExtensions(FileType fileType)
        {
            switch (fileType)
            {
                case FileType.Pictures:
                    return new[] { ".jpg", ".png" };
                case FileType.Pdf:
                    return new[] { ".pdf" };
                default:
                    return new string[0];
            }
        }
    }
}
