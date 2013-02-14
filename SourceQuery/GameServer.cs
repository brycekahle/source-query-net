using ICSharpCode.SharpZipLib.BZip2;
using ICSharpCode.SharpZipLib.Checksums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SourceQuery
{
    [Serializable]
    public class GameServer
    {
        [NonSerialized]
        private IPEndPoint _endpoint;
        [NonSerialized]
        private UdpClient _client;

        // TSource Engine Query
        [NonSerialized]
        private static readonly byte[] A2S_INFO = { 0x54, 0x53, 0x6F, 0x75, 0x72, 0x63, 0x65, 0x20, 0x45, 0x6E, 0x67, 0x69, 0x6E, 0x65, 0x20, 0x51, 0x75, 0x65, 0x72, 0x79, 0x00 };
        [NonSerialized]
        private static readonly byte[] A2S_SERVERQUERY_GETCHALLENGE = { 0x55, 0xFF, 0xFF, 0xFF, 0xFF };
        [NonSerialized]
        private static readonly byte A2S_PLAYER = 0x55;
        [NonSerialized]
        private static readonly byte A2S_RULES = 0x56;
        [NonSerialized]
        private byte[] _challengeBytes;

        public byte NetworkVersion;
        public string Name;
        public string Map;
        public string GameDirectory;
        public string GameDescription;
        public short AppId;
        public byte PlayerCount;
        public byte MaximumPlayerCount;
        public byte BotCount;
        public ServerType ServerType;
        public OperatingSystem OS;
        public bool RequiresPassword;
        public bool VACSecured;
        public string GameVersion;
        public short Port = 27015;
        public string SteamId;
        public short SpectatorPort;
        public string SpectatorName;
        public string GameTagData;
        public string GameID;

        public List<PlayerInfo> Players { get; set; }
        public Dictionary<string, string> Rules { get; set; }
        public string Endpoint { get; set; }

        public GameServer()
        {
            Players = new List<PlayerInfo>();
            Rules = new Dictionary<string, string>();
        }

        public GameServer(IPAddress address)
            : this(new IPEndPoint(address, 27015))
        {            
        }

        public GameServer(IPEndPoint endpoint)
            : this()
        {
            _endpoint = endpoint;
            Endpoint = endpoint.ToString();

            using (_client = new UdpClient())
            {
                _client.Client.SendTimeout = (int)500;
                _client.Client.ReceiveTimeout = (int)500;
                _client.Connect(endpoint);

                RefreshMainInfo();
                RefreshPlayerInfo();
                RefreshRules();
            }
            _client = null;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Name: " + Name);
            sb.AppendLine("Map: " + Map);
            sb.AppendLine("Players: " + PlayerCount + "/" + MaximumPlayerCount);
            sb.AppendLine("ServerType: " + ServerType);
            sb.AppendLine("OS: " + OS);
            sb.AppendLine("Port: " + Port);
            sb.AppendLine("GameData: " + GameTagData);
            return sb.ToString();
        }

        public void RefreshMainInfo()
        {
            Send(A2S_INFO);
            var infoData = Receive();
            using (var br = new BinaryReader(new MemoryStream(infoData)))
            {
                br.ReadByte(); // type byte, not needed

                NetworkVersion = br.ReadByte();
                Name = br.ReadAnsiString();
                Map = br.ReadAnsiString();
                GameDirectory = br.ReadAnsiString();
                GameDescription = br.ReadAnsiString();
                AppId = br.ReadInt16();
                PlayerCount = br.ReadByte();
                MaximumPlayerCount = br.ReadByte();
                BotCount = br.ReadByte();
                ServerType = (ServerType)br.ReadByte();
                OS = (OperatingSystem)br.ReadByte();
                RequiresPassword = br.ReadByte() == 0x01;
                VACSecured = br.ReadByte() == 0x01;
                GameVersion = br.ReadAnsiString();
                var edf = (ExtraDataFlags)br.ReadByte();

                if (edf.HasFlag(ExtraDataFlags.GamePort)) Port = br.ReadInt16();
                if (edf.HasFlag(ExtraDataFlags.SteamID)) SteamId = br.ReadUInt64().ToString();
                if (edf.HasFlag(ExtraDataFlags.SpectatorInfo))
                {
                    SpectatorPort = br.ReadInt16();
                    SpectatorName = br.ReadAnsiString();
                }
                if (edf.HasFlag(ExtraDataFlags.GameTagData)) GameTagData = br.ReadAnsiString();
                if (edf.HasFlag(ExtraDataFlags.GameID)) GameID = br.ReadUInt64().ToString();
            }
        }

        public void RefreshPlayerInfo()
        {
            Players.Clear();
            GetChallengeData();

            _challengeBytes[0] = A2S_PLAYER;
            Send(_challengeBytes);
            var playerData = Receive();
            
            using (var br = new BinaryReader(new MemoryStream(playerData)))
            {
                if (br.ReadByte() != 0x44) throw new Exception("Invalid data received in response to A2S_PLAYER request");
                var numPlayers = br.ReadByte();
                for (int index = 0; index < numPlayers; index++)
                {
                    Players.Add(PlayerInfo.FromBinaryReader(br));
                }
            }
        }

        public void RefreshRules()
        {
            Rules.Clear();
            GetChallengeData();

            _challengeBytes[0] = A2S_RULES;
            Send(_challengeBytes);
            var ruleData = Receive();

            using (var br = new BinaryReader(new MemoryStream(ruleData)))
            {
                if (br.ReadByte() != 0x45) throw new Exception("Invalid data received in response to A2S_RULES request");
                var numRules = br.ReadUInt16();
                for (int index = 0; index < numRules; index++)
                {
                    Rules.Add(br.ReadAnsiString(), br.ReadAnsiString());
                }
            }
        }

        private void GetChallengeData()
        {
            if (_challengeBytes != null) return;
            
            Send(A2S_SERVERQUERY_GETCHALLENGE);
            var challengeData = Receive();
            if (challengeData[0] != 0x41) throw new Exception("Unable to retrieve challenge data");
            _challengeBytes = challengeData;            
        }

        private void Send(byte[] message)
        {
            var fullmessage = new byte[4 + message.Length];
            fullmessage[0] = fullmessage[1] = fullmessage[2] = fullmessage[3] = 0xFF;

            Buffer.BlockCopy(message, 0, fullmessage, 4, message.Length);
            _client.Send(fullmessage, fullmessage.Length);
        }

        private byte[] Receive()
        {
            byte[][] packets = null;
            byte packetNumber = 0, packetCount = 1;
            bool usesBzip2 = false;
            int crc = 0;

            do
            {
                var result = _client.Receive(ref _endpoint);
                using (var br = new BinaryReader(new MemoryStream(result)))
                {
                    if (br.ReadInt32() == -2)
                    {
                        int requestId = br.ReadInt32();
                        usesBzip2 = (requestId & 0x80000000) == 0x80000000;
                        packetNumber = br.ReadByte();
                        packetCount = br.ReadByte();
                        int splitSize = br.ReadInt32();

                        if (usesBzip2 && packetNumber == 0)
                        {
                            int decompressedSize = br.ReadInt32();
                            crc = br.ReadInt32();
                        }
                    }

                    if (packets == null) packets = new byte[packetCount][];

                    var data = new byte[result.Length - br.BaseStream.Position];
                    Buffer.BlockCopy(result, (int)br.BaseStream.Position, data, 0, data.Length);
                    packets[packetNumber] = data;
                }
            } while (packets.Any(p => p == null));

            var combinedData = Combine(packets);
            if (usesBzip2)
            {
                combinedData = Decompress(combinedData);
                Crc32 crc32 = new Crc32();
                crc32.Update(combinedData);
                if (crc32.Value != crc) throw new Exception("Invalid CRC for compressed packet data");
                return combinedData;
            }
            return combinedData;
        }

        private byte[] Decompress(byte[] combinedData)
        {
            using (var compressedData = new MemoryStream(combinedData))
            using (var uncompressedData = new MemoryStream())
            {
                BZip2.Decompress(compressedData, uncompressedData, true);
                return uncompressedData.ToArray();
            }
        }

        private byte[] Combine(byte[][] arrays)
        {
            var rv = new byte[arrays.Sum(a => a.Length)];
            int offset = 0;
            foreach (byte[] array in arrays)
            {
                Buffer.BlockCopy(array, 0, rv, offset, array.Length);
                offset += array.Length;
            }
            return rv;
        }
    }    
}
