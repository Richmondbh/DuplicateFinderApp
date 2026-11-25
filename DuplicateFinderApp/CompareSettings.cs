namespace DuplicateFinderApp
{
    /// <summary>
    /// This is about  Which file attributes to compare when looking for duplicates.
    /// Two files are duplicates when ALL the selected attributes are equal.
    /// </summary>
    public class CompareSettings
    {
        /// Compare the file name (for example "photo.jpg").
        public bool Name { get; set; }

        /// Compare the file size in bytes.
        public bool Size { get; set; }

        /// Compare the date and time the file was last changed.
        public bool DateModified { get; set; }

        /// Compare the contents of the files using a SHA-256 hash.
        public bool Hash { get; set; }

        /// True when at least one attribute is selected.
        public bool AnySelected => Name || Size || DateModified || Hash;
    }
}
