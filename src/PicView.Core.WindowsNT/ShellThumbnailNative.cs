using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace PicView.Core.WindowsNT;

/// <summary>
/// AOT-compatible Windows Shell thumbnail extraction via IShellItemImageFactory.
/// Uses GeneratedComInterface and LibraryImport for source-generated interop.
/// </summary>
[SuppressMessage("ReSharper", "InconsistentNaming")]
public static partial class ShellThumbnailNative
{
    #region COM Interface

    [GeneratedComInterface]
    [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal partial interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage(SIZE size, SIIGBF flags, out IntPtr phbm);
    }

    #endregion

    #region Native Structs and Enums

    [StructLayout(LayoutKind.Sequential)]
    internal struct SIZE
    {
        public int Width;
        public int Height;
    }

    /// <summary>
    /// Flags controlling how the shell thumbnail is retrieved.
    /// </summary>
    [Flags]
    public enum SIIGBF
    {
        ResizeToFit = 0x00000000,
        BiggerSizeOk = 0x00000001,
        MemoryOnly = 0x00000002,
        IconOnly = 0x00000004,
        ThumbnailOnly = 0x00000008,
        InCacheOnly = 0x00000010,
        CropToSize = 0x00000020,
        WideThumbnails = 0x00000040,
        IconBackground = 0x00000080,
        ScaleUp = 0x00000100,
    }

    #endregion

    #region Native Methods

    [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int SHCreateItemFromParsingName(
        string pszPath,
        IntPtr pbc,
        in Guid riid,
        out IntPtr ppv);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeleteObject(IntPtr hObject);

    [LibraryImport("gdi32.dll")]
    private static partial int GetObjectW(IntPtr hObject, int nCount, ref BITMAP lpObject);

    [LibraryImport("gdi32.dll", EntryPoint = "GetObjectW")]
    private static partial int GetDibSection(IntPtr hObject, int nCount, ref DIBSECTION lpObject);

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAP
    {
        public int bmType;
        public int bmWidth;
        public int bmHeight;
        public int bmWidthBytes;
        public ushort bmPlanes;
        public ushort bmBitsPixel;
        public IntPtr bmBits;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
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

    [StructLayout(LayoutKind.Sequential)]
    private struct DIBSECTION
    {
        public BITMAP dsBm;
        public BITMAPINFOHEADER dsBmih;
        public uint dsBitfields0;
        public uint dsBitfields1;
        public uint dsBitfields2;
        public IntPtr dshSection;
        public uint dsOffset;
    }

    #endregion

    #region Public API

    private static readonly ComWrappers s_comWrappers = new StrategyBasedComWrappers();

    /// <summary>
    /// Extracts a Windows Shell thumbnail for the given file path as raw BGRA pixel data.
    /// </summary>
    /// <param name="path">Absolute file path.</param>
    /// <param name="width">Requested thumbnail width.</param>
    /// <param name="height">Requested thumbnail height.</param>
    /// <param name="pixelWidth">Actual width of the returned thumbnail in pixels.</param>
    /// <param name="pixelHeight">Actual height of the returned thumbnail in pixels.</param>
    /// <returns>Raw BGRA pixel data, or null on failure.</returns>
    public static byte[]? GetShellThumbnailBytes(string path, int width, int height,
        out int pixelWidth, out int pixelHeight)
    {
        pixelWidth = 0;
        pixelHeight = 0;

        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        var hBitmap = IntPtr.Zero;
        var shellItemPtr = IntPtr.Zero;

        try
        {
            var factoryGuid = new Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b");
            var hr = SHCreateItemFromParsingName(path, IntPtr.Zero, in factoryGuid, out shellItemPtr);
            if (hr != 0 || shellItemPtr == IntPtr.Zero)
            {
                return null;
            }

            // Manually query the COM interface via the generated marshaller
            var factory = (IShellItemImageFactory)s_comWrappers
                .GetOrCreateObjectForComInstance(shellItemPtr, CreateObjectFlags.None);

            var size = new SIZE { Width = width, Height = height };
            hr = factory.GetImage(size, SIIGBF.ThumbnailOnly | SIIGBF.BiggerSizeOk, out hBitmap);
            if (hr != 0 || hBitmap == IntPtr.Zero)
            {
                return null;
            }

            return ExtractBgraPixels(hBitmap, out pixelWidth, out pixelHeight);
        }
        catch
        {
            return null;
        }
        finally
        {
            if (hBitmap != IntPtr.Zero)
            {
                DeleteObject(hBitmap);
            }

            if (shellItemPtr != IntPtr.Zero)
            {
                Marshal.Release(shellItemPtr);
            }
        }
    }

    #endregion

    #region Private Helpers

    /// <summary>
    /// Reads BGRA pixel data from an HBITMAP.
    /// </summary>
    private static byte[]? ExtractBgraPixels(IntPtr hBitmap, out int pixelWidth, out int pixelHeight)
    {
        pixelWidth = 0;
        pixelHeight = 0;

        var bmp = new BITMAP();
        if (GetObjectW(hBitmap, Marshal.SizeOf<BITMAP>(), ref bmp) == 0)
        {
            return null;
        }

        pixelWidth = bmp.bmWidth;
        pixelHeight = bmp.bmHeight;

        if (bmp.bmBitsPixel != 32 || bmp.bmBits == IntPtr.Zero)
        {
            return null;
        }

        var stride = bmp.bmWidthBytes;
        var totalBytes = stride * bmp.bmHeight;
        var pixels = new byte[totalBytes];

        // The shell usually returns a bottom-up DIB (positive biHeight): its first row in memory
        // is the bottom of the image. BITMAP.bmHeight is always positive, so the orientation has
        // to come from the DIBSECTION header. Copy the rows in reverse to get top-down pixels.
        var dib = new DIBSECTION();
        var isBottomUp = GetDibSection(hBitmap, Marshal.SizeOf<DIBSECTION>(), ref dib) != 0 &&
                         dib.dsBmih.biHeight > 0;

        if (isBottomUp)
        {
            for (var row = 0; row < bmp.bmHeight; row++)
            {
                Marshal.Copy(bmp.bmBits + row * stride, pixels, (bmp.bmHeight - 1 - row) * stride, stride);
            }
        }
        else
        {
            Marshal.Copy(bmp.bmBits, pixels, 0, totalBytes);
        }

        return pixels;
    }

    #endregion
}
