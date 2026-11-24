namespace DuplicateFinderApp
{
    /// <summary>
    /// The kinds of files the user can search for.
    /// </summary>
    public enum FileType
    {
        /// <summary>Every file, whatever the extension.</summary>
        AllFiles,

        /// <summary>Pictures: .jpg and .png files.</summary>
        Pictures,

        /// <summary>PDF documents: .pdf files.</summary>
        Pdf,
    }
}
