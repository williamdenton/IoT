using System;

/// <summary>
/// More information and source code is available at https://github.com/williamdenton/IoT
/// </summary>
namespace WilliamDenton.IoT.Radio
{
    public struct Si4700Info
    {
        public UInt16 PN { get; set; }
        public UInt16 MFGID { get; set; }
        public UInt16 REV { get; set; }
        public UInt16 DEV { get; set; }
        public UInt16 FIRMWARE { get; set; }

        public string Device
        {
            get
            {
                switch (DEV)
                {
                    case 0x00: // 0000:
                        return "Si4700";
                    case 0x01: // 0001:
                        return "Si4702";
                    case 0x08: // 1000:
                        return "Si4701";
                    case 0x09: // 1001:
                        return "Si4703";
                    default:
                        return "";
                }
            }
        }
    }
}
