namespace DuplicateFinderApp
{
    /// <summary>
    /// Which file attributes to compare when looking for duplicates.
    /// Two files are duplicates when ALL the selected attributes are equal.
    /// </summary>
    public class CompareSettings
    {
        /// <summary>Compare the file name (for example "photo.jpg").</summary>
        public bool Name { get; set; }

        /// <summary>Compare the file size in bytes.</summary>
        public bool Size { get; set; }

        /// <summary>Compare the date and time the file was last changed.</summary>
        public bool DateModified { get; set; }

        /// <summary>Compare the contents of the files using a SHA-256 hash.</summary>
        public bool Hash { get; set; }

        /// <summary>True when at least one attribute is selected.</summary>
        public bool AnySelected => Name || Size || DateModified || Hash;
    }
}
