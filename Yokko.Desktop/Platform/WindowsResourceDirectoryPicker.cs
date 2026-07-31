using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Yokko.Game.Resources;

namespace Yokko.Desktop.Platform;

/// <summary>
/// Uses Windows' Explorer-style file dialog in folder-picking mode.
/// The legacy SHBrowseForFolder tree dialog is intentionally avoided because
/// it is cramped, difficult to navigate and does not provide normal Explorer
/// affordances such as breadcrumbs, search and large folder views.
/// </summary>
internal sealed class WindowsResourceDirectoryPicker : IResourceDirectoryPicker
{
    private const int cancelled_hresult = unchecked((int)0x800704C7);

    public bool IsAvailable => OperatingSystem.IsWindows();

    public Task<string> PickAsync(string initialPath)
    {
        if (!IsAvailable)
            return Task.FromResult<string>(null);

        var completion = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        IntPtr owner = GetForegroundWindow();
        var thread = new Thread(() =>
        {
            try
            {
                completion.TrySetResult(showDialog(owner, initialPath));
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        })
        {
            IsBackground = true,
            Name = "Yokko folder picker",
        };
        if (OperatingSystem.IsWindows())
            thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    private static string showDialog(IntPtr owner, string initialPath)
    {
        IFileDialog dialog = (IFileDialog)new FileOpenDialog();
        IShellItem initialFolder = null;
        IShellItem selectedFolder = null;

        try
        {
            dialog.GetOptions(out FileOpenOptions options);
            dialog.SetOptions(options
                              | FileOpenOptions.PickFolders
                              | FileOpenOptions.ForceFileSystem
                              | FileOpenOptions.PathMustExist
                              | FileOpenOptions.DontAddToRecent);
            dialog.SetTitle("选择文件夹");
            dialog.SetOkButtonLabel("选择此文件夹");

            if (!string.IsNullOrWhiteSpace(initialPath)
                && Directory.Exists(initialPath))
            {
                Guid shellItemId = typeof(IShellItem).GUID;
                int createResult = SHCreateItemFromParsingName(
                    Path.GetFullPath(initialPath),
                    IntPtr.Zero,
                    ref shellItemId,
                    out initialFolder);
                if (createResult >= 0 && initialFolder != null)
                    dialog.SetFolder(initialFolder);
            }

            int result = dialog.Show(owner);
            if (result == cancelled_hresult)
                return null;
            Marshal.ThrowExceptionForHR(result);

            dialog.GetResult(out selectedFolder);
            selectedFolder.GetDisplayName(
                ShellItemDisplayName.FileSystemPath,
                out IntPtr pathPointer);
            try
            {
                return Marshal.PtrToStringUni(pathPointer);
            }
            finally
            {
                Marshal.FreeCoTaskMem(pathPointer);
            }
        }
        finally
        {
            releaseComObject(selectedFolder);
            releaseComObject(initialFolder);
            releaseComObject(dialog);
        }
    }

    private static void releaseComObject(object value)
    {
        if (OperatingSystem.IsWindows()
            && value != null
            && Marshal.IsComObject(value))
        {
            Marshal.FinalReleaseComObject(value);
        }
    }

    [Flags]
    private enum FileOpenOptions : uint
    {
        PickFolders = 0x00000020,
        ForceFileSystem = 0x00000040,
        PathMustExist = 0x00000800,
        DontAddToRecent = 0x02000000,
    }

    private enum ShellItemDisplayName : uint
    {
        FileSystemPath = 0x80058000,
    }

    private enum FileDialogAddPlaceLocation
    {
        Bottom = 0,
        Top = 1,
    }

    [ComImport]
    [Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7")]
    [ClassInterface(ClassInterfaceType.None)]
    private class FileOpenDialog
    {
    }

    [ComImport]
    [Guid("42F85136-DB7E-439C-85F1-E4075D135FC8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileDialog
    {
        [PreserveSig]
        int Show(IntPtr owner);

        void SetFileTypes(uint count, IntPtr filterSpecifications);
        void SetFileTypeIndex(uint index);
        void GetFileTypeIndex(out uint index);
        void Advise(IntPtr events, out uint cookie);
        void Unadvise(uint cookie);
        void SetOptions(FileOpenOptions options);
        void GetOptions(out FileOpenOptions options);
        void SetDefaultFolder(IShellItem folder);
        void SetFolder(IShellItem folder);
        void GetFolder(out IShellItem folder);
        void GetCurrentSelection(out IShellItem item);
        void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);
        void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string name);
        void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);
        void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string text);
        void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
        void GetResult(out IShellItem item);
        void AddPlace(
            IShellItem item,
            FileDialogAddPlaceLocation location);
        void SetDefaultExtension(
            [MarshalAs(UnmanagedType.LPWStr)] string extension);
        void Close(int result);
        void SetClientGuid(ref Guid guid);
        void ClearClientData();
        void SetFilter(IntPtr filter);
    }

    [ComImport]
    [Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        void BindToHandler(
            IntPtr bindContext,
            ref Guid handlerId,
            ref Guid interfaceId,
            out IntPtr result);
        void GetParent(out IShellItem parent);
        void GetDisplayName(
            ShellItemDisplayName displayName,
            out IntPtr name);
        void GetAttributes(uint mask, out uint attributes);
        void Compare(IShellItem other, uint hint, out int order);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(
        [MarshalAs(UnmanagedType.LPWStr)] string path,
        IntPtr bindContext,
        ref Guid interfaceId,
        [MarshalAs(UnmanagedType.Interface)] out IShellItem shellItem);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
}
