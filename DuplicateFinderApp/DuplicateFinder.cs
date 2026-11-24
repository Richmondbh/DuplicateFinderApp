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

            // Reading the disk can take a long time, so we do it on a
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

        /// <summary>
        /// Builds a text "key" from the selected attributes of a file.
        /// Files with the same key are duplicates of each other.
        /// Example with Size and Date modified: "52341|2025-01-15 10:30:00".
        /// </summary>
        /// <param name="file">The file.</param>
        /// <param name="settings">The attributes to use.</param>
        /// <param name="token">Lets the caller cancel the work.</param>
        public Task<string> CreateKeyAsync(FileInfo file, CompareSettings settings, CancellationToken token = default)
        {
            var parts = new List<string>();

            if (settings.Size)
            {
                parts.Add(file.Length.ToString());
            }
            if (settings.DateModified)
            {
                parts.Add(file.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"));
            }

            // Nothing here needs to wait yet, so we return a finished task.
            // In a later commit the hash makes this method truly asynchronous.
            return Task.FromResult(string.Join("|", parts));
        }

        /// <summary>
        /// Finds duplicate files. This is the main method that the window calls.
        /// Steps: 1) get all files, 2) keep the selected file type,
        /// 3) give every file a key, 4) files with the same key are duplicates.
        /// </summary>
        /// <param name="folderPath">The folder to search (subfolders included).</param>
        /// <param name="settings">The attributes to compare.</param>
        /// <param name="fileType">The type of files to search for.</param>
        /// <param name="token">Lets the caller cancel the search.</param>
        /// <returns>One DuplicateGroup for every file that has duplicates.</returns>
        public async Task<List<DuplicateGroup>> FindDuplicatesAsync(string folderPath, CompareSettings settings,
                                                                    FileType fileType, CancellationToken token = default)
        {
            if (!settings.AnySelected)
            {
                throw new ArgumentException("Select at least one attribute to compare.");
            }

            List<FileInfo> allFiles = await GetFilesAsync(folderPath, token);
            List<FileInfo> files = await FilterByFileTypeAsync(allFiles, fileType);

            // Put files with the same key in the same list.
            var filesByKey = new Dictionary<string, List<FileInfo>>();
            foreach (FileInfo file in files)
            {
                token.ThrowIfCancellationRequested();

                string key = await CreateKeyAsync(file, settings, token);
                if (!filesByKey.ContainsKey(key))
                {
                    filesByKey[key] = new List<FileInfo>();
                }
                filesByKey[key].Add(file);
            }

            // A list with more than one file means we found duplicates.
            // The first file is the original, the rest are the duplicates.
            var groups = new List<DuplicateGroup>();
            foreach (List<FileInfo> sameFiles in filesByKey.Values)
            {
                if (sameFiles.Count > 1)
                {
                    groups.Add(new DuplicateGroup(sameFiles[0], sameFiles.Skip(1).ToList()));
                }
            }
            return groups;
        }
    }
}
