using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace HTTP_Proxy
{
    class Program
    {

        private const int bufferSize = 4096;
        private static Dictionary<Socket, Session> connectionList;

        static void Main(string[] args)
        {
            Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            serverSocket.Bind(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8080));
            serverSocket.Listen(10);
            connectionList = new Dictionary<Socket, Session>(); 

            serverSocket.BeginAccept(Accepting, serverSocket);

            Console.ReadLine();

        }

        private static void Accepting(IAsyncResult ar)
        {
            Socket serverSocket = (Socket)ar.AsyncState;
            Socket withClient = serverSocket.EndAccept(ar);

            if (connectionList.ContainsKey(withClient))
            {
                
            }
            


            var recvBuffer = new byte[bufferSize];
            var buffer = new byte[bufferSize];
            int bufSizeFactor = 1;

            var cnt = withClient.Receive(recvBuffer);
            recvBuffer.CopyTo(buffer, (bufSizeFactor - 1) * bufferSize);

            while (true)
            {
                bufSizeFactor *= 2;
                if (cnt < (bufSizeFactor / 2) * bufferSize) { break; }
                Array.Resize(ref buffer, bufSizeFactor * bufferSize);
                cnt = withClient.Receive(buffer);
                recvBuffer.CopyTo(buffer, (bufSizeFactor / 2) * bufferSize);
            }

            var req = new HTTPRequest(buffer, cnt);
            connectionList.Add(withClient, new Session(withClient, ConnectToServer.InitConnection(req)));

            Console.WriteLine(Encoding.UTF8.GetString(buffer, 0, cnt));

        }


    }
}
