using System.Collections.Generic;

/// <summary>
/// More information and source code is available at https://github.com/williamdenton/IoT
/// </summary>
namespace WilliamDenton.IoT.Radio.RDS
{
    public class RDSProgramTypeLookup
    {
        private static readonly Dictionary<byte, string[]> codes;

        private const byte IDX_EU = 0;
        private const byte IDX_NORTH_AMERICA = 1;
        static RDSProgramTypeLookup()
        {
            codes = new Dictionary<byte, string[]>();
            // codes.Add(pty, new string[] { "RDS program type(EU)", "RBDS program type(North America)" });
            codes.Add(1, new string[] { "News", "News" });
            codes.Add(2, new string[] { "Current affairs", "Information" });
            codes.Add(3, new string[] { "Information", "Sports" });
            codes.Add(4, new string[] { "Sport", "Talk" });
            codes.Add(5, new string[] { "Education", "Rock" });
            codes.Add(6, new string[] { "Drama", "Classic rock" });
            codes.Add(7, new string[] { "Culture", "Adult hits" });
            codes.Add(8, new string[] { "Science", "Soft rock" });
            codes.Add(9, new string[] { "Varied", "Top 40" });
            codes.Add(10, new string[] { "Pop  music", "Country" });
            codes.Add(11, new string[] { "Rock music", "Oldies" });
            codes.Add(12, new string[] { "Easy listening", "Soft" });
            codes.Add(13, new string[] { "Light classical", "Nostalgia" });
            codes.Add(14, new string[] { "Serious classical", "Jazz" });
            codes.Add(15, new string[] { "Other music", "Classical" });
            codes.Add(16, new string[] { "Weather", "Rhythm and blues" });
            codes.Add(17, new string[] { "Finance", "Soft rhythm and blues" });
            codes.Add(18, new string[] { "Children’s programmes", "Language" });
            codes.Add(19, new string[] { "Social affairs", "Religious music" });
            codes.Add(20, new string[] { "Religion", "Religious talk" });
            codes.Add(21, new string[] { "Phone-in", "Personality" });
            codes.Add(22, new string[] { "Travel", "Public" });
            codes.Add(23, new string[] { "Leisure", "College" });
            codes.Add(24, new string[] { "Jazz music", "Spanish Talk" });
            codes.Add(25, new string[] { "Country music", "Spanish Music" });
            codes.Add(26, new string[] { "National music", "Hip Hop" });
            codes.Add(27, new string[] { "Oldies music", "Unassigned" });
            codes.Add(28, new string[] { "Folk music", "Unassigned" });
            codes.Add(29, new string[] { "Documentary", "Weather" });
            codes.Add(30, new string[] { "Alarm test", "Emergency test" });
            codes.Add(31, new string[] { "Alarm", "Emergency" });
        }

        private RDSProgramTypeLookup() { }
        public static string GetProgramTypeEU(byte pty)
        {
            return GetProgramType(pty, IDX_EU);
        }

        public static string GetProgramTypeNorthAmerica(byte pty)
        {
            return GetProgramType(pty, IDX_NORTH_AMERICA);
        }

        private static string GetProgramType(byte pty, byte region)
        {
            if (codes.ContainsKey(pty))
            {
                return codes[pty][region];
            }
            return string.Empty;
        }
    }
}
