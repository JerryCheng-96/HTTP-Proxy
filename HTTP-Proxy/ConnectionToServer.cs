using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace HTTP_Proxy
{
    class ConnectToServer
    {
        private const int bufferSize = 4096;
        public Socket withClient;
        public Socket withServer;

        public ConnectToServer() { }

        public ConnectToServer(Socket withClient, HTTPRequest initReq)
        {
            this.withClient = withClient;
            this.withServer = null;


        }

        private void HandleConnect(Socket withClient, HTTPRequest req)
        {
            Socket withServer = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            withServer.Connect(new IPEndPoint(Dns.GetHostAddresses(req.hdr.httpHdrDict["Host"])[0], req.hdr.portNo));
            withClient.Send(Encoding.UTF8.GetBytes("HTTP/1.1 200 Connection Established\r\n\r\n"));

            while (true)
            {
                var bufClient = new byte[bufferSize];
                var bufServer = new byte[bufferSize];

                while (true)
                {
                    withClient.BeginReceive(bufClient, 0, bufferSize, 0, SendBuffer, new object[] { withServer, withClient, bufClient });
                    withServer.BeginReceive(bufServer, 0, bufferSize, 0, SendBuffer, new object[] { withClient, withServer, bufServer });
                }

            }
        }

        public static Socket InitConnection(HTTPRequest initReq)
        {
            switch (initReq.hdr.method)
            {
                case HTTPHeader.HttpMethod.GET:
                    Socket withServer = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    withServer.Connect(new IPEndPoint(Dns.GetHostAddresses(initReq.hdr.httpHdrDict["Host"])[0], 80));

                    return withServer;
            }

            return null;
        }

        private void HandleGet(HTTPRequest req)
        {
            var reqBytes = req.GetRequestBytes();
            withServer.Send(reqBytes);

            var recvBufferWithServer = new byte[bufferSize];
            var bufferWithServer = new byte[bufferSize];
            int bufSizeFactor;

            bufSizeFactor = 1;
            var cnt = withServer.Receive(recvBufferWithServer);
            recvBufferWithServer.CopyTo(bufferWithServer, (bufSizeFactor - 1) * bufferSize);

            while (true)
            {
                bufSizeFactor *= 2;
                if (cnt < (bufSizeFactor / 2) * bufferSize) { break; }
                Array.Resize(ref bufferWithServer, bufSizeFactor * bufferSize);
                cnt = withServer.Receive(bufferWithServer);
                recvBufferWithServer.CopyTo(bufferWithServer, (bufSizeFactor / 2) * bufferSize);
            }

            withClient.Send(bufferWithServer, cnt, 0);
        }

        private static void SendBuffer(IAsyncResult ar)
        {
            var withReceiver = (Socket)((object[])ar.AsyncState)[0];
            var withSender = (Socket)((object[])ar.AsyncState)[1];
            var bufClient = (byte[])((object[])ar.AsyncState)[2];
            int cnt = 0;


            try
            {
                cnt = withSender.EndReceive(ar);
            }
            catch (SocketException e)
            {
                Console.WriteLine(e);
            }

            if (cnt == bufferSize)
            {
                Array.Resize(ref bufClient, 2 * bufClient.Length);
                withReceiver.BeginReceive(bufClient, bufClient.Length / 2, bufClient.Length / 2, 0, SendBuffer, new object[] { withReceiver, withSender, bufClient });
            }

            withReceiver.Send(bufClient, cnt, 0);

        }
    }

    class Session
    {
        public Socket withClient;
        public Socket withServer;

        public Session(Socket withClient, Socket withServer)
        {
            this.withServer = withServer;
            this.withClient = withClient;
        }
    }
}
