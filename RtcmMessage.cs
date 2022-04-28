using System.Collections.Generic;
using System.Diagnostics;
using System;
using System.Globalization;
using System.Net.Mime;
using System.Text;

namespace Lomont.Gps
{
    /// <summary>
    /// RTCM messages are the protocol to communicate differential GPS messages
    /// Used for RTK-GPS
    /// </summary>
    public class RtcmMessage : Message
    {
        public RtcmMessage(byte[] payload, int index, uint checksum)
        {
            this.payload = payload;

            int p1 = payload[0], p2 = payload[1];
            id = (p1 * 256 + p2) >> 4; // 12 bits
            this.index = index;
            this.checksum = checksum;
        }

        // index into stream
        public int index { get;  }
        // payload bytes
        public byte[] payload { get;  }
        // message id
        public int id { get;  }

        // checksum
        public uint checksum { get;  }

        public class BitReader
        {
            private byte[] data;
            private int bitIndex = 0;
            public BitReader(byte[] data)
            {
                this.data = data;
                bitIndex = 0;
            }

            ulong GetBits(int bitLength)
            {
                Trace.Assert(bitLength > 0);

                ulong bits = 0;

                for (var j = bitIndex; j < bitIndex + bitLength; ++j)
                    bits |= (ulong)((data[j / 8] >> (j & 7)) & 1) << (j - bitIndex);
                bitIndex += bitLength;

                return bits;
            }


            public uint U32(int bitLength) => (uint)(GetBits(bitLength));

            public int S32(int bitLength) => (int)S64(bitLength);

            public long S64(int bitLength)
            {
                var value = GetBits(bitLength);
                var signBit = value >> (bitLength - 1);
                if (signBit != 0)
                    return (long)((~value) + 1); // negate via 2's complement
                return (long)value;
            }

            public string ReadChars(uint charCount)
            {
                var s = new StringBuilder();
                for (var i =0; i < charCount; ++i)
                {
                    var ch = U32(8); // read byte
                    s.Append((char)ch);
                }
                return s.ToString();
            }
        }

    }
    // https://www.geopp.de/pdf/gppigs06_rtcm_f.pdf
    // Data Fields DF001 to DF426
    // DF001: uint12 - 0-4095 - message number 
    // DF002: uint12 - 0-4095 - reference station ID
    // ...  : uint30 - 0-604,799,999 ms - GPS Epoch time (ms from beginning of midnight Sat night/Sun morn, in GPS time (not UTC!)
    // bit1   -   - 0 = no more refs synced to same Epoch Time, 1 = next message same Epoch Time
    // uint5 - 0-31 - GPS satellites processed number of satellites in this message
    // bit - 0=divergence free smoothing not used, 1= is used
    // bit3 - smoothing interval
    // uint6 - 1-63 - GPS satellite ID
    // bit1 - GPS L1 code indicator - 0 = C/A code, 1 = P(Y) code Direct
    // uint24 - 0-299,792.46m in 0.02m, GPS L1 Pseudorange
    // int20 - +-2621435 m - GPS L1 PhaseRange - L1 PseudoRange
    // uint7 GPS Lock Time Indicator
    // uint8 - GPS Integer L1 Pseudorange Modulus Ambiguity
    // uint8 - GPS L1 CNR estimate of SnR in dB-Hz. 0 = not computed
    // ...
    // ...

    // Message Types (MT) - originally 13 messages defined
    // Observations             MT
    //  - GPS L1              1001,1002
    //  - GPS L1/L2           1003,1004
    //  - GLONASS L1          1009,1010
    //  - GLONASS L1/L2       1011,1012
    // Station coords         1005,1006
    // Antenna Description    1007,1008
    // Aux operation info     1013
    //
    // Supplement 1
    // Network RTK            1014-1017
    // GPS Ephemeris          1019
    // GLONASS Ephemeris      1020
    // proprietary            4088-4095




