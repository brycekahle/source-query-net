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

        public IEnumerable<IPEndPoint> GetServers(Region region, string filter = null)
        {
            using (var client = new UdpClient())
            {
                int serverCount;                
                var anyEndpoint = new IPEndPoint(IPAddress.Any, 0);
                var lastEndpoint = anyEndpoint;

                do
                {
                    serverCount = 0;
                    var query = new List<byte> { 0x31, (byte)region };
                    query.AddRange(Encoding.ASCII.GetBytes(lastEndpoint.ToString()));
                    query.Add(0); // ip termination

                    if (!String.IsNullOrWhiteSpace(filter)) query.AddRange(Encoding.ASCII.GetBytes(filter));
                    query.Add(0); // filter termination

                    client.Send(query.ToArray(), query.Count, _endpoint);
                    var serverData = client.Receive(ref AnyIpEndPoint);

                    using (var br = new BinaryReader(new MemoryStream(serverData)))
                    {
                        if (br.ReadInt32() != -1 || br.ReadInt16() != 0x0A66) yield break;

                        while (br.BaseStream.Position < br.BaseStream.Length)
                        {
                            var ipBytes = br.ReadBytes(4);
                            var port = (ushort)IPAddress.NetworkToHostOrder(br.ReadInt16());

                            var server = new IPEndPoint(new IPAddress(ipBytes), port);
                            if (server.Equals(anyEndpoint)) yield break;
                            yield return server;

                            lastEndpoint = server;
                            serverCount++;
                        }
                    }
                } while (serverCount > 0);
            }
        }
    }

    public enum Region : byte
    {
        UsEast = 0x00,
        UsWest = 0x01,
        SouthAmerica = 0x02,
        Europe = 0x03,
        Asia = 0x04,
        Australia = 0x05,
        MiddleEast = 0x06,
        Africa = 0x07,
        World = 0xFF,
    }
}
