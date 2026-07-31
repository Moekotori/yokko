using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Yokko.Game.Resources;

namespace Yokko.Desktop.Platform;

/// <summary>
/// Uses the Windows shell folder browser so the resource location can be
/// changed without navigating an in-game filesystem control.
/// </summary>
internal sealed class WindowsResourceDirectoryPicker : IResourceDirectoryPicker
{
    private const uint bif_return_only_fs_dirs = 0x0001;
    private const uint bif_edit_box = 0x0010;
    private const uint bif_new_dialog_style = 0x0040;
    private const int bffm_initialized = 1;
    private const uint bffm_set_selection_w = 0x400 + 103;

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
        BrowseCallback callback = (window, message, _, _) =>
        {
            if (message == bffm_initialized
                && !string.IsNullOrWhiteSpace(initialPath)
                && Directory.Exists(initialPath))
            {
                SendMessage(
                    window,
                    bffm_set_selection_w,
                    new IntPtr(1),
                    initialPath);
            }

            return 0;
        };

        var info = new BrowseInfo
        {
            Owner = owner,
            Title = "\u9009\u62e9 Yokko \u8d44\u6e90\u6587\u4ef6\u5939",
            Flags = bif_return_only_fs_dirs | bif_edit_box | bif_new_dialog_style,
            Callback = callback,
        };

        IntPtr itemId = SHBrowseForFolder(ref info);
        if (itemId == IntPtr.Zero)
            return null;

        try
        {
            var path = new StringBuilder(32768);
            return SHGetPathFromIDListEx(
                itemId,
                path,
                (uint)path.Capacity,
                0)
                ? path.ToString()
                : null;
        }
        finally
        {
            Marshal.FreeCoTaskMem(itemId);
            GC.KeepAlive(callback);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct BrowseInfo
    {
        public IntPtr Owner;
        public IntPtr Root;
        public IntPtr DisplayName;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string Title;
        public uint Flags;
        [MarshalAs(UnmanagedType.FunctionPtr)]
        public BrowseCallback Callback;
        public IntPtr CallbackParameter;
        public int Image;
    }

    private delegate int BrowseCallback(
        IntPtr window,
        int message,
        IntPtr parameter,
        IntPtr data);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHBrowseForFolder(ref BrowseInfo info);

    [DllImport("shell32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SHGetPathFromIDListEx(
        IntPtr itemId,
        StringBuilder path,
        uint pathLength,
        uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessage(
        IntPtr window,
        uint message,
        IntPtr wordParameter,
        string longParameter);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
}