    // messages we see: 1004,1006,1008,1012,1013,1014,1015,1016,1033,1037,1038,1230
    // 1001-1004 header:
    // Message Number(e.g.,“1001”= 0011 1110 1001) DF002 uint12 12
    // Reference Station ID DF003 uint12 12
    // GPS Epoch Time(TOW) DF004 uint30 30
    // Synchronous GNSS Flag DF005 bit(1) 1
    // No.of GPS Satellite Signals Processed DF006 uint5 5
    // GPS Divergence-free Smoothing Indicator DF007 bit(1) 1
    // GPS Smoothing Interval DF008 bit(3) 3
    // TOTAL 64

    // 1004 body, per satellite
    // GPS Satellite ID DF009 uint6 6
    // GPS L1 Code Indicator DF010 bit(1) 1
    // GPS L1 Pseudorange DF011 uint24 24
    // GPS L1 PhaseRange – L1 Pseudorange DF012 int20 20
    // GPS L1 Lock time Indicator DF013 uint7 7
    // GPS Integer L1 Pseudorange Modulus Ambiguity
    // DF014 uint8 8
    // GPS L1 CNR DF015 uint8 8
    // GPS L2 Code Indicator DF016 bit(2) 2
    // GPS L2-L1 Pseudorange Difference DF017 int14 14
    // GPS L2 PhaseRange – L1 Pseudorange DF018 int20 20
    // GPS L2 Lock time Indicator DF019 uint7 7
    // GPS L2 CNR DF020 uint8 8
    // TOTAL 125


    // notes https://www.use-snip.com/kb/knowledge-base/rtcm-3-message-list/
    // from a run
    // msgs used:              (count)
    // 1006, 1013              ( 2059)
    // 1008, 1033, 1230        ( 2058)
    // 1004, 1012              (10294)
    // 1014,                   (10292)
    // 1015, 1016, 1037, 1038, (10290)


    // 1004 Extended L1&L2 GPS RTK Observables for GPS RTK Use, the main msg 

    // 1006 Stationary RTK Reference Station ARP plus the Antenna Height
    // 1008 Antenna Descriptor and Serial Number

    // 1012 Extended L1&L2 GLONASS RTK Observables, the other main msg

    // 1013 System Parameters, time offsets, lists of messages sent
    // 1014 Network Auxiliary Station Data
    // 1015 GPS Ionospheric Correction Differences
    // 1016 GPS Geometric Correction Differences
    // 1033 Receiver and Antenna Descriptors

    // 1037 GLONASS Ionospheric Correction Differences
    // 1038 GLONASS Geometric Correction Differences
    // 1230 GLONASS L1 and L2 Code-Phase Biases

    //1029 - Unicode Text string
    //4092 - Assigned to Leica Geosystems

    // see message bit layouts in
    // https://github.com/mathias13/RTCM
    // https://github.com/asvol/gnss.net  - has RTCM official specs

    class RtcmMessage1004 : RtcmMessage
    {

        public uint RefStationId { get;  }

        public uint Tow { get;  }

        public bool SyncGnssFlag { get;  }

        public uint NumberGpsSats { get; }

        public bool GpsDivSmooth { get; }

        public uint CarrSmoothInterval { get;  }

        public RtcmMessage1004(byte[] payload, int index, uint checksum) : base(payload, index, checksum)
        { // todo parser in here
            var r = new BitReader(payload);

            r.U32(12); // skip header
            RefStationId = r.U32(12);
            Tow = r.U32(30);
            SyncGnssFlag = r.U32(1) > 0;
            NumberGpsSats = r.U32(5);
            GpsDivSmooth = r.U32(1) > 0;
            CarrSmoothInterval = r.U32(3);

            for (var i = 0; i < NumberGpsSats; i++)
                sd.Add(new SatelliteData(r));
        }

        private List<SatelliteData> sd = new();
        public IReadOnlyList<SatelliteData> SatellitesData => sd.AsReadOnly();
        public class SatelliteData
        {
            public enum L2CodeIndicator
            {
                C_A_code = 0,
                P_code = 1,
                Reserved = 2
            }
            public SatelliteData(BitReader r)
            {
                SatID = r.U32(6);
                L1CodeInd = r.U32(1) > 0;
                L1Pseudo = r.U32(24);
                L1PhaseMinusPseudo = r.S32(20);
                L1LockTimeInd = r.U32(7);
                L1PseudoModulusAmbiguity = r.U32(8);
                L1CNR = r.U32(8);
                L2CodeInd = (L2CodeIndicator)r.U32(2);
                L2_L1_PseudoDifference = r.S32(14);
                L2PhaseMinus_L1Pseudo = r.S32(20);
                L22LockTimeInd = r.U32(7);
                L2CNR = r.U32(8);
            }


