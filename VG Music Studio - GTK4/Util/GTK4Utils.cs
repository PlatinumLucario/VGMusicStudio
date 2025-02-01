using Gtk;
using Kermalis.VGMusicStudio.Core.Properties;
using Kermalis.VGMusicStudio.Core.Util;
using System;
using System.Runtime.InteropServices;

namespace Kermalis.VGMusicStudio.GTK4.Util;

internal class GTK4Utils : DialogUtils
{
    // Callback
    private static Gio.Internal.AsyncReadyCallback? _saveCallback { get; set; }
    private static Gio.Internal.AsyncReadyCallback? _openCallback { get; set; }
    private static Gio.Internal.AsyncReadyCallback? _selectFolderCallback { get; set; }


    public static event Action<string>? OnPathChanged;

    private static void Convert(string filterName, Span<string> fileExtensions, FileFilter fileFilter)
    {
        if (fileExtensions.IsEmpty | filterName.Contains('|'))
        {
            for (int i = 0; i < filterName.Length; i++)
            {
                _ = new string[filterName.Split('|').Length];
                Span<string> fn = filterName.Split('|');
                fileFilter.SetName(fn[0]);
                if (fn[1].Contains(';'))
                {
                    _ = new string[fn[1].Split(';').Length];
                    Span<string> fe = fn[1].Split(';');
                    for (int k = 0; k < fe.Length; k++)
                    {
                        //fe[k] = fe[k].Trim('*', '.');
                        fileFilter.AddPattern(fe[k]);
                    }
                }
                else
                {
                    fileFilter.AddPattern(fn[1]);
                }
            }
        }
        else
        {
            fileFilter.SetName(filterName);
            for (int i = 0; i < fileExtensions.Length; i++)
            {
                fileFilter.AddPattern(fileExtensions[i]);
            }
        }
    }
    public static string CreateLoadDialog(string title, object parent = null!) =>
        new GTK4Utils().CreateLoadDialog(title, "", [""], false, false, parent);
    public static string CreateLoadDialog(string extension, string title, string filter, object parent = null!) =>
        new GTK4Utils().CreateLoadDialog(title, filter, [extension], true, true, parent);
    public static string CreateLoadDialog(Span<string> extensions, string title, string filter, bool allowAllFiles = true, object parent = null!) =>
        new GTK4Utils().CreateLoadDialog(title, filter, extensions, true, allowAllFiles, parent);
    public override string CreateLoadDialog(string title, string filterName = "", string fileExtension = "", bool isFile = false, bool allowAllFiles = false, object? parent = null) =>
        CreateLoadDialog(title, filterName, [fileExtension], isFile, allowAllFiles, parent!);
    public override string CreateLoadDialog(string title, string filterName, Span<string> fileExtensions, bool isFile, bool allowAllFiles, object? parent)
    {
        parent ??= MainWindow.Instance;
        string? path = null;
        if (isFile)
        {
            var ff = FileFilter.New();
            Convert(filterName, fileExtensions, ff);

            var filters = Gio.ListStore.New(FileFilter.GetGType());
            filters.Append(ff);
            var allFiles = FileFilter.New();
            allFiles.SetName(Strings.FilterAllFiles);
            allFiles.AddPattern("*.*");
            if (allowAllFiles)
            {
                filters.Append(allFiles);
            }

            if (Functions.GetMinorVersion() <= 8)
            {
                var d = FileChooserNative.New(
                    title,
                    (Window)parent!,
                    FileChooserAction.Open,
                    "Open",
                    "Cancel");


                d.AddFilter(ff);
                d.AddFilter(allFiles);

                d.OnResponse += OpenResponse;
                d.Show();
                return null!;

                void OpenResponse(NativeDialog sender, NativeDialog.ResponseSignalArgs e)
                {
                    if (e.ResponseId != (int)ResponseType.Accept)
                    {
                        d.Unref();
                        return;
                    }
                    var path = d.GetFile()!.GetPath() ?? "";
                    OnPathChanged!.Invoke(path!);
                    d.Unref();
                }
            }
            else
            {
                var d = FileDialog.New();
                d.SetTitle(title);
                d.SetFilters(filters);
                _openCallback += OpenCallback;

                // SelectFolder, Open and Save methods are currently missing from GirCore, but are available in the Gtk.Internal namespace,
                // so we're using this until GirCore updates with the method bindings. See here: https://github.com/gircore/gir.core/issues/900
                var p = (Window)parent!;
                Gtk.Internal.FileDialog.Open(d.Handle, p.Handle, IntPtr.Zero, _openCallback, IntPtr.Zero);
                //d.Open(Handle, IntPtr.Zero, _openCallback, IntPtr.Zero);
                return path!;

                void OpenCallback(nint sourceObject, nint res, nint data)
                {
                    var errorHandle = new GLib.Internal.ErrorOwnedHandle(IntPtr.Zero);
                    var fileHandle = Gtk.Internal.FileDialog.OpenFinish(d.Handle, res, out errorHandle);
                    if (fileHandle != IntPtr.Zero)
                    {
                        path = Marshal.PtrToStringUTF8(Gio.Internal.File.GetPath(fileHandle).DangerousGetHandle());
                        OnPathChanged!.Invoke(path!);
                    }
                    d.Unref();
                }
            }
        }
        else
        {
            if (Functions.GetMinorVersion() <= 8)
            {
                // To allow the dialog to display in native windowing format, FileChooserNative is used instead of FileChooserDialog
                var d = FileChooserNative.New(
                    title, // The title shown in the folder select dialog window
                    (Window)parent!, // The parent of the dialog window, is the MainWindow itself
                    FileChooserAction.SelectFolder, // To ensure it becomes a folder select dialog window, SelectFolder is used as the FileChooserAction
                    "Select Folder", // Followed by the accept
                    "Cancel");       // and cancel button names.

                d.SetModal(true);

                // Note: Blocking APIs were removed in GTK4, which means the code will proceed to run and return to the main loop, even when a dialog is displayed.
                // Instead, it's handled by the OnResponse event function when it re-enters upon selection.
                d.OnResponse += SelectFolderResponse;
                d.Show();
                return path!;

                void SelectFolderResponse(NativeDialog sender, NativeDialog.ResponseSignalArgs e)
                {
                    if (e.ResponseId != (int)ResponseType.Accept) // In GTK4, the 'Gtk.FileChooserNative.Action' property is used for determining the button selection on the dialog. The 'Gtk.Dialog.Run' method was removed in GTK4, due to it being a non-GUI function and going against GTK's main objectives.
                    {
                        d.Unref();
                        return;
                    }
                    var path = d.GetCurrentFolder()!.GetPath() ?? "";
                    d.GetData(path);
                    OnPathChanged!.Invoke(path!);
                    d.Unref(); // Ensures disposal of the dialog when closed
                }
            }
            else
            {
                var d = FileDialog.New();
                d.SetTitle(title);

                _selectFolderCallback += SelectFolderCallback;
                var p = (Window)parent!;
                Gtk.Internal.FileDialog.SelectFolder(d.Handle, p.Handle, IntPtr.Zero, _selectFolderCallback, IntPtr.Zero);
                return path!;

                void SelectFolderCallback(nint sourceObject, nint res, nint data)
                {
                    var errorHandle = new GLib.Internal.ErrorOwnedHandle(IntPtr.Zero);
                    var folderHandle = Gtk.Internal.FileDialog.SelectFolderFinish(d.Handle, res, out errorHandle);
                    if (folderHandle != IntPtr.Zero)
                    {
                        var path = Marshal.PtrToStringUTF8(Gio.Internal.File.GetPath(folderHandle).DangerousGetHandle());
                        OnPathChanged!.Invoke(path!);
                    }
                    d.Unref();
                }
            }
        }
    }

