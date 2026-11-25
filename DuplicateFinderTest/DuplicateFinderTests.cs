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
        private static readonly DateTime SameDate = new DateTime(2026, 1, 15, 10, 30, 0);

        private DuplicateFinder _finder = null!;
        private string _testFolder = null!;

        /// Runs before every test: creates an empty temporary folder.
        [TestInitialize]
        public void Setup()
        {
            _finder = new DuplicateFinder();
            _testFolder = Path.Combine(Path.GetTempPath(), "DuplicateFinderTest_" + Guid.NewGuid());
            Directory.CreateDirectory(_testFolder);
        }

        ///Runs after every test: deletes the temporary folder.
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

      
        // GetFilesAsync
      

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

        [TestMethod]
        public async Task GetFilesAsync_EmptyFolder_ReturnsEmptyList()
        {
            // Arrange: the test folder is empty

            // Act
            List<FileInfo> files = await _finder.GetFilesAsync(_testFolder);

            // Assert
            Assert.AreEqual(0, files.Count);
        }

        [TestMethod]
        public async Task GetFilesAsync_CancelledToken_ThrowsOperationCanceledException()
        {
            // Arrange
            await CreateFileAsync("a.txt", "one");
            var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            // Act and Assert
            await Assert.ThrowsAsync<OperationCanceledException>(
                () => _finder.GetFilesAsync(_testFolder, cancellation.Token));
        }

        // FilterByFileTypeAsync
 

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

        [TestMethod]
        public async Task FilterByFileTypeAsync_Pictures_ReturnsJpgAndPngFiles()
        {
            // Arrange
            await CreateFileAsync("photo.jpg", "jpg");
            await CreateFileAsync("PHOTO2.JPG", "jpg"); // upper case must also work
            await CreateFileAsync("logo.png", "png");
            await CreateFileAsync("report.pdf", "pdf");
            List<FileInfo> allFiles = await _finder.GetFilesAsync(_testFolder);

            // Act
            List<FileInfo> result = await _finder.FilterByFileTypeAsync(allFiles, FileType.Pictures);

            // Assert
            Assert.AreEqual(3, result.Count);
        }

        [TestMethod]
        public async Task FilterByFileTypeAsync_Text_ReturnsOnlyTxtFiles()
        {
            // Arrange
            await CreateFileAsync("notes.txt", "txt");
            await CreateFileAsync("report.pdf", "pdf");
            List<FileInfo> allFiles = await _finder.GetFilesAsync(_testFolder);

            // Act
            List<FileInfo> result = await _finder.FilterByFileTypeAsync(allFiles, FileType.Text);

            // Assert
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("notes.txt", result[0].Name);
        }

       
        // GetExtensions
        

        [TestMethod]
        public async Task GetExtensions_Pictures_ReturnsJpgAndPng()
        {
            // Arrange
            FileType fileType = FileType.Pictures;

            // Act (GetExtensions is not async, so i wrap it in Task.Run to await it)
            string[] extensions = await Task.Run(() => DuplicateFinder.GetExtensions(fileType));

            // Assert
            CollectionAssert.AreEqual(new[] { ".jpg", ".png" }, extensions);
        }

      
        // ComputeHashAsync
        

        [TestMethod]
        public async Task ComputeHashAsync_SameContent_ReturnsSameHash()
        {
            // Arrange
            string file1 = await CreateFileAsync("a.txt", "Hello world");
            string file2 = await CreateFileAsync("b.txt", "Hello world");

            // Act
            string hash1 = await _finder.ComputeHashAsync(file1);
            string hash2 = await _finder.ComputeHashAsync(file2);

            // Assert
            Assert.AreEqual(hash1, hash2);
            Assert.AreEqual(64, hash1.Length); // SHA-256 = 32 bytes = 64 hex characters
        }

        [TestMethod]
        public async Task ComputeHashAsync_DifferentContent_ReturnsDifferentHash()
        {
            // Arrange
            string file1 = await CreateFileAsync("a.txt", "Hello world");
            string file2 = await CreateFileAsync("b.txt", "Hello WORLD");

            // Act
            string hash1 = await _finder.ComputeHashAsync(file1);
            string hash2 = await _finder.ComputeHashAsync(file2);

            // Assert
            Assert.AreNotEqual(hash1, hash2);
        }

      
        // CreateKeyAsync
        

        [TestMethod]
        public async Task CreateKeyAsync_SizeAndDate_ReturnsSizeAndDate()
        {
            // Arrange
            string path = await CreateFileAsync("a.txt", "12345"); // 5 bytes
            var settings = new CompareSettings { Size = true, DateModified = true };

            // Act
            string key = await _finder.CreateKeyAsync(new FileInfo(path), settings);

            // Assert
            Assert.AreEqual("5|2025-01-15 10:30:00", key);
        }

        [TestMethod]
        public async Task CreateKeyAsync_SameNameDifferentFolders_ReturnsSameKey()
        {
            // Arrange
            string path1 = await CreateFileAsync("photo.jpg", "first");
            string path2 = await CreateFileAsync(Path.Combine("backup", "photo.jpg"), "other content");
            var settings = new CompareSettings { Name = true };

            // Act
            string key1 = await _finder.CreateKeyAsync(new FileInfo(path1), settings);
            string key2 = await _finder.CreateKeyAsync(new FileInfo(path2), settings);

            // Assert
            Assert.AreEqual(key1, key2);
        }

        
        // FindDuplicatesAsync
       

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

        [TestMethod]
        public async Task FindDuplicatesAsync_Hash_FindsSameContentEvenWithDifferentNameAndDate()
        {
            // Arrange
            await CreateFileAsync("original.txt", "Same content", new DateTime(2020, 1, 1));
            await CreateFileAsync("renamed.txt", "Same content", new DateTime(2025, 1, 1));
            var settings = new CompareSettings { Hash = true };

            // Act
            List<DuplicateGroup> groups = await _finder.FindDuplicatesAsync(_testFolder, settings, FileType.AllFiles);

            // Assert
            Assert.AreEqual(1, groups.Count);
            Assert.AreEqual(1, groups[0].Duplicates.Count);
        }

        [TestMethod]
        public async Task FindDuplicatesAsync_SizeAndHash_DoesNotMatchSameSizeDifferentContent()
        {
            // Arrange: same size (5 bytes) and date, but different content
            await CreateFileAsync("a.txt", "Hello");
            await CreateFileAsync("b.txt", "World");
            var settings = new CompareSettings { Size = true, DateModified = true, Hash = true };

            // Act
            List<DuplicateGroup> groups = await _finder.FindDuplicatesAsync(_testFolder, settings, FileType.AllFiles);

            // Assert: the hash shows they are different files
            Assert.AreEqual(0, groups.Count);
        }

        [TestMethod]
        public async Task FindDuplicatesAsync_ThreeEqualFiles_ReturnsOneOriginalAndTwoDuplicates()
        {
            // Arrange
            await CreateFileAsync("a.txt", "Hello");
            await CreateFileAsync("b.txt", "Hello");
            await CreateFileAsync("c.txt", "Hello");
            var settings = new CompareSettings { Size = true, Hash = true };

            // Act
            List<DuplicateGroup> groups = await _finder.FindDuplicatesAsync(_testFolder, settings, FileType.Text);

            // Assert
            Assert.AreEqual(1, groups.Count);
            Assert.AreEqual("a.txt", groups[0].Original.Name);
            Assert.AreEqual(2, groups[0].Duplicates.Count);
        }

        [TestMethod]
        public async Task FindDuplicatesAsync_NoAttributeSelected_ThrowsArgumentException()
        {
            // Arrange
            var settings = new CompareSettings(); // everything is false

            // Act and Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => _finder.FindDuplicatesAsync(_testFolder, settings, FileType.AllFiles));
        }

        [TestMethod]
        public async Task FindDuplicatesAsync_CancelledToken_ThrowsOperationCanceledException()
        {
            // Arrange
            await CreateFileAsync("a.txt", "Hello");
            await CreateFileAsync("b.txt", "Hello");
            var settings = new CompareSettings { Size = true };
            var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            // Act and Assert
            await Assert.ThrowsAsync<OperationCanceledException>(
                () => _finder.FindDuplicatesAsync(_testFolder, settings, FileType.AllFiles, cancellation.Token));
        }

        
        // DeleteDuplicatesAsync
       

        [TestMethod]
        public async Task DeleteDuplicatesAsync_DeletesDuplicatesAndKeepsOriginal()
        {
            // Arrange
            string original = await CreateFileAsync("a.txt", "Hello");
            string copy1 = await CreateFileAsync("b.txt", "Hello");
            string copy2 = await CreateFileAsync(Path.Combine("sub", "c.txt"), "Hello");
            var settings = new CompareSettings { Hash = true };
            List<DuplicateGroup> groups = await _finder.FindDuplicatesAsync(_testFolder, settings, FileType.AllFiles);

            // Act
            int deleted = await _finder.DeleteDuplicatesAsync(groups);

            // Assert
            Assert.AreEqual(2, deleted);
            Assert.IsTrue(File.Exists(original));
            Assert.IsFalse(File.Exists(copy1));
            Assert.IsFalse(File.Exists(copy2));
        }

        [TestMethod]
        public async Task DeleteDuplicatesAsync_FileAlreadyGone_SkipsIt()
        {
            // Arrange
            await CreateFileAsync("a.txt", "Hello");
            string copy = await CreateFileAsync("b.txt", "Hello");
            var settings = new CompareSettings { Hash = true };
            List<DuplicateGroup> groups = await _finder.FindDuplicatesAsync(_testFolder, settings, FileType.AllFiles);
            File.Delete(copy); // someone deleted it after the search

            // Act
            int deleted = await _finder.DeleteDuplicatesAsync(groups);

            // Assert
            Assert.AreEqual(0, deleted);
        }
    }
}
