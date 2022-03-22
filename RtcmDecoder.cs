using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Lomont.Information;

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

        const int Delimiter = 0xD3; // separates messages

        // scan till next 0xD3 start, skip current byte
        static void Scan(ref int index, byte [] data)
        {
            index++;
            while (index < data.Length && data[index] != Delimiter)
                ++index;
        }

        // Parse a message, update index
        static RtcmMessage ParseMessage(ref int index, byte [] data)
        {

            // message is D3, 6 bits of 0, 10 bits of message length, length bytes, 24 bits checksum
            if (data.Length <= index+3) // need to read header at least
                return null;
                
            if (data[index] != Delimiter)
            {
                // scan to next, return error
                Scan(ref index, data);
                return null;                
            }
            // read 2 bytes for length
            int b1 = data[index+1], b2 = data[index+2];
            var len = b1*256+b2; 
            if (1023 < len && data.Length <= index + 3 + len + 3)
            {   // invalid length, scan to next D3
                Scan(ref index, data);
                return null;
            }

            // checksum over all bytes except last 3
            
            var computedCrc = CRC.CRC_24Q(new ReadOnlySpan<byte>(data, index, len + 3));
            var ci = index+3+len;
            int c1 = data[ci], c2 = data[ci+1], c3 = data[ci+2];
            var readCrc = (uint)(c1*65536+c2*256+c3);
            if (computedCrc != readCrc)
            {
                // skip to next
                Scan(ref index, data);
                return null;
            }



            index += 3; // skip header

            // copy out payload
            var payload = new byte[len];
            Array.Copy(data,index,payload,0,len);

            // msg id is 12 bits in payload
            int p1 = payload[0], p2 = payload[1];
            var id = (p1 * 256 + p2) >> 4; // 12 bits

            index += len; // skip message
            index += 3; // skip checksum QualComm CRC-24Q

            RtcmMessage msg;
            switch (id)
            {
                case 1004:
                    msg = new RtcmMessage1004(payload, index, computedCrc);
                    break;
                case 1006:
                    msg = new RtcmMessage1006(payload, index, computedCrc);
                    break;
                case 1008:
                    msg = new RtcmMessage1008(payload, index, computedCrc);
                    break;
                case 1012:
                    msg = new RtcmMessage1012(payload, index, computedCrc);
                    break;
                case 1013:
                    msg = new RtcmMessage1013(payload, index, computedCrc);
                    break;
                case 1014:
                    msg = new RtcmMessage1014(payload, index, computedCrc);
                    break;
                case 1015:
                    msg = new RtcmMessage1015(payload, index, computedCrc);
                    break;
                case 1016:
                    msg = new RtcmMessage1016(payload, index, computedCrc);
                    break;
                case 1033:
                    msg = new RtcmMessage1033(payload, index, computedCrc);
                    break;
                case 1037:
                    msg = new RtcmMessage1037(payload, index, computedCrc);
                    break;
                case 1038:
                    msg = new RtcmMessage1038(payload, index, computedCrc);
                    break;
                case 1230:
                    msg = new RtcmMessage1230(payload, index, computedCrc);
                    break;
                default:
                    msg = new RtcmMessage(payload, index, computedCrc);
                    break;
            }

            return msg;
        }
        #endregion
    }
}