    public static string CreateSaveDialog(string fileName, string extension, string title, string filter, bool allowAllFiles = false, object parent = null!) =>
        new GTK4Utils().CreateSaveDialog(fileName, title, filter, [extension], true, allowAllFiles, parent);
    public static string CreateSaveDialog(string fileName, Span<string> extensions, string title, string filter, bool allowAllFiles = false, object parent = null!) =>
        new GTK4Utils().CreateSaveDialog(fileName, title, filter, extensions, true, allowAllFiles, parent);
    public override string CreateSaveDialog(string fileName, string title, string filterName, string fileExtension = "", bool isFile = false, bool allowAllFiles = false, object? parent = null) =>
        CreateSaveDialog(fileName, title, filterName, [fileExtension], false);
    public override string CreateSaveDialog(string fileName, string title, string filterName, Span<string> fileExtensions, bool isFile = false, bool allowAllFiles = false, object? parent = null)
    {
        var ff = FileFilter.New();
        Convert(filterName, fileExtensions, ff);

        var filters = Gio.ListStore.New(FileFilter.GetGType());
        filters.Append(ff);
        var allFiles = FileFilter.New();
        allFiles.SetName(Strings.FilterAllFiles);
        allFiles.AddPattern("*.*");
        if (allowAllFiles)
        {
            filters.Append(allFiles);
        }

        if (Functions.GetMinorVersion() <= 8)
        {
            var d = FileChooserNative.New(
                title,
                (Window)parent!,
                FileChooserAction.Save,
                "Save",
                "Cancel");
            d.SetCurrentName(fileName);
            d.AddFilter(ff);

            d.OnResponse += SaveResponse;
            d.Show();
            return null!;

            void SaveResponse(NativeDialog sender, NativeDialog.ResponseSignalArgs e)
            {
                if (e.ResponseId != (int)ResponseType.Accept)
                {
                    d.Unref();
                    return;
                }

                var path = d.GetFile()!.GetPath() ?? "";
                OnPathChanged!.Invoke(path!);
                d.Unref();
            }
        }
        else
        {
            var d = FileDialog.New();
            d.SetTitle(title);
            d.SetInitialName(fileName);
            d.SetFilters(filters);
            string? path = null;
            _saveCallback = SaveCallback;
            var p = (Window)parent!;
            Gtk.Internal.FileDialog.Save(d.Handle, p.Handle, IntPtr.Zero, _saveCallback, IntPtr.Zero);
            //d.Open(Handle, IntPtr.Zero, _openCallback, IntPtr.Zero);
            return path!;

            void SaveCallback(nint sourceObject, nint res, nint data)
            {
                var errorHandle = new GLib.Internal.ErrorOwnedHandle(IntPtr.Zero);
                var fileHandle = Gtk.Internal.FileDialog.SaveFinish(d.Handle, res, out errorHandle);
                if (fileHandle != IntPtr.Zero)
                {
                    path = Marshal.PtrToStringUTF8(Gio.Internal.File.GetPath(fileHandle).DangerousGetHandle());
                    OnPathChanged!.Invoke(path!);
                }
                d.Unref();
            }
        }
    }
}
