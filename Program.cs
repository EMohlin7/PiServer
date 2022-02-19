using Network;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using HTTPParser;

namespace PiServer
{
    static class Program
    {
        private static string apiAddress = "127.0.0.1";
        private static int apiPort = 8080;
        private static ServerTcp server = new ServerTcp(100, 1024 * 4);
        private static System.Text.Encoding utf8 = System.Text.Encoding.UTF8;
        private delegate Task requestHandler(Request request, ReceiveResult receiveResult);
        private static Dictionary<string, requestHandler> requestHandlers = new Dictionary<string, requestHandler>{
            {"/", SendIndex},
            {"/video.mp4", SendVideo},
            {"/video2.mp4", SendVideo},
            {"/pcStarter", PcStarter},
            {"/login", Login},
        };

        static async Task Main(string[] args)
        {
            var req = new Request();
            req.SetHeader("Hej", "da");
            if(req.HeaderExists("hej"))
                Console.WriteLine("insens");
            else
                Console.WriteLine("Sensitive");
            int port = 80;

            if(args.Length != 0 && int.TryParse(args[0], out int value))
                port = value;

            server.onAccept += OnAccept;
            server.onSend += OnSend;
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
            Task[] tasks = new Task[] { receiveTask, Task.Delay(3000) };
            Task finishedTask = await Task.WhenAny(tasks);

            if (!receiveTask.Equals(finishedTask))
            {
                server.CloseClientSocket(client);
                return;
            }

            await OnReceive(receiveTask.Result);
            server.CloseClientSocket(client);
        }

        private static async Task OnReceive(ReceiveResult rr)
        {
            if (rr.size == 0)
            {
                server.CloseClientSocket(server.GetClient(rr.remoteEndPoint));
                return;
            }

            Console.WriteLine("ep:{0} size:{1} socket type:{2}", rr.remoteEndPoint, rr.size, rr.socketType);
            string receivedMsg = utf8.GetString(rr.buffer);
            Console.WriteLine(receivedMsg);
            var req = new Request(receivedMsg);
            var res = new Response();
            res.code = 200;
                       
            try
            {
                if(requestHandlers.TryGetValue(req.element, out requestHandler value))
                    await value.Invoke(req, rr);
                else
                    await server.SendFile("Assets" + req.element, rr.remoteEndPoint, 0, null, utf8.GetBytes(res.GetMsg()));                
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

        private static Task SendIndex(Request req, ReceiveResult rr)
        {
            byte[] code = utf8.GetBytes("HTTP/1.1 200 ok \r\n\r\n");
            return server.SendFile("Assets/html/index.html", rr.remoteEndPoint, 0, null, code);
        }

        private static Task SendVideo(Request req, ReceiveResult rr)
        {
            var res = new Response();
            long offset = 0;
            long? end = null;
            if(req.HeaderExists("range"))
            {
                long fileLength = new FileInfo("Assets/" + req.element).Length;
                string[] bytes = req.GetHeader("range").Split("=")[1].Split("-");
                offset = long.Parse(bytes[0]);
                end = bytes[1].Length > 0 ? long.Parse(bytes[1]) : null;
                
                res.code = 206;
                res.SetHeader("Content-Type", "video/mp4");
                res.SetHeader("Content-Range", string.Format("bytes {0}-{1}/{2}", offset, end ?? fileLength-1, fileLength));
                res.SetHeader("Content-Length", string.Format("{0}", (end ?? fileLength-1) - offset+1));
            }

            return server.SendFile("Assets/"+req.element, rr.remoteEndPoint, offset, end, utf8.GetBytes(res.GetMsg()));
        }

        private static async Task<Task> PcStarter(Request req, ReceiveResult rr)
        {
            ClientTcp client = new ClientTcp(2048);
            if(!await client.Connect(apiAddress, apiPort))
                return SendInternalServerError(rr.remoteEndPoint);
            
            var re = new Request();
            re.method = "GET";
            re.element = "/pcStarter";
            re.SetHeader("Host", "localhost:80");
            re.SetHeader("Connection", "keep-alive");
           
            var bytes = utf8.GetBytes(re.GetMsg());
            client.Send(bytes, bytes.Length);
            ReceiveResult apiRR = await client.ReceiveAsync();
            client.Shutdown();

            if(!apiRR.success)
                return SendInternalServerError(rr.remoteEndPoint);
            
            return server.SendAsync(apiRR.buffer, rr.remoteEndPoint);
        }

        private static async Task<Task> Login(Request req, ReceiveResult rr)
        {
            var api = new ClientTcp(2048);
            if(!await api.Connect(apiAddress, apiPort))
                return SendInternalServerError(rr.remoteEndPoint);

            var apiReq = new Request();
            apiReq.element = "/login";
            apiReq.method = "POST";
            apiReq.body = req.body;
            apiReq.SetHeader("Host", "localhost:80");
            apiReq.SetHeader("Connection", "keep-alive");
            apiReq.SetHeader("Content-Type", "application/json");
            apiReq.SetHeader("Content-Length", apiReq.body.Length.ToString());

            var bytes = utf8.GetBytes(apiReq.GetMsg());
            api.Send(bytes, bytes.Length);
            var apiRR = await api.ReceiveAsync();
            api.Shutdown();
            if(!apiRR.success)
                return SendInternalServerError(rr.remoteEndPoint);
            var apiRes = new Response(utf8.GetString(apiRR.buffer));
            if(apiRes.code != 200)
                return server.SendAsync(apiRR.buffer, rr.remoteEndPoint);
            
            var res = new Response(apiRes.GetMsg());
            res.code = 303;
            res.SetHeader("Location", "/");
            Console.WriteLine(utf8.GetString(apiRR.buffer));
            Console.WriteLine(res.GetMsg());
            return server.SendAsync(utf8.GetBytes(res.GetMsg()), rr.remoteEndPoint);
        }


        private static Task SendBadRequest(IPEndPoint remoteEndPoint)
        {
            return server.SendAsync(utf8.GetBytes(new Response(400).GetMsg()), remoteEndPoint);
        }
        private static Task SendInternalServerError(IPEndPoint remoteEndPoint)
        {
            return server.SendAsync(utf8.GetBytes(new Response(500).GetMsg()), remoteEndPoint);
        }

        private static void OnClientClosed(TcpClient client)
        {
            Console.WriteLine("Removed {0}, {1} remaining", client, server.ConnectedClients());
        }

        
        
        private static void OnSend(long length, IPEndPoint ep)
        {
            Console.WriteLine("Sent {0} bytes to {1}", length, ep);
        }
    }
}
