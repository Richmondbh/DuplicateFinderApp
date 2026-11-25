using DuplicateFinderApp;

namespace DuplicateFinderTest
{
    /// <summary>
    /// MSTest tests for the DuplicateFinder class.
    /// Every test gets its own new temporary folder with test files, and the
    /// folder is deleted after the test. That way no real files are ever touched.
    /// All tests follow the AAA pattern: Arrange, Act, Assert.
    /// </summary>
    [TestClass]
    public sealed class DuplicateFinderTests
    {
        // A fixed date, so files we create can get exactly the same "date modified".
        private static readonly DateTime SameDate = new DateTime(2025, 1, 15, 10, 30, 0);

        private DuplicateFinder _finder = null!;
        private string _testFolder = null!;

        /// <summary>Runs before every test: creates an empty temporary folder.</summary>
        [TestInitialize]
        public void Setup()
        {
            _finder = new DuplicateFinder();
            _testFolder = Path.Combine(Path.GetTempPath(), "DuplicateFinderTest_" + Guid.NewGuid());
            Directory.CreateDirectory(_testFolder);
        }

        /// <summary>Runs after every test: deletes the temporary folder.</summary>
        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_testFolder))
            {
                Directory.Delete(_testFolder, recursive: true);
            }
        }

        /// <summary>
        /// Helper: creates a file in the test folder with the given text and date modified.
        /// relativePath may contain a subfolder, for example "sub\\photo.jpg".
        /// </summary>
        private async Task<string> CreateFileAsync(string relativePath, string content, DateTime? modified = null)
        {
            string path = Path.Combine(_testFolder, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, content);
            File.SetLastWriteTime(path, modified ?? SameDate);
            return path;
        }

        // ---------------------------------------------------------------
        // GetFilesAsync
        // ---------------------------------------------------------------

        [TestMethod]
        public async Task GetFilesAsync_FolderWithSubfolders_ReturnsAllFiles()
        {
            // Arrange
            await CreateFileAsync("a.txt", "one");
            await CreateFileAsync(Path.Combine("sub", "b.txt"), "two");
            await CreateFileAsync(Path.Combine("sub", "deeper", "c.pdf"), "three");

            // Act
            List<FileInfo> files = await _finder.GetFilesAsync(_testFolder);

            // Assert
            Assert.AreEqual(3, files.Count);
        }

        [TestMethod]
        public async Task GetFilesAsync_FolderDoesNotExist_ThrowsDirectoryNotFoundException()
        {
            // Arrange
            string missingFolder = Path.Combine(_testFolder, "does-not-exist");

            // Act and Assert
            await Assert.ThrowsAsync<DirectoryNotFoundException>(
                () => _finder.GetFilesAsync(missingFolder));
        }

        // ---------------------------------------------------------------
        // FilterByFileTypeAsync
        // ---------------------------------------------------------------

        [TestMethod]
        public async Task FilterByFileTypeAsync_Pdf_ReturnsOnlyPdfFiles()
        {
            // Arrange
            await CreateFileAsync("report.pdf", "pdf");
            await CreateFileAsync("photo.jpg", "jpg");
            await CreateFileAsync("notes.txt", "txt");
            List<FileInfo> allFiles = await _finder.GetFilesAsync(_testFolder);

            // Act
            List<FileInfo> result = await _finder.FilterByFileTypeAsync(allFiles, FileType.Pdf);

            // Assert
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("report.pdf", result[0].Name);
        }

        [TestMethod]
        public async Task FilterByFileTypeAsync_AllFiles_ReturnsEveryFile()
        {
            // Arrange
            await CreateFileAsync("report.pdf", "pdf");
            await CreateFileAsync("photo.jpg", "jpg");
            await CreateFileAsync("notes.txt", "txt");
            List<FileInfo> allFiles = await _finder.GetFilesAsync(_testFolder);

            // Act
            List<FileInfo> result = await _finder.FilterByFileTypeAsync(allFiles, FileType.AllFiles);

            // Assert
            Assert.AreEqual(3, result.Count);
        }

        // ---------------------------------------------------------------
        // FindDuplicatesAsync
        // ---------------------------------------------------------------

        [TestMethod]
        public async Task FindDuplicatesAsync_SameSizeAndDate_FindsDuplicates()
        {
            // Arrange: a.txt has one copy in a subfolder, c.txt is unique
            await CreateFileAsync("a.txt", "Hello");
            await CreateFileAsync(Path.Combine("copies", "a - copy.txt"), "Hello");
            await CreateFileAsync("c.txt", "A longer text that is unique");
            var settings = new CompareSettings { Size = true, DateModified = true };

            // Act
            List<DuplicateGroup> groups = await _finder.FindDuplicatesAsync(_testFolder, settings, FileType.AllFiles);

            // Assert
            Assert.AreEqual(1, groups.Count);
            Assert.AreEqual("a.txt", groups[0].Original.Name);
            Assert.AreEqual(1, groups[0].Duplicates.Count);
            Assert.AreEqual("a - copy.txt", groups[0].Duplicates[0].Name);
        }

        [TestMethod]
        public async Task FindDuplicatesAsync_SameSizeDifferentDate_FindsNoDuplicates()
        {
            // Arrange
            await CreateFileAsync("a.txt", "Hello", new DateTime(2024, 5, 1));
            await CreateFileAsync("b.txt", "Hello", new DateTime(2025, 5, 1));
            var settings = new CompareSettings { Size = true, DateModified = true };

            // Act
            List<DuplicateGroup> groups = await _finder.FindDuplicatesAsync(_testFolder, settings, FileType.AllFiles);

            // Assert
            Assert.AreEqual(0, groups.Count);
        }

        [TestMethod]
        public async Task FindDuplicatesAsync_PdfSelected_IgnoresOtherFileTypes()
        {
            // Arrange: two equal .pdf files and two equal .jpg files
            await CreateFileAsync("doc1.pdf", "pdf content");
            await CreateFileAsync("doc2.pdf", "pdf content");
            await CreateFileAsync("img1.jpg", "jpg content");
            await CreateFileAsync("img2.jpg", "jpg content");
            var settings = new CompareSettings { Size = true, DateModified = true };

            // Act
            List<DuplicateGroup> groups = await _finder.FindDuplicatesAsync(_testFolder, settings, FileType.Pdf);

            // Assert: only the pdf group is found
            Assert.AreEqual(1, groups.Count);
            Assert.AreEqual("doc1.pdf", groups[0].Original.Name);
        }
    }
}