            public uint SatID {get; }

            public bool L1CodeInd {get; }

            public uint L1Pseudo {get; }

            public int L1PhaseMinusPseudo {get; }

            public uint L1LockTimeInd {get; }

            public uint L1PseudoModulusAmbiguity {get; }

            public uint L1CNR {get; }

            public L2CodeIndicator L2CodeInd {get; }

            public int L2_L1_PseudoDifference {get; }

            public int L2PhaseMinus_L1Pseudo {get; }

            public uint L22LockTimeInd {get; }

            public uint L2CNR {get; }

        }



    }
    class RtcmMessage1006 : RtcmMessage
    {
        public uint RefId {get; }

        public uint Irtf {get; }

        public bool Gps {get; }

        public bool Glonass {get; }

        public bool Galileo {get; }

        public bool RefStationIndicator {get; }

        public long RefX {get; }

        public bool SingleReceiverIndicator {get; }

        public bool Reserved => false;

        public long RefY {get; }

        public bool Reserved1 => false;

        public bool Reserved2 => false;

        public long RefZ {get; }

        public ushort AntennaHeight {get; }

        public RtcmMessage1006(byte[] payload, int index, uint checksum) : base(payload, index, checksum)
        { // todo parser in here

            var r = new BitReader(payload);
            r.U32(12); // skip header

            RefId = r.U32(12);
            Irtf = r.U32(6);
            Gps = r.U32(1) > 0;
            Glonass = r.U32(1) > 0;
            Galileo = r.U32(1) > 0;
            RefStationIndicator = r.U32(1) > 0;
            RefX = r.S64(38);
            SingleReceiverIndicator = r.U32(1) > 0;
            r.U32(1); // skip reserved
            RefY = r.S64(38);
            RefZ = r.S64(38);
            r.U32(1); // skip reserved
            r.U32(1); // skip reserved
            AntennaHeight = (ushort)r.U32(16);
        }
    }
    class RtcmMessage1008 : RtcmMessage
    {
        public RtcmMessage1008(byte[] payload, int index, uint checksum) : base(payload, index, checksum)
        {
            var r = new BitReader(payload);
            r.U32(12); // skip header

            RefStationID = r.U32(12);
            var descriptorLen = r.U32(8);
            AntennaDescriptor = r.ReadChars(descriptorLen);
            AntennaSetupID = r.U32(8);
            var serialLen = r.U32(8);
            AntennaSerialNumber = r.ReadChars(serialLen);
        }

        public uint RefStationID;
        public string AntennaDescriptor;
        public uint AntennaSetupID;
        public string AntennaSerialNumber;

        /*
Message Number (“1008”=0011 1111 0000) DF002 uint12 12
Reference Station ID DF003 uint12 12
Descriptor Counter N DF029 uint8 8
Antenna Descriptor DF030 char8(N) 8*N N  31
Antenna Setup ID DF031 uint8 8
Serial Number Counter M DF032 uint8 8
Antenna Serial Number DF033 char8(M) 8*M M  31
TOTAL 48+8*(M+N)       
         */
    }
    class RtcmMessage1012 : RtcmMessage
    {
        public RtcmMessage1012(byte[] payload, int index, uint checksum) : base(payload, index, checksum)
        {
            var r = new BitReader(payload);
            r.U32(12); // skip header

            // common header 1009-1012
            RefStationID = r.U32(12);
            GLONASSEpochTime = r.U32(27);
            SynchronousGNSSFLAG = r.U32(1);
            NumGLONASSSatellites = r.U32(5);
            GLONASSDivergenceFreeSmoothingIndicator = r.U32(1);
            GLONASSSmoothingInterval = r.U32(3);

            // 1012 specific
            for (var i = 0; i < NumGLONASSSatellites;++i)
            {
                Sats.Add(new Satellite(r));
            }


        }
        // common header for 1009-1012
        public uint RefStationID,GLONASSEpochTime,NumGLONASSSatellites;
        public uint SynchronousGNSSFLAG, GLONASSDivergenceFreeSmoothingIndicator, GLONASSSmoothingInterval;

