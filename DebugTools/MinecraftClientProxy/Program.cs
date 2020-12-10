using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;
using MinecraftClient.Protocol.Handlers;

namespace MinecraftClientProxy
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Waiting for client on port 25565...");
            TcpListener listener = new TcpListener(IPAddress.Any, 25565);
            listener.Start();
            TcpClient client = listener.AcceptTcpClient();
            
            Console.WriteLine("Connecting to server on port 25565...");
            TcpClient server = new TcpClient("proxima.theminers.id", 25565);
            //TcpClient server = new TcpClient("temp.theminers.id", 25565);

            Console.WriteLine("Starting proxy...\n");
            new PacketProxy(client, server).Run();

            Console.ReadLine();
        }
    }
}
