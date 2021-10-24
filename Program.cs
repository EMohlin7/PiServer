using Network;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace PiServer
{
    static class Program
    {
        static ServerTcp server = new ServerTcp(20, 1024 * 4);
        static System.Text.Encoding utf8 = System.Text.Encoding.UTF8;
        static void Main(string[] args)
        {
            server.onAccept += OnAccept;
            server.onReceive += OnReceive;
            server.onSend += onSend;
            server.onClientClosed += OnClientClosed;
            server.StartListening(80);

            while(true)
            {
                Console.WriteLine("Input \"stop\" to stop server");
                string input = Console.ReadLine();
                if(input == "stop")
                    break;
            }
            server.Shutdown();

            
        }

        private static async void OnAccept(IPEndPoint ep)
        {
            Console.WriteLine("{0} connected", ep);
            Task receiveTask = server.ReceiveAsync(ep);
            Task[] tasks = new Task[] { receiveTask, Task.Delay(10000) };
            Task finishedTask = await Task.WhenAny(tasks);
            if (!receiveTask.Equals(finishedTask))
                server.CloseClientSocket(ep);
        }

        private static async void OnReceive(ReceiveResult rr)
        {

            if (rr.size == 0)
            {
                server.CloseClientSocket(rr.remoteEndPoint);
                return;
            }

            Console.WriteLine("ep:{0} size:{1} socket type:{2}", rr.remoteEndPoint, rr.size, rr.socketType);
            string receivedMsg = utf8.GetString(rr.buffer);


            byte[] code = utf8.GetBytes("HTTP/1.1 200 ok \r\n\r\n");
            string wantedElement = ParseMsg(receivedMsg);
            Console.WriteLine(receivedMsg);//wantedElement);
            try
            {
                await server.SendFile("Assets/" + wantedElement, rr.remoteEndPoint, code);
            }
            catch (Exception e) when (ExceptionFilter(e, server, rr)) { }
            finally { server.CloseClientSocket(rr.remoteEndPoint); }
        }

        private static bool ExceptionFilter(Exception e, Server server, ReceiveResult rr)
        {
            if (e is SocketException)
            {
                var s = e as SocketException;
                Console.WriteLine("Socket exception code: " + s.ErrorCode);
                return true;
            }
            else if (e is System.IO.DirectoryNotFoundException || e is System.IO.FileNotFoundException)
            {
                byte[] code = System.Text.Encoding.UTF8.GetBytes("HTTP/1.1 404 not found \r\n\r\n");
                server.Send(code, rr.remoteEndPoint);
                Console.WriteLine(e.Message);
                return true;
            }

            return false;
        }


        private static void OnClientClosed(IPEndPoint ep)
        {
            Console.WriteLine("Removed {0}, {1} remaining", ep, server.ConnectedClients());
        }

        
        private static string ParseMsg(string msg)
        {
            string[] strings = msg.Split("\r\n");
            string get = strings[0];
            return get.Split(" ")[1];
        }

        private static void onSend(long length, IPEndPoint ep)
        {
            Console.WriteLine("Sent {0} bytes to {1}", length, ep);
        }
    }
}