        // 1012 specific stuff
        public class Satellite
        {
            public Satellite(BitReader r)
            {
                SatID = r.U32(6);

                CodeIndicator = r.U32(1);
                ChannelNum = r.U32(5);
                L1Pseudorange = r.U32(25);
                L1Phase_L1Pseudorange = r.S32(20);
                L1LockTime = r.U32(7);
                IntegerL1Pseudo = r.U32(7);
                L1CNR = r.U32(8);
                CodeIndicator2 = r.U32(2);
                L2L1PseudorangeDifference = r.U32(14);
                L2PhaseRange_L1PseudoRange = r.S32(20);
                LockTime = r.U32(7);
                L2CNR = r.U32(8);
            }

            public uint SatID, CodeIndicator,ChannelNum, L1Pseudorange, L1LockTime, IntegerL1Pseudo, L1CNR, L2L1PseudorangeDifference,
                CodeIndicator2, LockTime, L2CNR;
            public int L1Phase_L1Pseudorange, L2PhaseRange_L1PseudoRange;




        }
        public List<Satellite> Sats = new();

        /*
GLONASS Satellite ID (Satellite Slot Number) DF038 uint6 6
GLONASS L1 Code Indicator DF039 bit(1) 1
GLONASS Satellite Frequency Channel Number DF040 uint5 5
GLONASS L1 Pseudorange DF041 uint25 25
GLONASS L1 PhaseRange – L1 Pseudorange DF042 int20 20
GLONASS L1 Lock time Indicator DF043 uint7 7
GLONASS Integer L1 Pseudorange Modulus
Ambiguity
DF044 uint7 7
GLONASS L1 CNR DF045 uint8 8
GLONASS L2 Code Indicator DF046 bit(2) 2
GLONASS L2-L1 Pseudorange Difference DF047 uint14 14
GLONASS L2 PhaseRange – L1 Pseudorange DF048 int20 20
GLONASS L2 Lock time Indicator DF049 uint7 7
GLONASS L2 CNR DF050 uint8 8
TOTAL 130 
         
         */

    }
    class RtcmMessage1013 : RtcmMessage
    {
        public RtcmMessage1013(byte[] payload, int index, uint checksum) : base(payload, index, checksum)
        {
            var r = new BitReader(payload);
            r.U32(12); // skip header

            RefID = r.U32(12);
            Julian = r.U32(16);
            Secs = r.U32(17);
            var num = r.U32(5);
            LeapSecs = r.U32(8);

            for (var i =0; i < num; ++i)
                msgs.Add(new(r));
        }

        public uint RefID, Julian, Secs, LeapSecs;
        public List<Msg> msgs = new();
        public class Msg
        {
            public Msg(BitReader r)
            {
                ID = r.U32(12);
                Sync = r.U32(1);
                Trans = r.U32(16);
            }
            public uint ID, Sync, Trans;
        }

        /*
        Message Number DF002 uint12 12
        Reference Station ID DF003 uint12 12
        Modified Julian Day (MJD) Number DF051 uint16 16
        Seconds of Day (UTC) DF052 uint17 17
        No. of Message ID Announcements to Follow (Nm) DF053 uint5 5
        Leap Seconds, GPS-UTC DF054 uint8 8
        Message ID #1 DF055 uint12 12
        Message #1 Sync Flag DF056 bit(1) 1
        Message #1 Transmission Interval DF057 uint16 16
        Message ID #2 DF055 uint12 12
        Message #2 Sync Flag DF056 bit(1) 1
        Message #2 Transmission Interval DF057 uint16 16
        (Repeat until Nm sets)
        TOTAL 70+29* Nm
        */
    }
    class RtcmMessage1014 : RtcmMessage
    {
        public RtcmMessage1014(byte[] payload, int index, uint checksum) : base(payload, index, checksum)
        { 
            var r = new BitReader(payload);
            r.U32(12); // skip header

            NetworkID = r.U32(8);
            SubnetworkID = r.U32(4);
            numStations = r.U32(5);
            MasterRefStationID = r.U32(12);
            AuxRefStationID = r.U32(12);
            AuxMasterDeltaLatitude = r.S32(20);
            AuxMasterDeltaLongitude = r.S32(21);
            AuxMasterDeltaHeight = r.S32(23);
        }

