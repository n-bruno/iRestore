using System;
using System.IO;
using System.Text;

namespace iTired
{
    public class RootDirectory
    {
        private const int
            fileName_address = 0,
            fileName_size = 8,
            fileExt_address = 8,
            fileExt_size = 3,

            fileAttribute_address = 11,

            date_size = 2,
            createTime_address = 14,
            createDate_address = 16,
            accessDate_address = 18,

            modifyTime_address = 22,
            modifyDate_address = 24,

            startingCluster_address = 26,

            fileSize_address = 28;

        private byte[] data;
        public RootDirectory(byte[] data)
        {
            if (data.Length == Constants.RD_ENTRY_LENGTH)
                this.data = data;
            else throw new ArgumentException();
        }

        public RootDirectory(string filePath)
        {
            data = File.ReadAllBytes(filePath);

            if (!(data.Length == Constants.RD_ENTRY_LENGTH))
                throw new ArgumentException();
        }

        public string FileName
        { get { return Encoding.ASCII.GetString(data, fileName_address, fileName_size); } }

        public string FileExtension
        { get { return Encoding.ASCII.GetString(data, fileExt_address, fileExt_size); } }

        private const byte
            mask0 = 1,
            mask1 = 2,
            mask2 = 4,
            mask3 = 8,
            mask4 = 16,
            mask5 = 32;

        public bool isReadonly
        { get { return Convert.ToBoolean(mask0 & data[fileAttribute_address]); } }
        public bool isHidden
        { get { return Convert.ToBoolean(mask1 & data[fileAttribute_address]); } }
        public bool isSystemFile
        { get { return Convert.ToBoolean(mask2 & data[fileAttribute_address]); } }
        public bool isVolumeLabel
        { get { return Convert.ToBoolean(mask3 & data[fileAttribute_address]); } }
        public bool isFolder
        { get { return Convert.ToBoolean(mask4 & data[fileAttribute_address]); } }
        public bool isArchive
        { get { return Convert.ToBoolean(mask5 & data[fileAttribute_address]); } }

        public ushort start_cluster
        {
            get
            {
                return BitConverter.ToUInt16(
                    new byte[]
                    {
                        data[startingCluster_address],
                        data[startingCluster_address + 1]
                    }, 0
                    );
            }
        }

        public string createdDate
        { get { return generateDate(data[createDate_address], data[createDate_address + 1]); } }

        public string createdTime
        { get { return generateTime(data[createTime_address], data[createTime_address + 1]); } }

        public string modifyDate
        { get { return generateDate(data[modifyDate_address], data[modifyDate_address + 1]); } }

        public string modifyTime
        { get { return generateTime(data[modifyTime_address], data[modifyTime_address + 1]); } }

        public string accessDate
        { get { return generateDate(data[accessDate_address], data[accessDate_address + 1]); } }

        public uint byteSize
        {
            get
            {
                return BitConverter.ToUInt32(
                    new byte[] 
                    {
                        data[fileSize_address],
                        data[fileSize_address + 1],
                        data[fileSize_address + 2],
                        data[fileSize_address + 3]
                    }, 0);
            }
        }

        /*
         * Byte 1       Byte 2
         * --------     --------
         * mmmddddd     yyyyyyym 
         * 
         * d = day
         * m = month
         * y = year
         */

        private const int
            day_mask = 0x1F,

            month_mask1 = 0x01,
            month_mask2 = 0xE0,
            month_shift1 = 3,
            month_shift2 = 5,

            year_mask = 0xFE,
            year_shift = 0x01,
            min_year = 1980;
        public void generateDate(byte byte1, byte byte2, out int day, out int month, out int year)
        {
            day = byte1 & day_mask;
            month = ((month_mask1 & byte2) << month_shift1) | ((month_mask2 & byte1) >> month_shift2);
            year = ((byte2 & year_mask) >> year_shift) + min_year;
        }

        private const string sep = "/";

        public string generateDate(byte byte1, byte byte2)
        {
            int day, month, year;
            generateDate(byte1, byte2, out day, out month, out year);


            string s = "";
            if (day == 0 || month == 0)
                s = Constants.INVALID;
            else s = year + sep + month.ToString(d) + sep + day.ToString(d);
            return s;
        }



        private const byte
            seconds_mask = 0x1F,
            seconds_multipler = 2,

            minutes_shift1 = 3,
            minutes_mask1 = 0x38,

            minutes_shift2 = 5,
            minutes_mask2 = 0x07,

            hours_shift = 3,
            hours_mask = 0x1F;
        /*
         * Byte 1       Byte 2
         * --------     --------
         * mmmsssss     hhhhhmmm
         * 
         * h = hours 
         * m = minutes
         * s = seconds
         */
        public void generateTime(byte byte1, byte byte2, out int seconds, out int minutes, out int hours)
        {
            seconds = (byte1 & seconds_mask) * seconds_multipler; //Why? There's unused bytes.
            minutes = ((byte2 << minutes_shift1) & minutes_mask1) | ((byte1 >> minutes_shift2) & minutes_mask2);
            hours = ((byte2 >> hours_shift) & hours_mask);
        }

        private const string sep2 = ":", d = "00";
        private const int twelve = 12;

        public string generateTime(byte byte1, byte byte2)
        {
            int seconds, minutes, hours;
            generateTime(byte1, byte2, out seconds, out minutes, out hours);

            string s = "";
            switch (hours % twelve)
            {
                case 0:
                    s = " AM";
                    break;
                default:
                    s = " PM";
                    hours %= twelve;
                    break;
            }

            return hours.ToString(d) + sep2 + minutes.ToString(d) + sep2 + seconds.ToString(d) + s;
        }
    }
}
