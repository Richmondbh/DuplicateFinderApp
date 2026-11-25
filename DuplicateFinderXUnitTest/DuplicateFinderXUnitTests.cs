using DuplicateFinderApp;

namespace DuplicateFinderXUnitTest
{
    /// <summary>
    /// xUnit tests for the DuplicateFinder class. They test the same things as
    /// the MSTest project, so i can compare the two frameworks.
    ///
    /// Differences from MSTest:
    ///  - [Fact] instead of [TestMethod], [Theory] + [InlineData] instead of [DataRow].
    ///  - No [TestInitialize]: xUnit creates a new object for every test,
    ///    so the constructor is the setup.
    ///  - No [TestCleanup]: Dispose() is the cleanup.
    ///  - Assert.Equal(expected, actual) instead of Assert.AreEqual.
    /// </summary>
    public class DuplicateFinderXUnitTests : IDisposable
    {
        private static readonly DateTime SameDate = new DateTime(2026, 1, 15, 10, 30, 0);

        private readonly DuplicateFinder _finder;
        private readonly string _testFolder;

        /// Setup: runs before every test.
        public DuplicateFinderXUnitTests()
        {
            _finder = new DuplicateFinder();
            _testFolder = Path.Combine(Path.GetTempPath(), "DuplicateFinderXUnit_" + Guid.NewGuid());
            Directory.CreateDirectory(_testFolder);
        }

        //Cleanup: runs after every test.
        public void Dispose()
        {
            if (Directory.Exists(_testFolder))
            {
                Directory.Delete(_testFolder, recursive: true);
            }
        }

        /// >Helper: creates a test file with the given text and date modified.
        private async Task<string> CreateFileAsync(string relativePath, string content, DateTime? modified = null)
        {
            string path = Path.Combine(_testFolder, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, content);
            File.SetLastWriteTime(path, modified ?? SameDate);
            return path;
        }

        // GetFilesAsync 

        [Fact]
        public async Task GetFilesAsync_FolderWithSubfolders_ReturnsAllFiles()
        {
            // Arrange
            await CreateFileAsync("a.txt", "one");
            await CreateFileAsync(Path.Combine("sub", "b.txt"), "two");
            await CreateFileAsync(Path.Combine("sub", "deeper", "c.pdf"), "three");

            // Act
            List<FileInfo> files = await _finder.GetFilesAsync(_testFolder);

            // Assert
            Assert.Equal(3, files.Count);
        }

        [Fact]
        public async Task GetFilesAsync_FolderDoesNotExist_ThrowsDirectoryNotFoundException()
        {
            // Arrange
            string missingFolder = Path.Combine(_testFolder, "does-not-exist");

            // Act and Assert
            await Assert.ThrowsAsync<DirectoryNotFoundException>(
                () => _finder.GetFilesAsync(missingFolder));
        }

        [Fact]
        public async Task GetFilesAsync_CancelledToken_ThrowsOperationCanceledException()
        {
            // Arrange
            await CreateFileAsync("a.txt", "one");
            var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            // Act and Assert (ThrowsAnyAsync also accepts TaskCanceledException, a subclass)
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => _finder.GetFilesAsync(_testFolder, cancellation.Token));
        }

        // FilterByFileTypeAsync 

        /// <summary>
        /// A [Theory] runs once for every [InlineData] row:
        /// the same test with different input.
        /// </summary>
        [Theory]
        [InlineData(FileType.AllFiles, 5)]
        [InlineData(FileType.Pictures, 2)]
        [InlineData(FileType.Pdf, 1)]
        [InlineData(FileType.Text, 1)]
        public async Task FilterByFileTypeAsync_ReturnsOnlySelectedType(FileType fileType, int expectedCount)
        {
            // Arrange
            await CreateFileAsync("photo.jpg", "jpg");
            await CreateFileAsync("logo.PNG", "png");
            await CreateFileAsync("report.pdf", "pdf");
            await CreateFileAsync("notes.txt", "txt");
            await CreateFileAsync("music.mp3", "mp3");
            List<FileInfo> allFiles = await _finder.GetFilesAsync(_testFolder);

            // Act
            List<FileInfo> result = await _finder.FilterByFileTypeAsync(allFiles, fileType);

            // Assert
            Assert.Equal(expectedCount, result.Count);
        }

        // GetExtensions 

        [Fact]
        public async Task GetExtensions_Pictures_ReturnsJpgAndPng()
        {
            // Arrange
            FileType fileType = FileType.Pictures;

            // Act
            string[] extensions = await Task.Run(() => DuplicateFinder.GetExtensions(fileType));

            // Assert
            Assert.Equal(new[] { ".jpg", ".png" }, extensions);
        }

        // ComputeHashAsync 

        [Fact]
        public async Task ComputeHashAsync_SameContent_ReturnsSameHash()
        {
            // Arrange
            string file1 = await CreateFileAsync("a.txt", "Hello world");
            string file2 = await CreateFileAsync("b.txt", "Hello world");

            // Act
            string hash1 = await _finder.ComputeHashAsync(file1);
            string hash2 = await _finder.ComputeHashAsync(file2);

            // Assert
            Assert.Equal(hash1, hash2);
            Assert.Equal(64, hash1.Length);
        }