        public uint NetworkID;
        public uint SubnetworkID;
        public uint numStations;
        public uint MasterRefStationID;
        public uint AuxRefStationID;
        public int AuxMasterDeltaLatitude;
        public int AuxMasterDeltaLongitude;
        public int AuxMasterDeltaHeight;


        /*
Message Number DF002 uint12 12 1014
Network ID DF059 uint8 8
Subnetwork ID DF072 uint4 4
Number of Auxiliary Stations Transmitted DF058 uint5 5 0 - 31
Master Reference Station ID DF060 unit12 12
Auxiliary Reference Station ID DF061 uint12 12
Aux-Master Delta Latitude DF062 int20 20
Aux-Master Delta Longitude DF063 int21 21
Aux-Master Delta Height DF064 int23 23
TOTAL 117          
         */
    }

    class Header1015To1017
    {

        // common header for 1015-1017
        public uint NetID, SubId, GPSEpoch, MultIndi, RefId, AuxId, NumSats;

        public Header1015To1017(RtcmMessage.BitReader r)
        {
            // common header 1015-1017
            NetID = r.U32(8);
            SubId = r.U32(4);
            GPSEpoch = r.U32(23);
            MultIndi = r.U32(1);
            RefId = r.U32(12);
            AuxId = r.U32(12);
            NumSats = r.U32(4);
        }

    }

    class RtcmMessage1015 : RtcmMessage
    {
        public RtcmMessage1015(byte[] payload, int index, uint checksum) : base(payload, index, checksum)
        {
            var r = new BitReader(payload);
            r.U32(12); // skip header

            header = new Header1015To1017(r);

            // 1015 specific part
            for (var i  =0; i < header.NumSats; ++i)
                Sats.Add(new Sat(r));
        }

        Header1015To1017 header;

        // 1015 part
        public List<Sat> Sats = new();
        public class Sat
        {
            public Sat(BitReader r)
            {
                SatID = r.U32(6);
                AmbFlag = r.U32(2);
                NonSyncCount = r.U32(3);
                IsoCorr = r.U32(17);
            }
            public uint SatID, AmbFlag, NonSyncCount, IsoCorr;

        }

        /*
         Header part

        Message Number DF002 uint12 12 1015, 1016 or 1017
        Network ID DF059 uint8 8
        Subnetwork ID DF072 uint4 4
        GPS Epoch Time (GPS TOW) DF065 uint23 23
        GPS Multiple Message Indicator DF066 bit(1) 1
        Master Reference Station ID DF060 uint12 12
        Auxiliary Reference Station ID DF061 uint12 12
        # of GPS Sats DF067 uint4 4
        TOTAL 76  

        1015 specific part
GPS Satellite ID DF068 uint6 6
GPS Ambiguity Status Flag DF074 bit(2) 2
GPS Non Sync Count DF075 uint3 3
GPS Ionospheric Carrier Phase Correction
Difference
DF069 int17 17
TOTAL 28         
         
         */
    }
    class RtcmMessage1016 : RtcmMessage
    {
        public RtcmMessage1016(byte[] payload, int index, uint checksum) : base(payload, index, checksum)
        {

            var r = new BitReader(payload);
            r.U32(12); // skip header

            header = new Header1015To1017(r);

            // 1016 specific part
            for (var i = 0; i < header.NumSats; ++i)
                Cors.Add(new Cor(r));

        }
        Header1015To1017 header;

        // 1016 part
        public List<Cor> Cors = new();
        
        public class Cor
        {
            public Cor(BitReader r)
            {
                SatID = r.U32(6);
                AmbFlag = r.U32(2);
                NonSyncCount = r.U32(3);
                PhaseCorr = r.U32(17);
                GPSIODE = r.U32(8);
            }
            public uint SatID, AmbFlag, NonSyncCount, PhaseCorr, GPSIODE;
        }

