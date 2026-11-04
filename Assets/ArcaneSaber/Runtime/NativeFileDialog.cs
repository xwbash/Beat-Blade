using System;
using System.Runtime.InteropServices;

namespace ArcaneSaber
{
    // Unity has no runtime file picker; this is the plain Win32 dialog, in editor and player.
    public static class NativeFileDialog
    {
        public const string AudioFilter="Ses dosyası (mp3, wav, ogg)\0*.mp3;*.wav;*.ogg\0\0";
        public const string MapFilter="Arcane Saber modu (.arcanemap)\0*.arcanemap\0\0";
        public const string ImportFilter="Arcane Saber modu veya BeatSaver haritası (.arcanemap, .zip)\0*.arcanemap;*.zip\0\0";
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        public static bool Supported => true;
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
        struct OpenFileName
        {
            public int lStructSize; public IntPtr hwndOwner,hInstance;
            public string lpstrFilter,lpstrCustomFilter; public int nMaxCustFilter,nFilterIndex;
            public IntPtr lpstrFile; public int nMaxFile; public IntPtr lpstrFileTitle; public int nMaxFileTitle;
            public string lpstrInitialDir,lpstrTitle; public int Flags; public short nFileOffset,nFileExtension;
            public string lpstrDefExt; public IntPtr lCustData,lpfnHook; public string lpTemplateName;
            public IntPtr pvReserved; public int dwReserved,FlagsEx;
        }
        [DllImport("comdlg32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool GetOpenFileNameW(ref OpenFileName dialog);
        [DllImport("comdlg32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool GetSaveFileNameW(ref OpenFileName dialog);
        [DllImport("user32.dll")] static extern IntPtr GetActiveWindow();
        public static int StructSize => Marshal.SizeOf(typeof(OpenFileName));
        const int Capacity=4096;
        // OFN_NOCHANGEDIR keeps the player's working directory stable.
        const int Explorer=0x00080000,NoChangeDir=0x00000008,FileMustExist=0x00001000,PathMustExist=0x00000800,OverwritePrompt=0x00000002;
        static string Show(string title,string filter,string extension,string initialName,bool save)
        {
            IntPtr buffer=Marshal.AllocHGlobal(Capacity*2);
            try
            {
                var initial=new char[Capacity]; if(!string.IsNullOrEmpty(initialName)) initialName.CopyTo(0,initial,0,Math.Min(initialName.Length,Capacity-1));
                Marshal.Copy(initial,0,buffer,Capacity);
                var dialog=new OpenFileName
                {
                    hwndOwner=GetActiveWindow(),lpstrFilter=filter,nFilterIndex=1,lpstrFile=buffer,nMaxFile=Capacity,
                    lpstrTitle=title,lpstrDefExt=extension,Flags=Explorer|NoChangeDir|PathMustExist|(save?OverwritePrompt:FileMustExist)
                };
                dialog.lStructSize=Marshal.SizeOf(dialog);
                return (save?GetSaveFileNameW(ref dialog):GetOpenFileNameW(ref dialog))?Marshal.PtrToStringUni(buffer):null;
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }
        public static string Open(string title,string filter) => Show(title,filter,null,null,false);
        public static string Save(string title,string filter,string extension,string initialName) => Show(title,filter,extension,initialName,true);
#else
        public static bool Supported => false;
        public static string Open(string title,string filter) => null;
        public static string Save(string title,string filter,string extension,string initialName) => null;
#endif
    }
}
