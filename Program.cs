using Network;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace PiServer
{
    static class Program
    {
        private static ServerTcp server = new ServerTcp(100, 1024 * 4);
        private static System.Text.Encoding utf8 = System.Text.Encoding.UTF8;
        private delegate Task requestHandler(string wantedElement, ReceiveResult receiveResult);
        private static Dictionary<string, requestHandler> requestHandlers = new Dictionary<string, requestHandler>{
            {"/", SendIndex},

        };

        static async Task Main(string[] args)
        {
            int port = 80;

            if(args.Length != 0 && int.TryParse(args[0], out int value))
                port = value;

            server.onAccept += OnAccept;
            server.onSend += onSend;
            server.onClientClosed += OnClientClosed;
            var listeningTask = server.StartListening(port);

            while(true)
            {
                Console.WriteLine("Server listening on port {0}\nInput \"stop\" to stop server", port);
                string input = Console.ReadLine();
                if(input == "stop")
                    break;
            }
            server.Shutdown();
            await listeningTask;
        }

        private static async void OnAccept(TcpClient client)
        {
            Console.WriteLine("{0} connected", client.Client.RemoteEndPoint);
            var receiveTask = server.ReceiveAsync(client);
            Task[] tasks = new Task[] { receiveTask, Task.Delay(10000) };
            Task finishedTask = await Task.WhenAny(tasks);
            if (!receiveTask.Equals(finishedTask))
                server.CloseClientSocket(client);
            await OnReceive(receiveTask.Result);
            server.CloseClientSocket(client);
        }

        private static async Task OnReceive(ReceiveResult rr)
        {
            /*if (rr.size == 0)
            {
                server.CloseClientSocket(server.GetClient(rr.remoteEndPoint));
                return;
            }*/

            Console.WriteLine("ep:{0} size:{1} socket type:{2}", rr.remoteEndPoint, rr.size, rr.socketType);
            string receivedMsg = utf8.GetString(rr.buffer);

            byte[] code = utf8.GetBytes("HTTP/1.1 200 ok \r\n\r\n");
            string wantedElement = GetWantedElement(receivedMsg);
            Console.WriteLine(receivedMsg);//wantedElement);
            try
            {
                if(requestHandlers.TryGetValue(wantedElement, out requestHandler value))
                    await value.Invoke(wantedElement, rr);
                else
                    await server.SendFile("Assets" + wantedElement, rr.remoteEndPoint, code);                
            }
            catch (Exception e) when (ExceptionFilter(e, server, rr)) { }
        }
        private static bool ExceptionFilter(Exception e, Server server, ReceiveResult rr)
        {
            if (e is SocketException)
            {
                var s = e as SocketException;
                Console.WriteLine("Socket exception code: " + s.ErrorCode);
                return true;
            }
            else if (e is DirectoryNotFoundException || e is FileNotFoundException || e is UnauthorizedAccessException)
            {
                byte[] code = System.Text.Encoding.UTF8.GetBytes("HTTP/1.1 404 not found \r\n\r\n");
                server.Send(code, rr.remoteEndPoint);
                Console.WriteLine(e.Message);
                return true;
            }

            return false;
        }

        private static async Task SendIndex(string element, ReceiveResult rr)
        {
            byte[] code = utf8.GetBytes("HTTP/1.1 200 ok \r\n\r\n");
            await server.SendFile("Assets/html/index.html", rr.remoteEndPoint, code);
        }

        private static void OnClientClosed(TcpClient client)
        {
            Console.WriteLine("Removed {0}, {1} remaining", client, server.ConnectedClients());
        }

        
        private static string GetWantedElement(string msg)
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
