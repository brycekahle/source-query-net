using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SourceQuery
{
    [Serializable]
    public class PlayerInfo
    {
        public string Name;
        public int Score;
        public TimeSpan TimeConnected;

        public static PlayerInfo FromBinaryReader(BinaryReader br)
        {
            var playerInfo = new PlayerInfo();
            byte index = br.ReadByte();
            playerInfo.Name = br.ReadAnsiString();
            playerInfo.Score = br.ReadInt32();
            playerInfo.TimeConnected = TimeSpan.FromSeconds(br.ReadSingle());
            return playerInfo;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Name: " + Name);
            sb.AppendLine("Score: " + Score);
            sb.AppendLine("TimeConnected: " + TimeConnected);
            
            return sb.ToString();
        }
    }
}
