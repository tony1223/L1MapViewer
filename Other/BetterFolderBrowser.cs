using System;
using System.ComponentModel;
using Eto.Forms;
using Eto.Drawing;

namespace L1MapViewer.Other
{
    /// <summary>
    /// Cross-platform folder browser dialog wrapper.
    /// Replaces the Windows-specific BetterFolderBrowser with Eto.Forms SelectFolderDialog.
    /// </summary>
    [Description("A cross-platform folder browser dialog.")]
    public class BetterFolderBrowser : IDisposable
    {
        private readonly SelectFolderDialog _dialog = new SelectFolderDialog();

        public BetterFolderBrowser()
        {
            SetDefaults();
        }

        public BetterFolderBrowser(IContainer container)
        {
            SetDefaults();
        }

        private void SetDefaults()
        {
            Title = "Select a folder";
        }

        /// <summary>
        /// Gets or sets the dialog title.
        /// </summary>
        public string Title
        {
            get => _dialog.Title;
            set => _dialog.Title = value;
        }

        /// <summary>
        /// Gets or sets the root folder.
        /// </summary>
        public string RootFolder
        {
            get => _dialog.Directory;
            set => _dialog.Directory = value;
        }

        /// <summary>
        /// Gets the selected folder path.
        /// </summary>
        public string SelectedPath => _dialog.Directory;

        /// <summary>
        /// Gets the selected folder (same as SelectedPath for compatibility).
        /// </summary>
        public string SelectedFolder => _dialog.Directory;

        /// <summary>
        /// Gets or sets whether multiple selection is allowed.
        /// Note: Eto's SelectFolderDialog doesn't support multiple selection,
        /// this property is kept for API compatibility.
        /// </summary>
        public bool Multiselect { get; set; } = false;

        /// <summary>
        /// Gets the selected folders (returns single item for compatibility).
        /// </summary>
        public string[] SelectedFolders => string.IsNullOrEmpty(_dialog.Directory)
            ? Array.Empty<string>()
            : new[] { _dialog.Directory };

        /// <summary>
        /// Shows the dialog and returns the result.
        /// </summary>
        public DialogResult ShowDialog()
        {
            return _dialog.ShowDialog(null);
        }

        /// <summary>
        /// Shows the dialog with a parent window.
        /// </summary>
        public DialogResult ShowDialog(Window owner)
        {
            return _dialog.ShowDialog(owner);
        }

        /// <summary>
        /// Shows the dialog with a parent control.
        /// </summary>
        public DialogResult ShowDialog(Eto.Forms.Control owner = null)
        {
            var window = owner?.ParentWindow;
            return _dialog.ShowDialog(window);
        }

        /// <summary>
        /// Resets the dialog to default values.
        /// </summary>
        public void Reset()
        {
            _dialog.Directory = string.Empty;
            SetDefaults();
        }

        public void Dispose()
        {
            // Eto dialogs don't need explicit disposal
        }
    }
}