        /* header same as 1015
         
        1016 specific part
GPS Satellite ID DF068 uint6 6
GPS Ambiguity Status Flag DF074 bit(2) 2
GPS Non Sync Count DF075 uint3 3
GPS Geometric Carrier Phase Correction
Difference
DF070 int17 17
GPS IODE DF071 uint8 8
TOTAL 36          
         */



    }
    class RtcmMessage1029 : RtcmMessage
    {
        public RtcmMessage1029(byte[] payload, int index, uint checksum) : base(payload, index, checksum)
        { // todo parser in here
            throw new NotImplementedException();
        }
        /*
Message Number DF002 uint12 12 1029
Reference Station ID DF003 uint12 12
Modified Julian Day (MJD) Number DF051 uint16 16 (Note 1)
Seconds of Day (UTC) DF052 uint17 17 (Note 1)
Number of Characters to Follow DF138 uint7 7 
        This represents the number of fully formed Unicode
        characters in the message text. It is not necessarily
        the number of bytes that are needed to represent the
        characters as UTF-8. Note that for some messages it
        may not be possible to utilize the full range of this
        field, e.g. where many characters require 3 or 4 byte
        representations and together will exceed 255 code
        units.
Number of UTF-8 Code Units (N) DF139 uint8 8 The length of the message is limited by this field, or
        possibly by DF+1 (see previous note).
UTF-8 Character Code Units DF140 utf8(N) 8*N
TOTAL 72+8*N         */
    }

    class RtcmMessage1033 : RtcmMessage
    {
        public RtcmMessage1033(byte[] payload, int index, uint checksum) : base(payload, index, checksum)
        {
            var r = new BitReader(payload);
            r.U32(12); // skip header

            RefStationID = r.U32(12);
            AntennaDescriptor = r.ReadChars(r.U32(8));
            AntennaSetupID = r.U32(8);
            AntennaSerialNumber = r.ReadChars(r.U32(8));
            ReceriverTypeDescriptor = r.ReadChars(r.U32(8));
            ReceiverFirmwareVersion = r.ReadChars(r.U32(8));
            ReceiverSerialNumber = r.ReadChars(r.U32(8));

        }
        
        public uint RefStationID, AntennaSetupID;
        public string AntennaDescriptor,AntennaSerialNumber,ReceriverTypeDescriptor,ReceiverFirmwareVersion,ReceiverSerialNumber;

    }

    class Header1037To1039
    {

        // common header for 1015-1017
        public uint NetID, SubId, GLONASSEpoch, MultIndi, RefId, AuxId, NumGLONASSEntries;

        public Header1037To1039(RtcmMessage.BitReader r)
        {
            // common header 1015-1017
            NetID = r.U32(8);
            SubId = r.U32(4);
            GLONASSEpoch = r.U32(20);
            MultIndi = r.U32(1);
            RefId = r.U32(12);
            AuxId = r.U32(12);
            NumGLONASSEntries = r.U32(4);
        }

    }


    class RtcmMessage1037 : RtcmMessage
    { // 9.125 + 3.5*Ns bytes
        public RtcmMessage1037(byte[] payload, int index, uint checksum) : base(payload, index, checksum)
        {
            var r = new BitReader(payload);
            r.U32(12); // skip header

            header = new Header1037To1039(r);

            if (payload.Length * 8 < (9 * 8 + 1) + 28 * header.NumGLONASSEntries)
            {
                Trace.TraceError("error parsing message 1037, packet too small");
                return;
            }

            // 1015 specific part
            for (var i = 0; i < header.NumGLONASSEntries; ++i)
                Entries.Add(new Entry(r));

        }

        Header1037To1039 header;
        public List<Entry> Entries = new();

        public class Entry
        {
            public Entry(BitReader r)
            {
                SatID = r.U32(6);
                AmbFlag = r.U32(2);
                NonSyncCount = r.U32(3);
                IsoCorr = r.S32(17);
            }
            public uint SatID, AmbFlag, NonSyncCount;
            public int IsoCorr;
        }

