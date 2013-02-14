using SourceQuery;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace TestApp
{
    class Program
    {
        static void Main(string[] args)
        {
            bool running = true;
            while (running)
            {
                string input = Console.ReadLine().Trim();
                switch (input)
                {
                    case "exit":
                    case "quit":
                        running = false;
                        break;
                    case "list":
                        var ms = new MasterServer(new IPEndPoint(IPAddress.Parse("208.64.200.52"), 27011));
                        var servers = ms.GetServers(Region.World, @"\gamedir\naturalselection2");
                        foreach (var s in servers) Console.WriteLine(s);
                        Console.WriteLine("== END ==");
                        break;
                    default:
                        var addressParts = input.Split(':');
                        var gs = addressParts.Length == 1 ? 
                            new GameServer(IPAddress.Parse(input)) : 
                            new GameServer(new IPEndPoint(IPAddress.Parse(addressParts[0]), int.Parse(addressParts[1])));
                        Console.WriteLine(gs);
                        gs.Players.ForEach(Console.WriteLine);
                        Console.WriteLine(String.Join("\n", gs.Rules.Select(x => String.Format("{0}:{1}", x.Key, x.Value))));
                        Console.WriteLine("DONE");
                        break;
                }
            }
        }
    }
}
