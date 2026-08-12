using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace KoreanSubtitleStudio.Services
{
    internal sealed class ExplorerFolderPicker
    {
        public string Title { get; set; }
        public string InitialFolder { get; set; }

        public string ShowDialog(Window owner)
        {
            var dialog = (IFileOpenDialog)new FileOpenDialog();
            try
            {
                dialog.SetOptions(Fos.PickFolders | Fos.ForceFileSystem | Fos.PathMustExist);
                dialog.SetTitle(string.IsNullOrWhiteSpace(Title) ? "폴더 선택" : Title);
                dialog.SetOkButtonLabel("이 폴더 선택");

                IShellItem initialItem = null;
                try
                {
                    if (!string.IsNullOrWhiteSpace(InitialFolder))
                    {
                        var id = typeof(IShellItem).GUID;
                        if (SHCreateItemFromParsingName(InitialFolder, IntPtr.Zero, ref id, out initialItem) == 0)
                            dialog.SetFolder(initialItem);
                    }
                }
                finally { if (initialItem != null) Marshal.ReleaseComObject(initialItem); }

                var handle = owner == null ? IntPtr.Zero : new WindowInteropHelper(owner).Handle;
                var result = dialog.Show(handle);
                if (result == unchecked((int)0x800704C7)) return null;
                Marshal.ThrowExceptionForHR(result);

                IShellItem selected;
                dialog.GetResult(out selected);
                try
                {
                    IntPtr pathPointer;
                    selected.GetDisplayName(Sigdn.FileSystemPath, out pathPointer);
                    try { return Marshal.PtrToStringUni(pathPointer); }
                    finally { Marshal.FreeCoTaskMem(pathPointer); }
                }
                finally { if (selected != null) Marshal.ReleaseComObject(selected); }
            }
            finally { Marshal.ReleaseComObject(dialog); }
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        private static extern int SHCreateItemFromParsingName(string path, IntPtr bindContext, ref Guid riid, out IShellItem shellItem);

        [ComImport, Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7")]
        private class FileOpenDialog { }

        [ComImport, Guid("D57C7288-D4AD-4768-BE02-9D969532D960"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IFileOpenDialog
        {
            [PreserveSig] int Show(IntPtr parent);
            void SetFileTypes(uint count, IntPtr filterSpec);
            void SetFileTypeIndex(uint index);
            void GetFileTypeIndex(out uint index);
            void Advise(IntPtr events, out uint cookie);
            void Unadvise(uint cookie);
            void SetOptions(Fos options);
            void GetOptions(out Fos options);
            void SetDefaultFolder(IShellItem shellItem);
            void SetFolder(IShellItem shellItem);
            void GetFolder(out IShellItem shellItem);
            void GetCurrentSelection(out IShellItem shellItem);
            void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);
            void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string name);
            void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);
            void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string text);
            void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
            void GetResult(out IShellItem shellItem);
            void AddPlace(IShellItem shellItem, int alignment);
            void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string extension);
            void Close(int error);
            void SetClientGuid(ref Guid guid);
            void ClearClientData();
            void SetFilter(IntPtr filter);
            void GetResults(out IntPtr items);
            void GetSelectedItems(out IntPtr items);
        }

        [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem
        {
            void BindToHandler(IntPtr bindContext, ref Guid handlerId, ref Guid interfaceId, out IntPtr result);
            void GetParent(out IShellItem parent);
            void GetDisplayName(Sigdn displayName, out IntPtr name);
            void GetAttributes(uint mask, out uint attributes);
            void Compare(IShellItem other, uint hint, out int order);
        }

        [Flags]
        private enum Fos : uint
        {
            PickFolders = 0x20,
            ForceFileSystem = 0x40,
            PathMustExist = 0x800
        }

        private enum Sigdn : uint { FileSystemPath = 0x80058000 }
    }
}
