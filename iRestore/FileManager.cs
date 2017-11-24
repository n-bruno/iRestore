using iTired;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
namespace iRestore
{
    public class FileManager
    {
        public enum FAT { Invalid, FAT12, FAT16, FAT32 }

        private byte[] data;

        private readonly string filePath;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="filePath"></param>
        public FileManager(string filePath)
        {
            this.filePath = filePath;
            data = File.ReadAllBytes(filePath);

            //Check what FAT version it is.
            switch (Encoding.ASCII.GetString(data, 0x36, 5))
            {
                case "FAT32": FATVersion = (int)FAT.FAT32; break;
                case "FAT16": FATVersion = (int)FAT.FAT16; break;
                case "FAT12": FATVersion = (int)FAT.FAT12; break;
                default: FATVersion = (int)FAT.Invalid; break;
            }
            const int adress_bps = 0xB;
            //get bytes per sector
            BytesPerSector = BitConverter.ToUInt16(new byte[] { data[adress_bps], data[adress_bps + 1] }, 0);
            //split the data by sectors
        }

        private const int EOFMarker = 0xFF;

        /// <summary>
        /// Restores files by reading the Root Directory for any deleted entries.
        /// </summary>
        /// <returns></returns>
        public int Restore()
        {
            int FileRestored = 0;

            byte[][] root_directoryEntries = new byte[0][];

            for (int j = 0; j < RootDirectory_size; j++)
            {
                byte[][] RootDirectoryEntries = split(getCluster(j + RootDirectory_start_sector), Constants.RD_ENTRY_LENGTH);
                root_directoryEntries = root_directoryEntries.Concat(RootDirectoryEntries).ToArray();
            }

            RootDirectory rd;

            /*int startIndex = 0;

            switch (FATVersion)
            {
                case (int)FAT.FAT12: startIndex = 2; break;
                case (int)FAT.FAT16: startIndex = 3; break;
                case (int)FAT.FAT32: startIndex = 8; break;
                default: throw new Exception();
            }*/

            for (int i = 0; i < root_directoryEntries.Length; i++)
            {
                bool hasAllZeros = root_directoryEntries[i].All(singlByte => singlByte == 0);
                if (!hasAllZeros)
                {
                    if (root_directoryEntries[i][0] == 0xE5)
                    {
                        FileRestored++;
                        writeToArray(RootDirectory_start_sector, i * Constants.RD_ENTRY_LENGTH, 0x5F); //hacky
                        root_directoryEntries[i][0] = 0x5F;
                        rd = new RootDirectory(root_directoryEntries[i]);

                        double temp = (double)rd.byteSize / (double)bytesPerCluster;
                        uint
                            start_cluster = rd.start_cluster,
                            clusters_inSize = (uint)Math.Ceiling(temp);

                        if (start_cluster >= 2)
                        {
                            List<byte[]> FATChain = new List<byte[]>();

                            for (uint t = start_cluster + 1; t < start_cluster + clusters_inSize; t++)
                            {
                                byte[] entry;
                                switch (FATVersion)
                                {
                                    case (int)FAT.FAT12:
                                        entry = BitConverter.GetBytes((byte)t);
                                        break;
                                    case (int)FAT.FAT16:
                                        entry = BitConverter.GetBytes((ushort)t);
                                        break;
                                    case (int)FAT.FAT32:
                                        entry = BitConverter.GetBytes(t);
                                        break;
                                    default:
                                        throw new Exception();
                                }
                                FATChain.Add(entry);
                            }

                            byte[] eofArray;
                            switch (FATVersion)
                            {
                                case (int)FAT.FAT12:
                                    eofArray = new byte[] { EOFMarker };
                                    break;
                                case (int)FAT.FAT16:
                                    eofArray = new byte[] { EOFMarker, EOFMarker };
                                    break;
                                case (int)FAT.FAT32:
                                    eofArray = new byte[] { EOFMarker, EOFMarker, EOFMarker };
                                    break;
                                default:
                                    throw new Exception();
                            }

                            FATChain.Add(eofArray);

                            int multiplier = 0;
                            switch (FATVersion)
                            {
                                case (int)FAT.FAT12:
                                    multiplier = 1;
                                    break;
                                case (int)FAT.FAT16:
                                    multiplier = 2;
                                    break;
                                case (int)FAT.FAT32:
                                    multiplier = 3;
                                    break;
                                default:
                                    throw new Exception();
                            }

                            uint start_offset = (uint)(multiplier * start_cluster);


                            int iter = 0;
                            foreach (byte[] b in FATChain)
                            {
                                int cluster_ = (int)start_offset / BytesPerSector + FAT1_start_sector,
                                    offset = (int)start_offset % BytesPerSector;

                                foreach (byte by in b)
                                    writeToArray(cluster_, offset + iter++, by);
                            }
                        }
                        else throw new FormatException();
                    }
                }
            }

            return FileRestored;
        }

        /// <summary>
        /// Save the modified file to the filesystem.
        /// </summary>
        public void Save()
        { File.WriteAllBytes(filePath, data); }

        private void FromShort(ushort number, out byte byte1, out byte byte2)
        {
            byte2 = (byte)(number >> 8);
            byte1 = (byte)(number & 0xFF);
        }

        private void writeToArray(int cluster, int offset, byte value)
        {
            if (offset > BytesPerSector)
                throw new Exception();

            data[cluster * BytesPerSector + offset] = value;
        }

        private byte ReadArray(int cluster, int offset)
        {
            if (offset > BytesPerSector)
                throw new Exception();
            return data[cluster * BytesPerSector + offset];
        }

        private byte[] getCluster(int cluster)
        {
            byte[] array = new byte[BytesPerSector];
            Array.ConstrainedCopy(data, cluster * BytesPerSector, array, 0, BytesPerSector);
            return array;
        }

        private byte[][] split(byte[] array, int chunkSize)
        {
            return array
                .Select((s, i) => new { Value = s, Index = i })
                .GroupBy(x => x.Index / chunkSize)
                .Select(grp => grp.Select(x => x.Value).ToArray())
                .ToArray();
        }
        private ushort getUShort(int address)
        { return BitConverter.ToUInt16(new byte[] { ReadArray(0, address), ReadArray(0, address + 1) }, 0); }
        public int FATVersion { private set; get; }
        public ushort BytesPerSector { private set; get; }
        public byte SectorsPerCluster { get { return ReadArray(0, 0x0D); } }
        public ushort ReservedAreaSize { get { return getUShort(0x0E); } }
        public byte NumberOfFATCopies { get { return ReadArray(0, 0x10); } }
        public ushort NumberOfRootDirectoryEntries { get { return getUShort(0x11); } }
        public byte SectorsPerFAT { get { return ReadArray(0, 0x016); } }
        public int FAT1_start_sector { get { return ReservedAreaSize; } }
        #region Calculations
        public int bytesPerCluster { get { return BytesPerSector * SectorsPerCluster; } }
        public int RootDirectory_start_sector { get { return FAT1_start_sector + SectorsPerFAT * NumberOfFATCopies; } }
        public int RootDirectory_size { get { return (NumberOfRootDirectoryEntries * Constants.RD_ENTRY_LENGTH) / BytesPerSector; } }
        #endregion
    }
}