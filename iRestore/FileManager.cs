using iRestore;
using iTired;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using static iRestore.HeaderChecker;

namespace iCarve
{
    delegate int ClusterDelegate(int sector);
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

        public bool changeFATTable(uint startClusterNo, uint clustersInSize)
        {
            bool success = true;
            if (startClusterNo < Constants.START_CLUSTER)
                throw new FormatException();

            List<byte[]> FATChain = new List<byte[]>();

            for (uint t = startClusterNo + 1; t < startClusterNo + clustersInSize; t++)
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

            int multiplier = Constants.NULLED;
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

            uint start_offset = (uint)(multiplier * startClusterNo);

            int iter = Constants.NULLED;
            foreach (byte[] b in FATChain)
            {
                int cluster_ = (int)start_offset / BytesPerSector + FAT1_start_sector,
                    offset = (int)start_offset % BytesPerSector;

                int prev = iter;
                for (int i = 0; i < b.Length; i++)
                {
                    if (data[cluster_ * BytesPerSector + offset + prev++] != 0x00) //Does the file already have a FAT entry?
                    {
                        success = false;    //don't write to FAT table
                        break;              //and mosey out of here
                    }
                }

                if (success)
                {
                    foreach (byte by in b)
                        data[cluster_ * BytesPerSector + offset + iter++] = by;
                }
            }

            return success;
        }

        private const int EOFMarker = 0xFF;

        public int Carve()
        {
            int filesRestored = 0;
            RootDirectory r;
            ClusterDelegate getClusterNo = delegate (int sector)
            { return ((sector - DataRegion_start_sector) / SectorsPerCluster) + Constants.START_CLUSTER; };

            List<RootDirectory> root_entries = new List<RootDirectory>();

            uint 
                start_cluster_for_file = Constants.NULLED, 
                fileSize = Constants.NULLED,
                start_sector_for_file = Constants.NULLED;
            bool foundHeader = false;


            /*
             * Read all root directory entries
             */
            for (int j = 0; j < RootDirectory_size; j++)
            {
                byte[][] RootDirectoryEntries = split(getSector(j + RootDirectory_start_sector), Constants.RD_ENTRY_LENGTH);
                for (int i = 0; i < RootDirectoryEntries.Length; i++)
                    if (!RootDirectoryEntries[i].All(singlByte => singlByte == 0))
                        root_entries.Add(new RootDirectory(RootDirectoryEntries[i]));
            }

            /*
             * This for-loop reads each cluster in the data region and
             * changes the FAT region (as necessary).
             */
            for (int sector = DataRegion_start_sector; sector < data.Length / BytesPerSector && sector < ushort.MaxValue; sector++)
            {
                /*
                 * Prepare yourself for unneccessary unit and int casts
                 * and cluster and sector conversions.
                 */
                int type = HeaderChecker.checkClusterForFooterAndHeader(getSector(sector), ref fileSize, ref fileSize);

                switch (type)
                {
                    case (int)headerFooter.GIFHeader:
                    case (int)headerFooter.JPGHeader:
                    case (int)headerFooter.PNGHeader:
                        if (foundHeader)
                            throw new Exception();
                        else foundHeader = true;
                        start_sector_for_file = (uint)sector;
                        break;
                    case (int)headerFooter.BMPHeader:
                        if (foundHeader)
                            throw new Exception();
                        start_cluster_for_file = (uint)getClusterNo(sector);

                        if (changeFATTable(start_cluster_for_file, (uint)(Math.Ceiling((double)fileSize / bytesPerCluster))))
                        {
                            r = new RootDirectory();
                            r.FileName = (++filesRestored).ToString();
                            r.FileExtension = HeaderChecker.getExtension(type);
                            r.byteSize = fileSize;
                            r.start_cluster = (ushort)start_cluster_for_file;
                            root_entries.Add(r);
                        }
                        break;
                    case (int)headerFooter.GIFFooter:
                    case (int)headerFooter.JPGFooter:
                    case (int)headerFooter.PNGFooter:
                        if (!foundHeader)
                            throw new Exception();
                        else foundHeader = false;
                        int aa2 = getClusterNo(sector);
                        uint file_cluster_length = (uint)(getClusterNo(sector) - getClusterNo((int)start_sector_for_file)) + 1;

                        /*
                         * Oh my god, what have I created.
                         */
                        if (changeFATTable((uint)getClusterNo((int)start_sector_for_file), file_cluster_length))
                        {
                            r = new RootDirectory();
                            r.FileName = (++filesRestored).ToString();
                            r.FileExtension = HeaderChecker.getExtension(type);

                            int lastSectorLength = RemoveTrailingZeros(getSector(sector)).Length;
                            r.byteSize = (uint)(lastSectorLength + ((sector - start_sector_for_file) * BytesPerSector));

                            r.start_cluster = (ushort)getClusterNo((int)start_sector_for_file);
                            root_entries.Add(r);
                        }
                        break;
                    case (int)headerFooter.Invalid:
                        break;
                    default:
                        throw new Exception();
                }
            }



            if (filesRestored != 0)
            {
                //Code for reading the current root directories can also be placed here.

                /*
                 * Get the Root directory data in terms of sector byte data
                 */
                int clustersToReserve = (int)(Math.Floor(((double)root_entries.Count * Constants.RD_ENTRY_LENGTH) / BytesPerSector));
                List<byte[]> dataToWrite = new List<byte[]>();

                foreach (RootDirectory rr in root_entries)
                    dataToWrite.Add(rr.ByteData);

                int k = 0;
                foreach (byte[] b in dataToWrite)
                    foreach (byte bb in b)
                        data[RootDirectory_start_sector * BytesPerSector + k++] = bb;
            }

            return filesRestored;
        }

        public int Restore()
        {
            int FileRestored = 0;

            byte[][] root_directoryEntries = new byte[0][];

            for (int j = 0; j < RootDirectory_size; j++)
            {
                byte[][] RootDirectoryEntries = split(getSector(j + RootDirectory_start_sector), Constants.RD_ENTRY_LENGTH);
                root_directoryEntries = root_directoryEntries.Concat(RootDirectoryEntries).ToArray();
            }

            RootDirectory rd;

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

                        changeFATTable(start_cluster, clusters_inSize);
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

        private byte[][] split(byte[] array, int chunkSize)
        {
            return array
                .Select((s, i) => new { Value = s, Index = i })
                .GroupBy(x => x.Index / chunkSize)
                .Select(grp => grp.Select(x => x.Value).ToArray())
                .ToArray();
        }

        private byte[] getSector(int cluster)
        {
            byte[] array = new byte[BytesPerSector];
            Array.ConstrainedCopy(data, cluster * BytesPerSector, array, 0, BytesPerSector);
            return array;
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
        public int DataRegion_start_sector { get { return RootDirectory_start_sector + RootDirectory_size; } }



        #endregion
    }
}