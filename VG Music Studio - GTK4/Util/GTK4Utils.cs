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
    public static string CreateLoadDialog(Span<string> extensions, string title, string filter, object parent = null!) =>
        new GTK4Utils().CreateLoadDialog(title, filter, extensions, true, true, parent);
    public override string CreateLoadDialog(string title, string filterName = "", string fileExtension = "", bool isFile = false, bool allowAllFiles = false, object? parent = null) =>
        CreateLoadDialog(title, filterName, [fileExtension], isFile, allowAllFiles, parent!);
    public override string CreateLoadDialog(string title, string filterName, Span<string> fileExtensions, bool isFile, bool allowAllFiles, object? parent)
    {
        if (isFile)
        {

            var ff = FileFilter.New();
            Convert(filterName, fileExtensions, ff);

            var d = FileDialog.New();
            d.SetTitle(title);
            var filters = Gio.ListStore.New(FileFilter.GetGType());
            filters.Append(ff);
            if (allowAllFiles)
            {
                var allFiles = FileFilter.New();
                allFiles.SetName(Strings.FilterAllFiles);
                allFiles.AddPattern("*.*");
                filters.Append(allFiles);
            }
            d.SetFilters(filters);
            string? path = null;
            _openCallback = (source, res, data) =>
            {
                var errorHandle = new GLib.Internal.ErrorOwnedHandle(IntPtr.Zero);
                var fileHandle = Gtk.Internal.FileDialog.OpenFinish(d.Handle, res, out errorHandle);
                if (fileHandle != IntPtr.Zero)
                {
                    path = Marshal.PtrToStringUTF8(Gio.Internal.File.GetPath(fileHandle).DangerousGetHandle());
                }
                d.Unref();
            };
            if (path != null)
            {
                d.Unref();
                return path;
            }
            if (parent == Adw.Window.New())
            {
                var p = (Adw.Window)parent;
                // SelectFolder, Open and Save methods are currently missing from GirCore, but are available in the Gtk.Internal namespace,
                // so we're using this until GirCore updates with the method bindings. See here: https://github.com/gircore/gir.core/issues/900
                Gtk.Internal.FileDialog.Open(d.Handle, p.Handle, IntPtr.Zero, _openCallback, IntPtr.Zero);
            }
            else if (parent == Gtk.Window.New())
            {
                var p = (Gtk.Window)parent;
                Gtk.Internal.FileDialog.Open(d.Handle, p.Handle, IntPtr.Zero, _openCallback, IntPtr.Zero);
            }
            else
            {
                var p = Gtk.Window.New();
                Gtk.Internal.FileDialog.Open(d.Handle, p.Handle, IntPtr.Zero, _openCallback, IntPtr.Zero);
            }
            //d.Open(Handle, IntPtr.Zero, _openCallback, IntPtr.Zero);
            return null!;
        }
        else
        {
            var d = FileDialog.New();
            d.SetTitle(title);

            string? path = null;
            _selectFolderCallback = (source, res, data) =>
            {
                var errorHandle = new GLib.Internal.ErrorOwnedHandle(IntPtr.Zero);
                var folderHandle = Gtk.Internal.FileDialog.SelectFolderFinish(d.Handle, res, out errorHandle);
                if (folderHandle != IntPtr.Zero)
                {
                    var path = Marshal.PtrToStringUTF8(Gio.Internal.File.GetPath(folderHandle).DangerousGetHandle());
                }
                d.Unref();
            };
            if (path != null)
            {
                d.Unref();
                return path;
            }
            if (parent == Adw.Window.New())
            {
                var p = (Adw.Window)parent;
                Gtk.Internal.FileDialog.SelectFolder(d.Handle, p.Handle, IntPtr.Zero, _selectFolderCallback, IntPtr.Zero);
            }
            else if (parent == Gtk.Window.New())
            {
                var p = (Gtk.Window)parent;
                Gtk.Internal.FileDialog.SelectFolder(d.Handle, p.Handle, IntPtr.Zero, _selectFolderCallback, IntPtr.Zero);
            }
            else
            {
                var p = Gtk.Window.New();
                Gtk.Internal.FileDialog.SelectFolder(d.Handle, p.Handle, IntPtr.Zero, _selectFolderCallback, IntPtr.Zero);
            }
            return null!;
        }
    }

    public static string CreateSaveDialog(string fileName, string extension, string title, string filter, object parent = null!) =>
        new GTK4Utils().CreateSaveDialog(fileName, title, filter, [extension], false, false, parent);
    public static string CreateSaveDialog(string fileName, Span<string> extensions, string title, string filter, object parent = null!) =>
        new GTK4Utils().CreateSaveDialog(fileName, title, filter, extensions, false, false, parent);
    public override string CreateSaveDialog(string fileName, string title, string filterName, string fileExtension = "", bool isFile = false, bool allowAllFiles = false, object? parent = null) =>
        CreateSaveDialog(fileName, title, filterName, [fileExtension], false);
    public override string CreateSaveDialog(string fileName, string title, string filterName, Span<string> fileExtensions, bool isFile = false, bool allowAllFiles = false, object? parent = null)
    {
        var ff = FileFilter.New();
        Convert(filterName, fileExtensions, ff);

        var d = FileDialog.New();
        d.SetTitle(title);
        d.SetInitialName(fileName);
        var filters = Gio.ListStore.New(FileFilter.GetGType());
        filters.Append(ff);
        if (allowAllFiles)
        {
            var allFiles = FileFilter.New();
            allFiles.SetName(Strings.FilterAllFiles);
            allFiles.AddPattern("*.*");
            filters.Append(allFiles);
        }
        d.SetFilters(filters);
        string? path = null;
        _saveCallback = (source, res, data) =>
        {
            var errorHandle = new GLib.Internal.ErrorOwnedHandle(IntPtr.Zero);
            var fileHandle = Gtk.Internal.FileDialog.SaveFinish(d.Handle, res, out errorHandle);
            if (fileHandle != IntPtr.Zero)
            {
                path = Marshal.PtrToStringUTF8(Gio.Internal.File.GetPath(fileHandle).DangerousGetHandle());
            }
            d.Unref();
        };
        if (path != null)
        {
            d.Unref();
            return path;
        }
        if (parent == Adw.Window.New())
        {
            var p = (Adw.Window)parent;
            Gtk.Internal.FileDialog.Save(d.Handle, p.Handle, IntPtr.Zero, _saveCallback, IntPtr.Zero);
        }
        else if (parent == Gtk.Window.New())
        {
            var p = (Gtk.Window)parent;
            Gtk.Internal.FileDialog.Save(d.Handle, p.Handle, IntPtr.Zero, _saveCallback, IntPtr.Zero);
        }
        else
        {
            var p = Gtk.Window.New();
            Gtk.Internal.FileDialog.Save(d.Handle, p.Handle, IntPtr.Zero, _saveCallback, IntPtr.Zero);
        }
        //d.Open(Handle, IntPtr.Zero, _openCallback, IntPtr.Zero);
        return null!;
    }
}
