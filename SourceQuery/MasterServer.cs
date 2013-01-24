using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SourceQuery
{
    public class MasterServer
    {
        private IPEndPoint _endpoint;
        private static IPEndPoint AnyIpEndPoint = new IPEndPoint(IPAddress.Any, 0);

        public MasterServer(IPAddress address)
            : this(new IPEndPoint(address, 27015))
        {            
        }

        public MasterServer(IPEndPoint endpoint)
        {
            _endpoint = endpoint;            
        }

        public List<IPEndPoint> GetServers(Region region, string filter = null)
        {
            using (var client = new UdpClient())
            {
                var servers = new List<IPEndPoint>();
                var anyEndpoint = new IPEndPoint(IPAddress.Any, 0);

                do
                {
                    var lastEndpoint = servers.LastOrDefault() ?? anyEndpoint;
                    var query = new List<byte> { 0x31, (byte)region };
                    query.AddRange(Encoding.ASCII.GetBytes(lastEndpoint.ToString()));
                    query.Add(0); // ip termination

                    if (!String.IsNullOrWhiteSpace(filter)) query.AddRange(Encoding.ASCII.GetBytes(filter));
                    query.Add(0);

                    client.Send(query.ToArray(), query.Count, _endpoint);
                    var serverData = client.Receive(ref AnyIpEndPoint);

                    using (var br = new BinaryReader(new MemoryStream(serverData)))
                    {
                        if (br.ReadInt32() != -1 || br.ReadInt16() != 0x0A66) return servers;

                        while (br.BaseStream.Position < br.BaseStream.Length)
                        {
                            var ipBytes = br.ReadBytes(4);
                            var port = (ushort)IPAddress.NetworkToHostOrder(br.ReadInt16());

                            var server = new IPEndPoint(new IPAddress(ipBytes), port);
                            servers.Add(server);
                        }
                    }
                } while (servers.Count > 0 && !servers.Last().Equals(anyEndpoint));

                servers.Remove(anyEndpoint);
                return servers;
            }
        }
    }

    public enum Region : byte
    {
        UsEast = 0x00,
        UsWest = 0x01,
        World = 0xFF,
    }
}
