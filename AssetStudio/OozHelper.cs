using System;
using System.IO;
using System.Runtime.InteropServices;
using AssetStudio.PInvoke;

namespace AssetStudio;
public static class OozHelper
{
    static OozHelper()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            DllLoader.PreloadDll("ooz");
        }
    }

    [DllImport(@"ooz")]
    static extern int Ooz_Decompress(ref byte compressedBuffer, int compressedBufferSize, ref byte decompressedBuffer, int decompressedBufferSize, int fuzzSafe, int checkCRC, int verbosity, IntPtr rawBuffer, int rawBufferSize, IntPtr fpCallback, IntPtr callbackUserData, IntPtr decoderMemory, IntPtr decoderMemorySize, int threadPhase);

    public static int Decompress(Span<byte> compressed, Span<byte> decompressed)
    {
        int numWrite = -1;
        try
        {
            numWrite = Ooz_Decompress(ref compressed[0], compressed.Length, ref decompressed[0], decompressed.Length, 1, 0, 0, 0, 0, 0, 0, 0, 0, 3);
        }
        catch (Exception e)
        {
            throw new IOException($"Oodle decompression error, write {numWrite} bytes but expected {decompressed.Length} bytes", e);
        }

        return numWrite;
    }
}