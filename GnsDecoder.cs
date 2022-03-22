﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Lomont.Gps
{
    public static class GnsDecoder
    {
        public static (int successful, int failed) Parse(IEnumerable<string> lines, List<Message> messages)
        {
            int successful = 0, failed = 0, lineCount = 0;
            bool seekLine = true;
            foreach (var line1 in lines)
            {
                var line = line1 + "\r\n"; // endlines were stripped, pass into decoder
                if (seekLine)
                { // skip any initial noise, get to first $
                    if (line.Contains("$"))
                        line = line.Substring(line.IndexOf('$'));
                    if (!line.StartsWith("$"))
                        continue;
                    seekLine = false;
                }

                ++lineCount;
                var message = TryParse(line);
                if (message != null)
                {
                    messages.Add(message);
                    successful++;
                }
                else
                {
                    // todo - last line sometimes clipped - ignore? warn?
                    Trace.TraceError($"Error parsing GNS line {lineCount}:{line}");
                    failed++;
                }
            }
            return (successful, failed);

        }

        public static (int successful, int failed) Parse(string filename, List<Message> messages)
        {
            Trace.WriteLine($"Parsing GNS {filename}");
            if (!File.Exists(filename))
            {
                Trace.TraceError($"File {filename} not found");
                return (0, 0);
            }

            return Parse(File.ReadAllLines(filename),messages);
        }

        public static GnsMessage TryParse(string text)
        {
            const int minLength = 1 + 5 + 1 + 2 + 2; // "$ABCDE*12\r\n"
            if (string.IsNullOrEmpty(text) || text.Length < minLength)
                return null;
            var len = text.Length;
            if (text[0] != '$')
                return null;
            if (text[len - 2] != '\r' || text[len - 1] != '\n')
                return null;
            if (text[len - 5] != '*')
                return null;
            var checksumText = text.Substring(1, len - 6);
            var computedChecksum = 0;
            foreach (var c in checksumText)
                computedChecksum ^= (byte)c;
            if (!TryParseChecksum(text.Substring(len - 4, 2), out int readChecksum))
                return null;
            if (computedChecksum != readChecksum)
                return null;

            // get fields, no initial comma
            var fieldsText = text.Substring(7, len - 6 - 5 - 1);

            var addr = text.Substring(1, 5);
            var sys = addr.Substring(0, 2);
            var type = addr.Substring(2);
            GnsSystem gnsSystem = GnsSystem.UNKNOWN;
            GnsMessageType gnsMessageType = GnsMessageType.UNKNOWN;

            switch (sys)
            {
                case "GP":
                    gnsSystem = GnsSystem.GPS;
                    break;
                case "GN":
                    gnsSystem = GnsSystem.COMBO;
                    break;
                case "GL":
                    gnsSystem = GnsSystem.GLONASS;
                    break;
                case "GB":
                    gnsSystem = GnsSystem.BEIDOU;
                    break;
                case "GA":
                    gnsSystem = GnsSystem.GALILEO;
                    break;
                default:
                    Trace.TraceError($"Unknown message system {sys} in {text}");
                    break;
            }

            GnsMessage gnsMessage = null;
            switch (type)
            {
                case "GSV": // satellites in view
                    gnsMessageType = GnsMessageType.GSV;
                    break;
                case "GSA": // GPS DOP and active satellites
                    gnsMessageType = GnsMessageType.GSA;
                    break;
                case "VTG": // VTG - Track made good and Ground speed
                    gnsMessageType = GnsMessageType.VTG;
                    break;
                case "GLL": // GLL - Geographic Position - Latitude/Longitude
                    // fieldsText = ",,,,,220610.00,V,";// test 
                    if (!GllGnsMessage.TryParse(fieldsText, out var gllMessage))
                        return null;
                    gnsMessage = gllMessage;
                    gnsMessageType = GnsMessageType.GLL;
                    break;
                case "RMC": // RMC - Recommended Minimum Navigation Information
                    // fieldsText = "220611.00,V,,,,,,,221120,,,N,V";
                    if (!RmcGnsMessage.TryParse(fieldsText, out var rmcMessage))
                        return null;
                    gnsMessage = rmcMessage;
                    gnsMessageType = GnsMessageType.RMC;
                    break;
                case "GGA": // GGA - Global Positioning System Fix Data
                    // $GNGGA,001043.00,4404.14036,N,12118.85961,W,1,12,0.98,1113.0,M,-21.3,M,,*47
                    if (!GgaGnsMessage.TryParse(fieldsText, out var ggaMessage))
                        return null;
                    gnsMessage = ggaMessage;
                    gnsMessageType = GnsMessageType.GGA;
                    break;
                case "TXT": // TXT - Text messages
                    gnsMessageType = GnsMessageType.TXT;
                    break;
                default:
                    Trace.TraceError($"Unknown message type {type} in {text}");
                    return null;
            }

            gnsMessage ??= new GnsMessage();

            gnsMessage.Text = text;
            gnsMessage.GnsSystem = gnsSystem;
            gnsMessage.GnsMessageType = gnsMessageType;

            return gnsMessage;
        }

        static bool TryParseChecksum(string text, out int readChecksum)
        {
            bool TryParseHex(char ch, out int digit)
            {
                digit = 0;
                if ('0' <= ch && ch <= '9')
                {
                    digit = ch - '0';
                    return true;
                }
                else if ('A' <= ch && ch <= 'F')
                {
                    digit = ch - 'A' + 10;
                    return true;
                }

                return false;

            }
            readChecksum = 0;
            if (String.IsNullOrEmpty(text) || text.Length != 2)
                return false;
            text = text.ToUpper();
            if (!TryParseHex(text[0], out int d1) || !TryParseHex(text[1], out int d2))
                return false;
            readChecksum = d1 * 16 + d2;
            return true;
        }




    }
}
