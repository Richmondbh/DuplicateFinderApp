using System.IO;

namespace DuplicateFinderApp
{
    /// <summary>
    /// One file that has duplicates, together with all its duplicates.
    /// </summary>
    public class DuplicateGroup
    {
        /// The first occurrence of the file. This is the copy we keep.
        public FileInfo Original { get; }

        ///  The other copies of the same file.
        public List<FileInfo> Duplicates { get; }

        public DuplicateGroup(FileInfo original, List<FileInfo> duplicates)
        {
            Original = original;
            Duplicates = duplicates;
        }
    }
}
