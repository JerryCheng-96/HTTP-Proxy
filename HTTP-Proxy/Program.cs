using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace HTTP_Proxy
{
    class Program
    {
        static void Main(string[] args)
        {
            var s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            var ipEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8080);
            s.Bind(ipEndPoint);
            s.Listen(10);

            while (true)
            {
                Response(s.Accept());
            }
        }

        static void Response(Socket serverSocket)
        {
            var buffer = new byte[65536];

            var cnt = serverSocket.Receive(buffer);
            var reqString = Encoding.UTF8.GetString(buffer, 0, cnt);

            var c = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            c.Connect(new IPEndPoint(DnsParse(ExtractHost(reqString))[0], 80));
            c.Send(buffer, cnt, 0);
            cnt = c.Receive(buffer);
            ExtractContentLength(Encoding.UTF8.GetString(buffer, 0, cnt));
            serverSocket.Send(buffer, cnt, 0);
            while (true)
            {
                if (IsClose(Encoding.UTF8.GetString(buffer)))
                {
                    c.Close();
                    serverSocket.Close();
                    return;
                }
                else
                {
                    cnt = serverSocket.Receive(buffer);
                    reqString = Encoding.UTF8.GetString(buffer, 0, cnt);
                    c.Send(buffer);
                    c.Receive(buffer);
                    ExtractContentLength(Encoding.UTF8.GetString(buffer));
                    Console.WriteLine(Encoding.UTF8.GetString(buffer));
                    serverSocket.Send(buffer);
                } 
            }
        }

        static String ExtractHost(String reqString)
        {
            Console.WriteLine(reqString);
            var startIndex = reqString.IndexOf("Host: ") + "Host: ".Length;
            var endIndex = reqString.IndexOf('\r', startIndex);
            return reqString.Substring(startIndex, endIndex - startIndex);
        }

        static long ExtractContentLength(String respString)
        {
            var startIndex = respString.IndexOf("Content-Length: ") + "Content-Length: ".Length;
            var endIndex = respString.IndexOf('\r', startIndex);
            return long.Parse(respString.Substring(startIndex, endIndex - startIndex));
        }

        private static bool IsClose(String respString)
        {
            var startIndex = respString.IndexOf("Connection: ") + "Connection: ".Length;
            var endIndex = respString.IndexOf('\r', startIndex);
            return respString.Substring(startIndex, endIndex - startIndex) == "close";
        }

        static IPAddress[] DnsParse(String hostname)
        {
            try
            {
                return Dns.GetHostAddresses(hostname);
            }
            catch (SocketException e)
            {
                if (e.SocketErrorCode == SocketError.HostNotFound)
                {
                    return null;
                }
                throw;
            }
        }
    }
}
