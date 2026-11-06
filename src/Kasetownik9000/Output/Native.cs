using System.Runtime.InteropServices;

namespace Kasetownik9000.Output;

/// <summary>Minimalny zestaw Win32 (GDI + okno drukowania) — przez LibraryImport, więc działa z Native AOT.</summary>
internal static unsafe partial class Native
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct PRINTDLGW
    {
        public uint lStructSize;
        public nint hwndOwner;
        public nint hDevMode;
        public nint hDevNames;
        public nint hDC;
        public uint Flags;
        public ushort nFromPage;
        public ushort nToPage;
        public ushort nMinPage;
        public ushort nMaxPage;
        public ushort nCopies;
        public nint hInstance;
        public nint lCustData;
        public nint lpfnPrintHook;
        public nint lpfnSetupHook;
        public nint lpPrintTemplateName;
        public nint lpSetupTemplateName;
        public nint hPrintTemplate;
        public nint hSetupTemplate;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DOCINFOW
    {
        public int cbSize;
        public nint lpszDocName;
        public nint lpszOutput;
        public nint lpszDatatype;
        public uint fwType;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    public const uint PD_NOSELECTION = 0x4;
    public const uint PD_NOPAGENUMS = 0x8;
    public const uint PD_RETURNDC = 0x100;
    public const uint PD_USEDEVMODECOPIESANDCOLLATE = 0x40000;
    public const uint PD_HIDEPRINTTOFILE = 0x100000;

    public const int HORZRES = 8, VERTRES = 10, LOGPIXELSX = 88, LOGPIXELSY = 90;
    public const int PHYSICALWIDTH = 110, PHYSICALHEIGHT = 111, PHYSICALOFFSETX = 112, PHYSICALOFFSETY = 113;
    public const int HALFTONE = 4;
    public const uint DIB_RGB_COLORS = 0, SRCCOPY = 0x00CC0020;
    public const int PS_SOLID = 0;

    [LibraryImport("comdlg32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool PrintDlgW(ref PRINTDLGW lppd);

    [LibraryImport("comdlg32.dll")]
    internal static partial uint CommDlgExtendedError();

    [LibraryImport("gdi32.dll", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint CreateDCW(string? driver, string device, string? output, nint devmode);

    [LibraryImport("gdi32.dll")]
    internal static partial int StartDocW(nint hdc, ref DOCINFOW lpdi);

    [LibraryImport("gdi32.dll")]
    internal static partial int StartPage(nint hdc);

    [LibraryImport("gdi32.dll")]
    internal static partial int EndPage(nint hdc);

    [LibraryImport("gdi32.dll")]
    internal static partial int EndDoc(nint hdc);

    [LibraryImport("gdi32.dll")]
    internal static partial int AbortDoc(nint hdc);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DeleteDC(nint hdc);

    [LibraryImport("gdi32.dll")]
    internal static partial int GetDeviceCaps(nint hdc, int index);

    [LibraryImport("gdi32.dll")]
    internal static partial int SetStretchBltMode(nint hdc, int mode);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetBrushOrgEx(nint hdc, int x, int y, nint lppt);

    [LibraryImport("gdi32.dll")]
    internal static partial int StretchDIBits(nint hdc, int xDest, int yDest, int destWidth, int destHeight,
        int xSrc, int ySrc, int srcWidth, int srcHeight, void* lpBits, BITMAPINFOHEADER* lpbmi, uint iUsage, uint rop);

    [LibraryImport("gdi32.dll")]
    internal static partial nint CreatePen(int style, int width, uint color);

    [LibraryImport("gdi32.dll")]
    internal static partial nint SelectObject(nint hdc, nint h);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DeleteObject(nint h);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool MoveToEx(nint hdc, int x, int y, nint lppt);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool LineTo(nint hdc, int x, int y);

    [LibraryImport("kernel32.dll")]
    internal static partial nint GlobalFree(nint hMem);

    [LibraryImport("kernel32.dll")]
    internal static partial nint GlobalLock(nint hMem);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GlobalUnlock(nint hMem);

    // ---- druk dwustronny (DEVMODEW.dmFields/dmDuplex) ----
    public const uint DM_DUPLEX = 0x1000;
    public const short DMDUP_SIMPLEX = 1, DMDUP_VERTICAL = 2, DMDUP_HORIZONTAL = 3;
    public const int DevModeFieldsOffset = 72, DevModeDuplexOffset = 94;
    public const ushort DC_DUPLEX = 7;

    [LibraryImport("gdi32.dll")]
    internal static partial nint ResetDCW(nint hdc, nint devmode);

    [LibraryImport("winspool.drv", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial int DeviceCapabilitiesW(string device, string? port, ushort capability, nint output, nint devmode);

    [LibraryImport("user32.dll")]
    internal static partial uint GetDpiForWindow(nint hwnd);

    public const int SHCNE_ASSOCCHANGED = 0x08000000;

    [LibraryImport("shell32.dll")]
    internal static partial void SHChangeNotify(int eventId, uint flags, nint item1, nint item2);

    public const uint MB_OKCANCEL = 0x1, MB_ICONINFORMATION = 0x40;
    public const int IDOK = 1;

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial int MessageBoxW(nint hwnd, string text, string caption, uint type);

    public static uint Rgb(byte r, byte g, byte b) => (uint)(r | (g << 8) | (b << 16));
}