        /*
        Header for 1037, 1038, 1039 
Message Number DF002 uint12 12
Network ID DF059 uint8 8
Subnetwork ID DF072 uint4 4
GLONASS Network Epoch Time DF233 uint20 20
Multiple Message Indicator DF066 bit(1) 1
Master Reference Station ID DF060 uint12 12
Auxiliary Reference Station ID DF061 uint12 12
# of GLONASS Data Entries DF234 uint4 4
TOTAL 73

        1037 specific
GLONASS Satellite ID (Satellite
Slot Number)
DF038 uint6 6
GLONASS Ambiguity Status
Flag
DF235 bit(2) 2
GLONASS Non Sync Count DF236 uint3 3
GLONASS Ionospheric Carrier
Phase Correction Difference
DF237 int17 17
TOTAL 28 

         */
    }
    class RtcmMessage1038 : RtcmMessage
    { // 9.125 + 4.5*Ns
        public RtcmMessage1038(byte[] payload, int index, uint checksum) : base(payload, index, checksum)
        { // todo parser in here
            var r = new BitReader(payload);
            r.U32(12); // skip header

            header = new Header1037To1039(r);

            if (payload.Length * 8 < (9 * 8 + 1) + 36 * header.NumGLONASSEntries)
            {
                Trace.TraceError("error parsing message 1038, packet too small");
                return;
            }

            // 1038 specific part
            for (var i = 0; i < header.NumGLONASSEntries; ++i)
                Entries.Add(new Cor(r));

        }

        Header1037To1039 header;
        public List<Cor> Entries = new();

        public class Cor
        {
            public Cor(BitReader r)
            {
                SatID = r.U32(6);
                AmbFlag = r.U32(2);
                NonSyncCount = r.U32(3);
                PhaseCorr = r.U32(17);
                GPSIODE = r.U32(8);
            }
            public uint SatID, AmbFlag, NonSyncCount, PhaseCorr, GPSIODE;
        }
        /*

        header same as 1037

        1038 specific
GLONASS Satellite ID (Satellite
Slot Number)
DF038 uint6 6
GLONASS Ambiguity Status
Flag
DF235 bit(2) 2
GLONASS Non Sync Count DF236 uint3 3
GLONASS Geometric Carrier
Phase Correction Difference
DF238 int17 17
GLONASS IOD DF239 bit(8) 8
TOTAL 36         */
    }
    class RtcmMessage1230 : RtcmMessage
    {
        public RtcmMessage1230(byte[] payload, int index, uint checksum) : base(payload, index, checksum)
        { 
            var r = new BitReader(payload);
            r.U32(12); // skip header

            RefStationID = r.U32(12);
            GLONASSCodePhaseBias = r.U32(1) > 0;
            r.U32(3); // reserved bits
            GLONASSFMDAMask = r.U32(4); // 4 bits
            if ((GLONASSFMDAMask & 8) != 0) // todo - order correct?
                GLONASS_L1_CA_CodePhaseBias = r.S32(16);
            if ((GLONASSFMDAMask & 4) != 0) // todo - order correct?
                GLONASS_L1_P_CodePhaseBias = r.S32(16);
            if ((GLONASSFMDAMask & 2) != 0) // todo - order correct?
                GLONASS_L2_CA_CodePhaseBias = r.S32(16);
            if ((GLONASSFMDAMask & 1) != 0) // todo - order correct?
                GLONASS_L2_P_CodePhaseBias = r.S32(16);
        }

        public uint RefStationID;
        public bool GLONASSCodePhaseBias;
        public uint GLONASSFMDAMask;
        public int GLONASS_L1_CA_CodePhaseBias;
        public int GLONASS_L1_P_CodePhaseBias;
        public int GLONASS_L2_CA_CodePhaseBias;
        public int GLONASS_L2_P_CodePhaseBias;

        /*
Message Number DF002 uint12 12
Reference Station ID DF003 uint12 12
GLONASS Code-Phase bias
indicator
DF421 bit(1) 1
Reserved DF001 bit(3) 3 Reserved
GLONASS FDMA signals mask DF422 bit(4) 4
GLONASS L1 C/A Code-Phase Bias DF423 int16 16
GLONASS L1 P Code-Phase Bias DF424 int16 16
GLONASS L2 C/A Code-Phase Bias DF425 int16 16
GLONASS L2 P Code-Phase Bias DF426 int16 16
TOTAL 32+16*N N corresponds to the number of bits set to 1
in DF422.         */

    }


}
