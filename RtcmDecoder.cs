using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Lomont.Gps {


    /// <summary>
    /// RTCM message decoder. Splits a message stream into individual messages
    ///     
    /// https://northsurveying.com/index.php/soporte/gnss-and-geodesy-concepts    
    /// </summary>
    public static class RtcmDecoder{

        /// <summary>
        /// Parse the file into the message lists
        /// Return successful and failed messages count
        /// </summary>
        public static (int successful, int failed) Parse(string filename, List<Message> messages)
        {
            Trace.WriteLine($"Parsing RTCM {filename}");
            if (!File.Exists(filename))
            {
                Trace.TraceError($"File {filename} not found");
                return (0, 0);
            }

            var data = File.ReadAllBytes(filename);

            //data = new byte[]{
            //        // testing message from a spec
            //    0xD3 , 0x00 , 0x13 , 0x3E , 0xD7 , 0xD3 , 0x02 , 0x02 , 0x98 ,0x0E ,0xDE ,0xEF ,0x34 ,0xB4 ,0xBD ,0x62,
            //    0xAC , 0x09 , 0x41 , 0x98 , 0x6F , 0x33 , 0x36 , 0x0B , 0x98
            //}
            //;

            int successful = 0, failed = 0;
            var index = 0; // start index
            while (index < data.Length)
            {
                var msg = ParseMessage(ref index, data);
                if (msg != null)
                {
                    messages.Add(msg);
                    successful++;
                }
                else
                {
                    Trace.TraceError($"Bad RTCM message at index {index}");
                    failed++;
                }
            }
            return (successful, failed);
        }

        #region Implementation

        // todo - fix Lomont lib CRC24 - does not match this :(
        public class CRC24
        {
            private static readonly uint[] tbl =
            {
                0x000000, 0x864CFB, 0x8AD50D, 0x0C99F6, 0x93E6E1, 0x15AA1A, 0x1933EC, 0x9F7F17,
                0xA18139, 0x27CDC2, 0x2B5434, 0xAD18CF, 0x3267D8, 0xB42B23, 0xB8B2D5, 0x3EFE2E,
                0xC54E89, 0x430272, 0x4F9B84, 0xC9D77F, 0x56A868, 0xD0E493, 0xDC7D65, 0x5A319E,
                0x64CFB0, 0xE2834B, 0xEE1ABD, 0x685646, 0xF72951, 0x7165AA, 0x7DFC5C, 0xFBB0A7,
                0x0CD1E9, 0x8A9D12, 0x8604E4, 0x00481F, 0x9F3708, 0x197BF3, 0x15E205, 0x93AEFE,
                0xAD50D0, 0x2B1C2B, 0x2785DD, 0xA1C926, 0x3EB631, 0xB8FACA, 0xB4633C, 0x322FC7,
                0xC99F60, 0x4FD39B, 0x434A6D, 0xC50696, 0x5A7981, 0xDC357A, 0xD0AC8C, 0x56E077,
                0x681E59, 0xEE52A2, 0xE2CB54, 0x6487AF, 0xFBF8B8, 0x7DB443, 0x712DB5, 0xF7614E,
                0x19A3D2, 0x9FEF29, 0x9376DF, 0x153A24, 0x8A4533, 0x0C09C8, 0x00903E, 0x86DCC5,
                0xB822EB, 0x3E6E10, 0x32F7E6, 0xB4BB1D, 0x2BC40A, 0xAD88F1, 0xA11107, 0x275DFC,
                0xDCED5B, 0x5AA1A0, 0x563856, 0xD074AD, 0x4F0BBA, 0xC94741, 0xC5DEB7, 0x43924C,
                0x7D6C62, 0xFB2099, 0xF7B96F, 0x71F594, 0xEE8A83, 0x68C678, 0x645F8E, 0xE21375,
                0x15723B, 0x933EC0, 0x9FA736, 0x19EBCD, 0x8694DA, 0x00D821, 0x0C41D7, 0x8A0D2C,
                0xB4F302, 0x32BFF9, 0x3E260F, 0xB86AF4, 0x2715E3, 0xA15918, 0xADC0EE, 0x2B8C15,
                0xD03CB2, 0x567049, 0x5AE9BF, 0xDCA544, 0x43DA53, 0xC596A8, 0xC90F5E, 0x4F43A5,
                0x71BD8B, 0xF7F170, 0xFB6886, 0x7D247D, 0xE25B6A, 0x641791, 0x688E67, 0xEEC29C,
                0x3347A4, 0xB50B5F, 0xB992A9, 0x3FDE52, 0xA0A145, 0x26EDBE, 0x2A7448, 0xAC38B3,
                0x92C69D, 0x148A66, 0x181390, 0x9E5F6B, 0x01207C, 0x876C87, 0x8BF571, 0x0DB98A,
                0xF6092D, 0x7045D6, 0x7CDC20, 0xFA90DB, 0x65EFCC, 0xE3A337, 0xEF3AC1, 0x69763A,
                0x578814, 0xD1C4EF, 0xDD5D19, 0x5B11E2, 0xC46EF5, 0x42220E, 0x4EBBF8, 0xC8F703,
                0x3F964D, 0xB9DAB6, 0xB54340, 0x330FBB, 0xAC70AC, 0x2A3C57, 0x26A5A1, 0xA0E95A,
                0x9E1774, 0x185B8F, 0x14C279, 0x928E82, 0x0DF195, 0x8BBD6E, 0x872498, 0x016863,
                0xFAD8C4, 0x7C943F, 0x700DC9, 0xF64132, 0x693E25, 0xEF72DE, 0xE3EB28, 0x65A7D3,
                0x5B59FD, 0xDD1506, 0xD18CF0, 0x57C00B, 0xC8BF1C, 0x4EF3E7, 0x426A11, 0xC426EA,
                0x2AE476, 0xACA88D, 0xA0317B, 0x267D80, 0xB90297, 0x3F4E6C, 0x33D79A, 0xB59B61,
                0x8B654F, 0x0D29B4, 0x01B042, 0x87FCB9, 0x1883AE, 0x9ECF55, 0x9256A3, 0x141A58,
                0xEFAAFF, 0x69E604, 0x657FF2, 0xE33309, 0x7C4C1E, 0xFA00E5, 0xF69913, 0x70D5E8,
                0x4E2BC6, 0xC8673D, 0xC4FECB, 0x42B230, 0xDDCD27, 0x5B81DC, 0x57182A, 0xD154D1,
                0x26359F, 0xA07964, 0xACE092, 0x2AAC69, 0xB5D37E, 0x339F85, 0x3F0673, 0xB94A88,
                0x87B4A6, 0x01F85D, 0x0D61AB, 0x8B2D50, 0x145247, 0x921EBC, 0x9E874A, 0x18CBB1,
                0xE37B16, 0x6537ED, 0x69AE1B, 0xEFE2E0, 0x709DF7, 0xF6D10C, 0xFA48FA, 0x7C0401,
                0x42FA2F, 0xC4B6D4, 0xC82F22, 0x4E63D9, 0xD11CCE, 0x575035, 0x5BC9C3, 0xDD8538
            };

            // Qualcomm 24-bit Cyclical Redundancy Check (CRC-24Q).
            // CRC polynomial 0x1864CFB, not reversed, not XORed
            public static uint Compute(ReadOnlySpan<byte> buf)
            {
                uint crc = 0;
                foreach (var t in buf)
                    crc = ((crc << 8) & 0xFFFFFF) ^ tbl[(crc >> 16) ^ t];
                return crc;
            }
        }


        const int Delimiter = 0xD3; // separates messages
        const int HeaderLength = 3;
        const int CrcLength = 3;


        // scan till next 0xD3 start, skip current byte
        static void Scan(ref int index, byte [] data)
        {
            index++;
            while (index < data.Length && data[index] != Delimiter)
                ++index;
        }

        // Parse a message, update index
        static RtcmMessage ParseMessage(ref int startIndex, byte [] data)
        {

            // message is D3, 6 bits of 0, 10 bits of message length, length bytes, 24 bits checksum
            if (data.Length <= startIndex + 3) // need to read header at least
            {
                Trace.TraceError($"RTCM data too short at offset 0x{startIndex:X8}");
                return null;
            }

            if (data[startIndex] != Delimiter)
            {
                Trace.TraceWarning($"RTCM not at header at offset 0x{startIndex:X8}, scanning forward...");
                // scan to next, return error
                Scan(ref startIndex, data);
                return null;                
            }
            // read 2 bytes for length
            int b1 = data[startIndex+1], b2 = data[startIndex+2];
            var len = b1*256+b2; 
            if (1023 < len || data.Length <= startIndex + HeaderLength + len + CrcLength)
            {   // invalid length, scan to next D3
                Trace.TraceError($"RTCM invalid length header at offset 0x{startIndex:X8}, scanning forward...");
                Scan(ref startIndex, data);
                return null;
            }

            // checksum over all bytes except last 3
            // todo - error - fix! var computedCrc = CRC.CRC_24Q(new ReadOnlySpan<byte>(data, index, len+HeaderLength));
            var computedCrc = CRC24.Compute(new ReadOnlySpan<byte>(data, startIndex, len + HeaderLength));
            var ci = startIndex+ HeaderLength + len;
            int c1 = data[ci], c2 = data[ci+1], c3 = data[ci+2];
            var readCrc = (uint)(c1*65536+c2*256+c3);
            if (computedCrc != readCrc)
            {
                Trace.TraceError($"RTCM bad crc header {computedCrc:X3} != {readCrc:X3}  at offset 0x{startIndex:X8}, scanning forward...");
                // skip to next
                Scan(ref startIndex, data);
                return null;
            }




            // copy out payload
            var payload = new byte[len];
            Array.Copy(data,startIndex + HeaderLength,payload,0,len);

            // msg id is 12 bits in payload
            int p1 = payload[0], p2 = payload[1];
            var id = (p1 * 256 + p2) >> 4; // 12 bits

            RtcmMessage msg;
            switch (id)
            {

                case 1004:
                    msg = new RtcmMessage1004(payload, startIndex, computedCrc, "Extended L1&L2 GPS RTK Observables for GPS RTK Use");
                    break;
                case 1006:
                    msg = new RtcmMessage1006(payload, startIndex, computedCrc, "Stationary RTK Reference Station ARP plus the Antenna Height");
                    break;
                case 1008:
                    msg = new RtcmMessage1008(payload, startIndex, computedCrc, "Antenna Descriptor and Serial Number");
                    break;
                case 1012:
                    msg = new RtcmMessage1012(payload, startIndex, computedCrc, "Extended L1&L2 GLONASS RTK Observables");
                    break;
                case 1013:
                    msg = new RtcmMessage1013(payload, startIndex, computedCrc, "System Parameters, time offsets, lists of messages sent");
                    break;
                case 1014:
                    msg = new RtcmMessage1014(payload, startIndex, computedCrc, "Network Auxiliary Station Data");
                    break;
                case 1015:
                    msg = new RtcmMessage1015(payload, startIndex, computedCrc, "GPS Ionospheric Correction Differences");
                    break;
                case 1016:
                    msg = new RtcmMessage1016(payload, startIndex, computedCrc, "GPS Geometric Correction Differences");
                    break;
                case 1033:
                    msg = new RtcmMessage1033(payload, startIndex, computedCrc, "Receiver and Antenna Descriptors");
                    break;
                case 1037:
                    msg = new RtcmMessage1037(payload, startIndex, computedCrc, "GLONASS Ionospheric Correction Differences");
                    break;
                case 1038:
                    msg = new RtcmMessage1038(payload, startIndex, computedCrc, "GLONASS Geometric Correction Differences");
                    break;
                case 1230:
                    msg = new RtcmMessage1230(payload, startIndex, computedCrc, "GLONASS L1 and L2 Code-Phase Biases");
                    break;
                default:
                    msg = new RtcmMessage(payload, startIndex, computedCrc, "Undecoded RTCM message type");
                    Trace.TraceWarning($"Unknown RTCM message id {id} at index {startIndex}");
                    break;
            }

            startIndex += HeaderLength; // skip header
            startIndex += len; // skip message
            startIndex += CrcLength; // skip checksum QualComm CRC-24Q

            return msg;
        }

        /// <summary>
        /// See if file is a RTCM file
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        public static bool DetectFile(string filename)
        {
            if (!File.Exists(filename))
                return false;

            var data = File.ReadAllBytes(filename);

            // try reading a few
            int successful = 0, failed = 0;
            var index = 0; // start index
            while (index < data.Length)
            {
                var msg = ParseMessage(ref index, data);
                if (msg != null)
                    successful++;
                else
                    failed++;
                if (failed + successful > 5) 
                    break;
            }

            return successful > 0; // call any matches a success
        }
        #endregion
    }
}
