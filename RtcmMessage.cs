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
    }

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

    class RtcmMessage1004 : RtcmMessage
    { 
        public RtcmMessage1004(byte[] payload, int index, uint checksum) : base(payload, index, checksum)
        { // todo parser in here
        }
    }
    class RtcmMessage1006 : RtcmMessage
    {
        public RtcmMessage1006(byte[] payload, int index, uint checksum) : base(payload, index, checksum)
        { // todo parser in here
        }
    }
    class RtcmMessage1008 : RtcmMessage
    {
        public RtcmMessage1008(byte[] payload, int index, uint checksum) : base(payload, index, checksum)
        { // todo parser in here
        }
    }
    class RtcmMessage1012 : RtcmMessage
    {
        public RtcmMessage1012(byte[] payload, int index, uint checksum) : base(payload, index, checksum)
        { // todo parser in here
        }
    }
    class RtcmMessage1013 : RtcmMessage
    {
        public RtcmMessage1013(byte[] payload, int index, uint checksum) : base(payload, index, checksum)
        { // todo parser in here
        }
    }
    class RtcmMessage1014 : RtcmMessage
    {
        public RtcmMessage1014(byte[] payload, int index, uint checksum) : base(payload, index, checksum)
        { // todo parser in here
        }
    }
    class RtcmMessage1015 : RtcmMessage
    {
        public RtcmMessage1015(byte[] payload, int index, uint checksum) : base(payload, index, checksum)
        { // todo parser in here
        }
    }
    class RtcmMessage1016 : RtcmMessage
    {
        public RtcmMessage1016(byte[] payload, int index, uint checksum) : base(payload, index, checksum)
        { // todo parser in here
        }
    }
    class RtcmMessage1033 : RtcmMessage
    {
        public RtcmMessage1033(byte[] payload, int index, uint checksum) : base(payload, index, checksum)
        { // todo parser in here
        }
    }
    class RtcmMessage1037 : RtcmMessage
    {
        public RtcmMessage1037(byte[] payload, int index, uint checksum) : base(payload, index, checksum)
        { // todo parser in here
        }
    }
    class RtcmMessage1038 : RtcmMessage
    {
        public RtcmMessage1038(byte[] payload, int index, uint checksum) : base(payload, index, checksum)
        { // todo parser in here
        }
    }
    class RtcmMessage1230 : RtcmMessage
    {
        public RtcmMessage1230(byte[] payload, int index, uint checksum) : base(payload, index, checksum)
        { // todo parser in here
        }
    }


}