        [Fact]
        public async Task ComputeHashAsync_DifferentContent_ReturnsDifferentHash()
        {
            // Arrange
            string file1 = await CreateFileAsync("a.txt", "Hello world");
            string file2 = await CreateFileAsync("b.txt", "Hello WORLD");

            // Act
            string hash1 = await _finder.ComputeHashAsync(file1);
            string hash2 = await _finder.ComputeHashAsync(file2);

            // Assert
            Assert.NotEqual(hash1, hash2);
        }

        // CreateKeyAsync 

        [Fact]
        public async Task CreateKeyAsync_SizeAndDate_ReturnsSizeAndDate()
        {
            // Arrange
            string path = await CreateFileAsync("a.txt", "12345");
            var settings = new CompareSettings { Size = true, DateModified = true };

            // Act
            string key = await _finder.CreateKeyAsync(new FileInfo(path), settings);

            // Assert
            Assert.Equal("5|2025-01-15 10:30:00", key);
        }

        [Fact]
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
            Assert.Equal(key1, key2);
        }

        //  FindDuplicatesAsync 

        [Fact]
        public async Task FindDuplicatesAsync_SameSizeAndDate_FindsDuplicates()
        {
            // Arrange
            await CreateFileAsync("a.txt", "Hello");
            await CreateFileAsync(Path.Combine("copies", "a - copy.txt"), "Hello");
            await CreateFileAsync("c.txt", "A longer text that is unique");
            var settings = new CompareSettings { Size = true, DateModified = true };

            // Act
            List<DuplicateGroup> groups = await _finder.FindDuplicatesAsync(_testFolder, settings, FileType.AllFiles);

            // Assert
            DuplicateGroup group = Assert.Single(groups);
            Assert.Equal("a.txt", group.Original.Name);
            Assert.Equal("a - copy.txt", Assert.Single(group.Duplicates).Name);
        }

        [Fact]
        public async Task FindDuplicatesAsync_SameSizeDifferentDate_FindsNoDuplicates()
        {
            // Arrange
            await CreateFileAsync("a.txt", "Hello", new DateTime(2024, 5, 1));
            await CreateFileAsync("b.txt", "Hello", new DateTime(2025, 5, 1));
            var settings = new CompareSettings { Size = true, DateModified = true };

            // Act
            List<DuplicateGroup> groups = await _finder.FindDuplicatesAsync(_testFolder, settings, FileType.AllFiles);

            // Assert
            Assert.Empty(groups);
        }

        [Fact]
        public async Task FindDuplicatesAsync_PdfSelected_IgnoresOtherFileTypes()
        {
            // Arrange
            await CreateFileAsync("doc1.pdf", "pdf content");
            await CreateFileAsync("doc2.pdf", "pdf content");
            await CreateFileAsync("img1.jpg", "jpg content");
            await CreateFileAsync("img2.jpg", "jpg content");
            var settings = new CompareSettings { Size = true, DateModified = true };

            // Act
            List<DuplicateGroup> groups = await _finder.FindDuplicatesAsync(_testFolder, settings, FileType.Pdf);

            // Assert
            Assert.Equal("doc1.pdf", Assert.Single(groups).Original.Name);
        }

        [Fact]
        public async Task FindDuplicatesAsync_Hash_FindsSameContentEvenWithDifferentNameAndDate()
        {
            // Arrange
            await CreateFileAsync("original.txt", "Same content", new DateTime(2020, 1, 1));
            await CreateFileAsync("renamed.txt", "Same content", new DateTime(2025, 1, 1));
            var settings = new CompareSettings { Hash = true };

            // Act
            List<DuplicateGroup> groups = await _finder.FindDuplicatesAsync(_testFolder, settings, FileType.AllFiles);

            // Assert
            Assert.Single(Assert.Single(groups).Duplicates);
        }

        [Fact]
        public async Task FindDuplicatesAsync_SizeAndHash_DoesNotMatchSameSizeDifferentContent()
        {
            // Arrange
            await CreateFileAsync("a.txt", "Hello");
            await CreateFileAsync("b.txt", "World");
            var settings = new CompareSettings { Size = true, DateModified = true, Hash = true };

            // Act
            List<DuplicateGroup> groups = await _finder.FindDuplicatesAsync(_testFolder, settings, FileType.AllFiles);

            // Assert
            Assert.Empty(groups);
        }

        [Fact]
        public async Task FindDuplicatesAsync_NoAttributeSelected_ThrowsArgumentException()
        {
            // Arrange
            var settings = new CompareSettings();

            // Act and Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => _finder.FindDuplicatesAsync(_testFolder, settings, FileType.AllFiles));
        }

        [Fact]
        public async Task FindDuplicatesAsync_CancelledToken_ThrowsOperationCanceledException()
        {
            // Arrange
            await CreateFileAsync("a.txt", "Hello");
            var settings = new CompareSettings { Size = true };
            var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            // Act and Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => _finder.FindDuplicatesAsync(_testFolder, settings, FileType.AllFiles, cancellation.Token));
        }

        //  DeleteDuplicatesAsync 

        [Fact]
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
            Assert.Equal(2, deleted);
            Assert.True(File.Exists(original));
            Assert.False(File.Exists(copy1));
            Assert.False(File.Exists(copy2));
        }
    }
}
