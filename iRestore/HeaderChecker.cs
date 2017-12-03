using iTired;
using System;
using System.Linq;
namespace iRestore
{
    public class HeaderChecker
    {
        public enum headerFooter
        {
            Invalid,
            JPGHeader,
            JPGFooter,
            GIFHeader,
            GIFFooter,
            PNGHeader,
            PNGFooter,
            BMPHeader
        }

        public static string getExtension(int i)
        {
            string s = string.Empty;
            switch (i)
            {
                case (int)headerFooter.JPGHeader:
                case (int)headerFooter.JPGFooter:
                    s = "jpg";
                    break;
                case (int)headerFooter.GIFHeader:
                case (int)headerFooter.GIFFooter:
                    s = "gif";
                    break;
                case (int)headerFooter.PNGHeader:
                case (int)headerFooter.PNGFooter:
                    s = "png";
                    break;
                case (int)headerFooter.BMPHeader:
                    s = "bmp";
                    break;
            }
            return s;
        }

        private readonly static byte[]
            JPGHeader = { 0xFF, 0xD8 }, // actually: 0xFF, 0xD8, 0xFF, 0xE0 (I got lazy)
            JPGFooter = { 0xFF, 0xD9 },
            GIFHeader = { 0x47, 0x49 },
            GIFFooter = { 0x00, 0x3B },
            PNGHeader = { 0x89, 0x50 },
            PNGFooter = { 0x60, 0x82 },
            BMPHeader = { 0x42, 0x4D };

        private const int clusterSize_offset = 0x02;
        public static int checkClusterForFooterAndHeader(byte[] cluster, ref uint fileSize, ref uint lastSectorSize)
        {
            fileSize = lastSectorSize = Constants.NULLED;
            int enumVar = (int)headerFooter.Invalid;
            if (!(cluster.All(singlByte => singlByte == 0)))
            {
                byte[] array = new byte[JPGHeader.Length];
                Array.ConstrainedCopy(cluster, 0, array, 0, JPGHeader.Length);
                if (array.SequenceEqual(JPGHeader))
                    enumVar = (int)headerFooter.JPGHeader;
                else if (array.SequenceEqual(GIFHeader))
                    enumVar = (int)headerFooter.GIFHeader;
                else if (array.SequenceEqual(PNGHeader))
                    enumVar = (int)headerFooter.PNGHeader;
                else if (array.SequenceEqual(BMPHeader))
                {
                    enumVar = (int)headerFooter.BMPHeader;
                    fileSize = BitConverter.ToUInt32(
                    new byte[]
                    {
                        cluster[clusterSize_offset + 0],
                        cluster[clusterSize_offset + 1],
                        cluster[clusterSize_offset + 2],
                        cluster[clusterSize_offset + 3]
                    }, 0);
                }
                else
                {
                    byte[]
                        noTrail = RemoveTrailingZeros(cluster),
                        sig = new byte[JPGFooter.Length];
                    Array.ConstrainedCopy(cluster, noTrail.Length - JPGFooter.Length, sig, 0, JPGFooter.Length);

                    lastSectorSize = (uint)noTrail.Length;
                    Array.Copy(array, noTrail, JPGFooter.Length);

                    if (sig.SequenceEqual(JPGFooter))
                        enumVar = (int)headerFooter.JPGFooter;
                    else if (sig.SequenceEqual(GIFFooter))
                        enumVar = (int)headerFooter.GIFFooter;
                    else if (sig.SequenceEqual(PNGFooter))
                        enumVar = (int)headerFooter.PNGFooter;
                }
            }
            return enumVar;
        }

        public static byte[] RemoveTrailingZeros(byte[] array)
        {
            int i = array.Length - 1;
            while (array[i] == 0 && i > 0)
                i--;
            byte[] temp = new byte[i + 1];
            Array.Copy(array, temp, i + 1);
            return temp;
        }
    }
}