using Network;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using HTTPParser;
using System.Threading;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using System.Runtime.ConstrainedExecution;

namespace PiServer
{
    static class Program
    {
        private static string apiAddress = "127.0.0.1";
        private const int apiPort = 8080;
        private static int port;
        private static ServerTcp server = new ServerTcp(100, 1024 * 4);
        private static ServerTcpSSL sslServer = new ServerTcpSSL(test(), 100, 1024 * 4);
        private static System.Text.Encoding utf8 = System.Text.Encoding.UTF8;
        private delegate Task requestHandler(Request request, TcpReceiveResult receiveResult);
        private static Dictionary<string, requestHandler> requestHandlers = new Dictionary<string, requestHandler>{
            {"/", SendIndex},
            {"/user_login", UserLogin},
            {"/video.mp4", SendVideo},
            {"/video2.mp4", SendVideo},
            {"/pcStarter", PcStarter},
            {"/login", Login},
            {"/createaccount", CreateAccountPage},
            {"/newaccount", NewAccount},
        };
        static X509Certificate2 test()
        {
            using var cert = X509Certificate2.CreateFromPemFile("fullchain.pem", "privkey.pem");
            return new X509Certificate2(cert.Export(X509ContentType.Pkcs12));
        }
        static void Main(string[] args)
        {
            port = 80;
            
            if (args.Length != 0 && int.TryParse(args[0], out int value))
                port = value;

            server.clientAccepted += OnAccept;
            server.onSend += OnSend;
            server.onClientClosed += OnClientClosed;

            sslServer.clientAccepted += OnAcceptSSL;
            sslServer.onSend += OnSend;
            sslServer.onClientClosed += OnClientClosed;
            bool success = sslServer.StartListening(443, out string er);
            success = server.StartListening(port, out string err);
            if (!success)
            {
                Console.WriteLine("The server failed to start.\n Error: {0}\n Closing application...", err);
                return;
            }

            

            while(true)
            {
                Console.WriteLine("Server listening on port {0}\nInput \"stop\" to stop server, or " +
                "\"clear\" to clear the console", port);
                string input = Console.ReadLine();
                switch(input)
                {
                    case "clear":
                        Console.Clear();
                        break;
                    case "stop":
                        goto shutdown;
                    case "clients":
                        Console.WriteLine("Number of clients: " + sslServer.connectedClients);
                        break;

                    default:
                        break;
                } 
            }

            shutdown:
            server.Shutdown();
            sslServer.Shutdown();
        }
        private static async void OnAccept()
        {
            if (!server.FetchWaitingClient(out ClientTcp client, -1))
                return;

            Console.WriteLine("{0} connected", client.client.Client.RemoteEndPoint);

            TcpReceiveResult rr;

            do
            {
                rr = await server.ReceiveAsync(client);
            } while (rr.success && await OnReceive(rr));


            server.CloseClientSocket(client, -1);
        }
        private static async void OnAcceptSSL()
        {
            if (!sslServer.FetchWaitingClient(out ClientTcp client, -1))
                return;
           
            Console.WriteLine("{0} connected", client.client.Client.RemoteEndPoint);

            TcpReceiveResult rr;
           
            do
            {
                rr = await sslServer.ReceiveAsync(client);
            } while (rr.success && await OnReceive(rr));
            
            
            sslServer.CloseClientSocket(client, -1);
        }

        
        //Returns: A bool that indicates if the connection should be kept alive or not
        private static async Task<bool> OnReceive(TcpReceiveResult rr)
        {

            //size==0 Means that the client sent an empty message which often indicates end of transmission in this case
            if (rr.size == 0) 
            {
                return false;
            }

            Console.WriteLine("ep:{0} size:{1} socket type:{2}", rr.client.client.Client.RemoteEndPoint, rr.size, rr.socketType);
            string receivedMsg = utf8.GetString(rr.buffer);
            //Console.WriteLine(receivedMsg);
            var req = new Request(receivedMsg);
            Console.WriteLine(req.method + " " + req.element);
            bool keepAlive = false;
            if(req.TryGetHeader("Connection", out string con))
            {
                if(con == "keep-alive")
                    keepAlive = true;
            }
            var res = new Response(200);
            

            try
            {
                if (requestHandlers.TryGetValue(req.element, out requestHandler value))
                    await value.Invoke(req, rr);
                else
                {
                    if (keepAlive)
                    {
                        res.SetHeader("Connection", "keep-alive");
                        res.SetHeader("Content-Length", GetFileSize("Assets"+req.element).ToString());
                    }
                    await sslServer.SendFileAsync("Assets" + req.element, rr.client, 0, null, utf8.GetBytes(res.GetMsg()));
                }
            }
            catch (Exception e) when (ExceptionFilter(e, sslServer, rr)) { return false; }
            
            return keepAlive;
        }
        private static bool ExceptionFilter(Exception e, ServerTcp server, TcpReceiveResult rr)
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
                server.Send(code, rr.client);
                Console.WriteLine(e.Message);
                return true;
            }

