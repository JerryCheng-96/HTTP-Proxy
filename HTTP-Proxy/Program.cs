using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace HTTP_Proxy
{
    class Program
    {

        private const int bufferSize = 4096;
        private static int connectionCnt = 0;

        static void Main(string[] args)
        {
            Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            serverSocket.Bind(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8080));
            serverSocket.Listen(20);

            while (true)
            {
                ConnectionThread connThrd = new ConnectionThread(serverSocket.Accept());
                Thread procThrd = new Thread(new ThreadStart(connThrd.ProcConnection));
                procThrd.Name = "Connection #" + connectionCnt++;
                Console.WriteLine("Starting " + procThrd.Name);
                procThrd.Start();
            }

            Console.ReadLine();

        }

        private static void Accepting(IAsyncResult ar)
        {
            Socket serverSocket = (Socket)ar.AsyncState;
            Socket withClient = serverSocket.EndAccept(ar);

            ConnectionThread connThrd = new ConnectionThread(withClient);
            Thread procThrd = new Thread(new ThreadStart(connThrd.ProcConnection));
            procThrd.Name = "CONNECTION";
            procThrd.Start();

        }
    }

    class ConnectionThread
    {
        public Socket withClient;
        public Socket withServer;

        public ConnectionThread(Socket withClient)
        {
            this.withClient = withClient;
        }

        public void ProcConnection()
        {
            switch (ConnectToServer.InitConnection(withClient, ref withServer))
            {
                case ConnectToServer.HttpMethod.GET:
                    if (withServer == null) { return; }
                    while (ConnectToServer.ProcGetRequest(withClient, withServer)) ;
                    Console.WriteLine(Thread.CurrentThread.Name + " is to STOP.");
                    break;

                case ConnectToServer.HttpMethod.CONNECT:
                    ConnectToServer.ConnectForward(withClient, withServer);
                    break;

                case ConnectToServer.HttpMethod.INVALID:
                    break;
            }


        }
    }
}
