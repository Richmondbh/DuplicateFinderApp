namespace DuplicateFinderApp
{
    /// <summary>
    /// The kinds of files the user can search for.
    /// </summary>
    public enum FileType
    {
        /// Every file, whatever the extension.
        AllFiles,

        /// Pictures: .jpg and .png files.
        Pictures,

        /// PDF documents: .pdf files.<
        Pdf,

        /// Text files: .txt files.
        Text
    }
}