            return false;
        }

        private static Task SendIndex(Request req, TcpReceiveResult rr)
        {
            Response res = new Response(200);
            res.SetHeader("Connection", "keep-alive");
            res.SetHeader("Content-Length", GetFileSize("Assets/html/index.html").ToString());
            return sslServer.SendFileAsync("Assets/html/index.html", rr.client, 0, null, utf8.GetBytes(res.GetMsg()));
        }

        private static Task UserLogin(Request req, TcpReceiveResult rr)
        {
            Response res = new Response(200);
            res.SetHeader("Connection", "keep-alive");
            res.SetHeader("Content-Length", GetFileSize("Assets/html/user_login.html").ToString());
            return sslServer.SendFileAsync("Assets/html/user_login.html", rr.client, 0, null, utf8.GetBytes(res.GetMsg()));
        }

        private static Task SendVideo(Request req, TcpReceiveResult rr)
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

            return sslServer.SendFileAsync("Assets/"+req.element, rr.client, offset, end, utf8.GetBytes(res.GetMsg()));
        }

        private static async Task<Task> PcStarter(Request req, TcpReceiveResult rr)
        {
            ClientTcp client = new ClientTcp(2048);
            if(!await client.Connect(apiAddress, apiPort))
                return SendInternalServerError(rr.client);
            
            var re = new Request();
            re.method = "GET";
            re.element = "/pcStarter";
            re.SetHeader("Host", "localhost:80");
            re.SetHeader("Connection", "keep-alive");
           
            var bytes = utf8.GetBytes(re.GetMsg());
            client.Send(bytes);
            ReceiveResult apiRR = await client.ReceiveAsync();
            client.Shutdown();
            Console.WriteLine(utf8.GetString(apiRR.buffer));
            if(!apiRR.success)
                return SendInternalServerError(rr.client);
            
            return sslServer.SendAsync(apiRR.buffer, rr.client);
        }

        private static async Task<Task> Login(Request req, TcpReceiveResult rr)
        {
            var api = new ClientTcp(2048);
            if(!await api.Connect(apiAddress, apiPort))
                return SendInternalServerError(rr.client);

            var apiReq = new Request();
            apiReq.element = "/login";
            apiReq.method = "POST";
            apiReq.body = req.body;
            apiReq.SetHeader("Host", string.Format("localhost:{0}", port));
            apiReq.SetHeader("Connection", "keep-alive");
            apiReq.SetHeader("Content-Type", "application/json");
            apiReq.SetHeader("Content-Length", apiReq.body.Length.ToString());
            Console.WriteLine(apiReq.GetMsg());
            var bytes = utf8.GetBytes(apiReq.GetMsg());
            api.Send(bytes);
            var apiRR = await api.ReceiveAsync();
            api.Shutdown();
            if(!apiRR.success)
                return SendInternalServerError(rr.client);
            var apiRes = new Response(utf8.GetString(apiRR.buffer));
            if(apiRes.code != 200)
                return sslServer.SendAsync(apiRR.buffer, rr.client);
            
            //var res = new Response(apiRes.GetMsg());
            //res.code = 303;

            //res.SetHeader("Location", "/");
            //Console.WriteLine(res.GetMsg());
            return sslServer.SendAsync(apiRR.buffer, rr.client);
        }

        private static async Task CreateAccountPage(Request req, TcpReceiveResult rr)
        {
            string file = "Assets/html/createAccount.html";
            var res = new Response(200);
            res.SetHeader("Content-Type", "text/html");
            res.SetHeader("Content-Length", GetFileSize(file).ToString());
            await sslServer.SendFileAsync(file, rr.client, 0, null, utf8.GetBytes(res.GetMsg()));
        }

        private static async Task<Task> NewAccount(Request req, TcpReceiveResult rr)
        {
            var api = new ClientTcp(2048);
            if(!await api.Connect(apiAddress, apiPort))
                return SendInternalServerError(rr.client);
            
            var apiReq = new Request();
            apiReq.element = req.element;
            apiReq.method = "POST";
            apiReq.body = req.body;
            apiReq.SetHeader("Host", string.Format("localhost:{0}", port)); 
            apiReq.SetHeader("Connection", "keep-alive");
            apiReq.SetHeader("Content-Type", "application/json");
            apiReq.SetHeader("Content-Length", apiReq.body.Length.ToString());

            var bytes = utf8.GetBytes(apiReq.GetMsg());
            api.Send(bytes);
            var apiRR = await api.ReceiveAsync();
            if(!apiRR.success)
                return SendInternalServerError(rr.client);
            
            return sslServer.SendAsync(apiRR.buffer, rr.client);
        }

        private static Task SendBadRequest(ClientTcp client)
        {
            return client.SendAsync(utf8.GetBytes(new Response(400).GetMsg()));
        }
        private static Task SendInternalServerError(ClientTcp client)
        {
            return client.SendAsync(utf8.GetBytes(new Response(500).GetMsg()));
        }

        private static void OnClientClosed(IPEndPoint ep)
        {
            Console.WriteLine("Removed {0}, {1} remaining", ep, sslServer.connectedClients);
        }

        
        private static long GetFileSize(string file)
        {
            return new FileInfo(file).Length;
        }
        private static void OnSend(long length, IPEndPoint ep)
        {
            Console.WriteLine("Sent {0} bytes to {1}", length, ep);
        }
    }
}
