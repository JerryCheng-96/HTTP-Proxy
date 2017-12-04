using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace HTTP_Proxy
{
    class ConnectToServer
    {
        private const int bufferSize = 4096;
        public enum HttpMethod { GET, POST, CONNECT, HEAD, INVALID }

        public static int connectionCnt = 0;

        public static HttpMethod InitConnection(Socket withClient, ref Socket withServer)
        {
            var buffer = new byte[bufferSize];
            int cnt = 0;


            cnt = withClient.Receive(buffer);
            while (cnt == buffer.Length)
            {
                Array.Resize(ref buffer, 2 * buffer.Length);
                cnt = withClient.Receive(buffer, buffer.Length / 2, buffer.Length / 2, 0);
            }

            var initReq = new HTTPRequest(buffer, cnt);

            switch (initReq.reqHdr.method)
            {
                case HTTPRequestHeader.HttpMethod.GET:
                    withServer = InitGetConnection(withClient, initReq, buffer);
                    return HttpMethod.GET;

                case HTTPRequestHeader.HttpMethod.CONNECT:
                    Console.WriteLine("CONNECT request encountered @ " + Thread.CurrentThread.Name + ". Ignore now.");
                    return HttpMethod.INVALID;
                    withServer = InitConnectRequest(withClient, initReq, buffer);
                    return HttpMethod.CONNECT;

                default:
                    break;

            }
            return HttpMethod.INVALID;
        }

        public static Socket InitGetConnection(Socket withClient, HTTPRequest initReq, byte[] buffer)
        {
            Socket withServer = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            var ipAddrs = Dns.GetHostAddresses(initReq.reqHdr.httpHdrDict["Host"]);
            try
            {
                withServer.Connect(new IPEndPoint(ipAddrs[ipAddrs.Length - 1], 80));
                withServer.Send(initReq.GetRequestBytes());
            }
            catch (SocketException e)
            {
                try
                {
                    withServer.Connect(new IPEndPoint(ipAddrs[ipAddrs.Length - 1], 80));
                }
                catch (SocketException ee)
                {
                    withClient.Close();
                    return null;
                }
            }

            Array.Clear(buffer, 0, buffer.Length);
            var cnt = withServer.Receive(buffer);

            //Check & make sure get the whole head in the buffer
            int nowAtIndex = 0;
            bool flagCont = true;

            while (true)
            {
                while (flagCont)
                {
                    nowAtIndex = Array.IndexOf<byte>(buffer, 0x0a, nowAtIndex + 1);
                    if (nowAtIndex == -1) { break; }
                    if (nowAtIndex + 2 < buffer.Length) { if (buffer[nowAtIndex + 1] != 0x0d) { continue; } }  // End of Header found 
                    flagCont = false;
                }
                if (!flagCont) { break; }
                if (cnt == buffer.Length) { Array.Resize(ref buffer, 2 * buffer.Length); }
                if (withServer.Available != 0) { cnt += withServer.Receive(buffer, cnt, buffer.Length - cnt, 0); }
            }

            HTTPResponseHeader respHeader;
            if (cnt != 0) { respHeader = new HTTPResponseHeader(buffer); } else { return null; }
            int contentLength = 0;

            switch (respHeader.statusCode)
            {
                case 304:
                    break;
                case 200:
                    try
                    {
                        if (int.TryParse(respHeader.httpHdrDict["Content-Length"], out contentLength))
                        {
                            while (cnt < respHeader.hdrLength + contentLength)
                            {
                                if (cnt == buffer.Length)
                                {
                                    Array.Resize(ref buffer, 2 * buffer.Length);
                                }
                                cnt += withServer.Receive(buffer, cnt, buffer.Length - cnt, 0);
                            }
                        }
                    }
                    catch (KeyNotFoundException e)
                    {
                        if (respHeader.httpHdrDict["Transfer-Encoding"] == "chunked")
                        {
                            int currChunkLength = 0;
                            int headerStartIndex = respHeader.hdrLength;

                            while (true)
                            {
                                // Fetching the head and length of the chunk.
                                try
                                {
                                    nowAtIndex = Array.IndexOf<byte>(buffer, 0x0a, headerStartIndex);   // Finding the end of the header.
                                    Console.WriteLine(Thread.CurrentThread.Name + ", BufSize = " + buffer.Length +
                                                      ", nowHdrStartIndex = " + headerStartIndex);

                                }
                                catch (Exception eee)
                                {
                                    Console.WriteLine(Thread.CurrentThread.Name);
                                    Console.WriteLine(eee);
                                    Console.WriteLine(headerStartIndex + ", " + buffer.Length);

                                }
                                if (nowAtIndex != -1)
                                {
                                    currChunkLength = int.Parse(Encoding.UTF8.GetString(buffer, headerStartIndex,
                                                                nowAtIndex - headerStartIndex - 1), System.Globalization.NumberStyles.HexNumber);
                                    if (currChunkLength == 0) { break; }        // The chunked data stream ends.
                                    nowAtIndex++;
                                    headerStartIndex = nowAtIndex;
                                    // Now nowAtIndex at the beginning of the chunk.

                                    while (cnt < nowAtIndex + currChunkLength)      // The current chunk is not completely buffered
                                    {
                                        if (cnt == buffer.Length) { Array.Resize(ref buffer, 2 * buffer.Length); }
                                        cnt += withServer.Receive(buffer, cnt, buffer.Length - cnt, 0);
                                    }

                                    headerStartIndex += (currChunkLength + 2);    // nowAtIndex now at the start of the next Header
                                }
                                else
                                {
                                    if (cnt == buffer.Length) { Array.Resize(ref buffer, 2 * buffer.Length); }
                                    cnt += withServer.Receive(buffer, cnt, buffer.Length - cnt, 0);
                                }
                            }
                        }
                    }
                    break;
            }

            var response = new HTTPResponse(buffer, cnt);
            withClient.Send(response.GetResponseBytes());
            try
            {
                if (response.respHdr.httpHdrDict["Connection"] == "close")
                {
                    withServer.Close(10);
                    withClient.Close(10);
                    return null;
                }
            }
            catch (KeyNotFoundException ke)
            {
                if (!(initReq.reqHdr.httpHdrDict["Proxy-Connection"] == "keep-alive"))
                {
                    withServer.Close(10);
                    withClient.Close(10);
                    return null;
                }
            }













            //Array.Clear(buffer, 0, buffer.Length);
            //var cnt = withServer.Receive(buffer);

            ////Check & make sure get the whole head in the buffer
            //int nowAtIndex = 0;
            //bool flagCont = true;

            //while (true)
            //{
            //    while (flagCont)
            //    {
            //        nowAtIndex = Array.IndexOf<byte>(buffer, 0x0a, nowAtIndex + 1);
            //        if (nowAtIndex == -1) { break; }
            //        if (nowAtIndex + 2 < buffer.Length) { if (buffer[nowAtIndex + 1] != 0x0d) { continue; } }  // End of Header found 
            //        flagCont = false;
            //    }
            //    if (!flagCont) { break; }
            //    if (cnt == buffer.Length) { Array.Resize(ref buffer, 2 * buffer.Length); }
            //    if (withServer.Available != 0) { cnt += withServer.Receive(buffer, cnt, buffer.Length - cnt, 0); }
            //}

            //HTTPResponseHeader respHeader;
            //if (cnt != 0) { respHeader = new HTTPResponseHeader(buffer); } else { return null; }
            //int contentLength = 0;

            //switch (respHeader.statusCode)
            //{
            //    case 304:
            //        break;
            //    case 200:
            //        try
            //        {
            //            if (int.TryParse(respHeader.httpHdrDict["Content-Length"], out contentLength))
            //            {
            //                while (cnt < respHeader.hdrLength + contentLength)
            //                {
            //                    if (cnt == buffer.Length)
            //                    {
            //                        Array.Resize(ref buffer, 2 * buffer.Length);
            //                    }
            //                    cnt += withServer.Receive(buffer, cnt, buffer.Length - cnt, 0);
            //                }
            //            }
            //        }
            //        catch (KeyNotFoundException e)
            //        {
            //            if (respHeader.httpHdrDict["Transfer-Encoding"] == "chunked")
            //            {
            //                int currChunkLength = 0;
            //                int headerStartIndex = respHeader.hdrLength;

            //                while (true)
            //                {
            //                    // Fetching the head and length of the chunk.
            //                    nowAtIndex = Array.IndexOf<byte>(buffer, 0x0a, headerStartIndex);   // Finding the end of the header.
            //                    if (nowAtIndex != -1)
            //                    {
            //                        currChunkLength = int.Parse(Encoding.UTF8.GetString(buffer, headerStartIndex,
            //                                            nowAtIndex - headerStartIndex - 1), System.Globalization.NumberStyles.HexNumber);
            //                        if (currChunkLength == 0) { break; }        // The chunked data stream ends.
            //                        nowAtIndex++;
            //                        headerStartIndex = nowAtIndex;
            //                        // Now nowAtIndex at the beginning of the chunk.
            //                    }

            //                    while (cnt < nowAtIndex + currChunkLength)      // The current chunk is not completely buffered
            //                    {
            //                        if (cnt == buffer.Length) { Array.Resize(ref buffer, 2 * buffer.Length); }
            //                        cnt += withServer.Receive(buffer, cnt, buffer.Length - cnt, 0);
            //                    }

            //                    headerStartIndex += (currChunkLength + 2);    // nowAtIndex now at the start of the next Header
            //                }
            //            }
            //        }
            //        break;
            //}

            //var response = new HTTPResponse(buffer, cnt);
            //withClient.Send(response.GetResponseBytes());
            //try
            //{
            //    if (response.respHdr.httpHdrDict["Connection"] == "close")
            //    {
            //        withServer.Close(10);
            //        withClient.Close(10);
            //        return null;
            //    }
            //}
            //catch (KeyNotFoundException ke)
            //{
            //    if (!(initReq.reqHdr.httpHdrDict["Proxy-Connection"] == "keep-alive"))
            //    {
            //        withServer.Close(10);
            //        withClient.Close(10);
            //        return null;
            //    }
            //}

            return withServer;

        }

        public static bool ProcGetRequest(Socket withClient, Socket withServer)
        {
            var buffer = new byte[bufferSize];
            int cnt = 0;

            try
            {
                cnt = withClient.Receive(buffer);

            }
            catch (SocketException e)
            {
                Console.WriteLine(e);
                Console.WriteLine(e.SocketErrorCode);
                throw;
            }


            if (cnt == 0)
            {
                Console.WriteLine("Stopping " + Thread.CurrentThread.Name);
                return false;
            }

            while (cnt == buffer.Length)
            {
                Array.Resize(ref buffer, 2 * buffer.Length);
                cnt = withClient.Receive(buffer, buffer.Length / 2, buffer.Length / 2, 0);
            }

            var req = new HTTPRequest(buffer, cnt);

            try
            {
                withServer.Send(req.GetRequestBytes());
            }
            catch (ArgumentNullException e)
            {
                Console.WriteLine(e);
                Console.WriteLine(Encoding.UTF8.GetString(buffer, 0, cnt));
            }

            Array.Clear(buffer, 0, buffer.Length);
            cnt = withServer.Receive(buffer);

            //Check & make sure get the whole head in the buffer
            int nowAtIndex = 0;
            bool flagCont = true;

            while (true)
            {
                while (flagCont)
                {
                    nowAtIndex = Array.IndexOf<byte>(buffer, 0x0a, nowAtIndex + 1);
                    if (nowAtIndex == -1) { break; }
                    if (nowAtIndex + 2 < buffer.Length) { if (buffer[nowAtIndex + 1] != 0x0d) { continue; } }  // End of Header found 
                    flagCont = false;
                }
                if (!flagCont) { break; }
                if (cnt == buffer.Length) { Array.Resize(ref buffer, 2 * buffer.Length); }
                if (withServer.Available != 0) { cnt += withServer.Receive(buffer, cnt, buffer.Length - cnt, 0); }
            }

            HTTPResponseHeader respHeader;
            if (cnt != 0) { respHeader = new HTTPResponseHeader(buffer); } else { return false; }
            int contentLength = 0;

            switch (respHeader.statusCode)
            {
                case 304:
                    break;
                case 200:
                    try
                    {
                        if (int.TryParse(respHeader.httpHdrDict["Content-Length"], out contentLength))
                        {
                            while (cnt < respHeader.hdrLength + contentLength)
                            {
                                if (cnt == buffer.Length)
                                {
                                    Array.Resize(ref buffer, 2 * buffer.Length);
                                }
                                cnt += withServer.Receive(buffer, cnt, buffer.Length - cnt, 0);
                            }
                        }
                    }
                    catch (KeyNotFoundException e)
                    {
                        if (respHeader.httpHdrDict["Transfer-Encoding"] == "chunked")
                        {
                            int currChunkLength = 0;
                            int headerStartIndex = respHeader.hdrLength;

                            while (true)
                            {
                                // Fetching the head and length of the chunk.
                                try
                                {
                                    nowAtIndex = Array.IndexOf<byte>(buffer, 0x0a, headerStartIndex);   // Finding the end of the header.
                                    Console.WriteLine(Thread.CurrentThread.Name + ", BufSize = " + buffer.Length +
                                                      ", nowHdrStartIndex = " + headerStartIndex);

                                }
                                catch (Exception eee)
                                {
                                    Console.WriteLine(Thread.CurrentThread.Name);
                                    Console.WriteLine(eee);
                                    Console.WriteLine(headerStartIndex + ", " + buffer.Length);

                                }
                                if (nowAtIndex != -1)
                                {
                                    currChunkLength = int.Parse(Encoding.UTF8.GetString(buffer, headerStartIndex,
                                                                nowAtIndex - headerStartIndex - 1), System.Globalization.NumberStyles.HexNumber);
                                    if (currChunkLength == 0) { break; }        // The chunked data stream ends.
                                    nowAtIndex++;
                                    headerStartIndex = nowAtIndex;
                                    // Now nowAtIndex at the beginning of the chunk.

                                    while (cnt < nowAtIndex + currChunkLength)      // The current chunk is not completely buffered
                                    {
                                        if (cnt == buffer.Length) { Array.Resize(ref buffer, 2 * buffer.Length); }
                                        cnt += withServer.Receive(buffer, cnt, buffer.Length - cnt, 0);
                                    }

                                    headerStartIndex += (currChunkLength + 2);    // nowAtIndex now at the start of the next Header
                                }
                                else
                                {
                                    if (cnt == buffer.Length) { Array.Resize(ref buffer, 2 * buffer.Length); }
                                    cnt += withServer.Receive(buffer, cnt, buffer.Length - cnt, 0);
                                }
                            }
                        }
                    }
                    break;
            }

            var response = new HTTPResponse(buffer, cnt);
            withClient.Send(response.GetResponseBytes());
            try
            {
                if (response.respHdr.httpHdrDict["Connection"] == "close")
                {
                    withServer.Close(10);
                    withClient.Close(10);
                    return false;
                }
            }
            catch (KeyNotFoundException ke)
            {
                if (!(req.reqHdr.httpHdrDict["Proxy-Connection"] == "keep-alive"))
                {
                    withServer.Close(10);
                    withClient.Close(10);
                    return false;
                }
            }

            return true;

        }

        public static Socket InitConnectRequest(Socket withClient, HTTPRequest initReq, byte[] buffer)
        {
            Socket withServer = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPAddress[] ipAddrs = new IPAddress[1];
            try
            {
                ipAddrs = Dns.GetHostAddresses(initReq.reqHdr.httpHdrDict["Host"]);
            }
            catch (SocketException e)
            {
                if (e.SocketErrorCode == SocketError.HostNotFound)
                {
                    ipAddrs[0] = IPAddress.Parse(initReq.reqHdr.httpHdrDict["Host"]);
                }

            }
            try
            {
                withServer.Connect(new IPEndPoint(ipAddrs[ipAddrs.Length - 1], initReq.reqHdr.portNo));
            }
            catch (SocketException e)
            {
                try
                {
                    withServer.Connect(new IPEndPoint(ipAddrs[ipAddrs.Length - 1], 80));
                }
                catch (SocketException ee)
                {
                    withClient.Close();
                    return null;
                }
            }
            withClient.Send(Encoding.UTF8.GetBytes("HTTP/1.1 200 Connection Established\r\n\r\n"));

            return withServer;
        }

        public static void ConnectForward(Socket withClient, Socket withServer)
        {
            var c2s = new ForwardingSession(withClient, withServer);
            Thread c2sThrd = new Thread(new ThreadStart(c2s.Forward));
            var currcnt = connectionCnt++;
            c2sThrd.Name = "CONNECT C2S_" + currcnt;
            var s2c = new ForwardingSession(withServer, withClient);
            Thread s2cThrd = new Thread(new ThreadStart(s2c.Forward));
            s2cThrd.Name = "CONNECT S2C_" + currcnt;
            c2sThrd.Start();
            s2cThrd.Start();

            //var buffer = new byte[bufferSize];

            //try
            //{
            //    var cnt = withClient.Receive(buffer);
            //    while (true)
            //    {
            //        if (cnt < buffer.Length) { break; }
            //        Array.Resize(ref buffer, 2 * buffer.Length);
            //        cnt += withClient.Receive(buffer, cnt, buffer.Length - cnt, 0);
            //    }
            //    withServer.Send(buffer, 0, cnt, 0);

            //    cnt = withServer.Receive(buffer);
            //    while (true)
            //    {
            //        if (cnt < buffer.Length) { break; }
            //        Array.Resize(ref buffer, 2 * buffer.Length);
            //        cnt += withServer.Receive(buffer, cnt, buffer.Length - cnt, 0);
            //    }
            //    withClient.Send(buffer, 0, cnt, 0);

            //}
            //catch (SocketException e)
            //{
            //    switch (e.SocketErrorCode)
            //    {
            //        default:
            //            break;
            //    }
            //}
            //return true;
        }

        class ForwardingSession
        {
            private const int bufferSize = 4096;

            public Socket recvSocket;
            public Socket sendSocket;
            public byte[] buffer;
            public int cnt;

            public ForwardingSession(Socket r, Socket s)
            {
                recvSocket = r;
                sendSocket = s;
            }

            public void Forward()
            {
                buffer = new byte[bufferSize];

                while (true)
                {
                    try
                    {
                        int cnt = 0;
                        if (recvSocket.Available != 0) { cnt = recvSocket.Receive(buffer); }
                        sendSocket.Send(buffer, cnt, 0);
                    }
                    catch (Exception ode)
                    {
                        try
                        {
                            Console.WriteLine(Thread.CurrentThread.Name + " completing.");
                            Console.WriteLine(ode);
                            sendSocket.Close(10);
                            recvSocket.Close(10);
                            return;
                        }
                        catch (NullReferenceException) { return; }
                    }
                }
                //Thread.CurrentThread.Abort();
            }

            public void SendAfterRecved(IAsyncResult iar)
            {
                try
                {
                    try
                    {
                        cnt = recvSocket.EndReceive(iar);
                    }
                    catch (Exception e)
                    {
                    }
                    sendSocket.Send(buffer, 0, cnt, 0);
                }
                catch (Exception e)
                {
                    try
                    {
                        Console.WriteLine(e);
                        sendSocket.Close(10);
                        recvSocket.Close(10);
                        return;
                    }
                    catch (NullReferenceException) { return; }

                }
            }
        }
    }


    class ForwardingiSession
    {
        private const int bufferSize = 4096;

        public Socket withClient;
        public Socket withServer;

        public ForwardingiSession(Socket withClient, Socket withServer)
        {
            this.withClient = withClient;
            this.withServer = withServer;
        }

        public void BidirectionalForward()
        {
            var c2sBuffer = new byte[bufferSize];
            var s2cBuffer = new byte[bufferSize];

            while (true)
            {
                try
                {
                    withServer.Send(c2sBuffer, withClient.Receive(c2sBuffer), 0);
                    withClient.Send(s2cBuffer, withServer.Receive(s2cBuffer), 0);
                    //withClient.BeginReceive(c2sBuffer, 0, bufferSize, 0, new AsyncCallback(Forwarding),
                    //    new SocketsBuffer(withClient, withServer, c2sBuffer));
                    //withServer.BeginReceive(s2cBuffer, 0, bufferSize, 0, new AsyncCallback(Forwarding),
                    //                        new SocketsBuffer(withServer, withClient, c2sBuffer));
                }
                catch (SocketException e)
                {
                    Console.WriteLine(e);
                    if (e.SocketErrorCode == SocketError.ConnectionAborted) { return; }
                }
            }
        }

        public void Forwarding(IAsyncResult ar)
        {
            var sktBuf = (SocketsBuffer)ar.AsyncState;
            int cnt = 0;

            try
            {
                cnt = sktBuf.recvBuffer.EndReceive(ar);
                sktBuf.sendBuffer.Send(sktBuf.buffer, 0, cnt, 0);
            }
            catch (SocketException e) { return; }
        }

        class SocketsBuffer
        {
            public Socket recvBuffer;
            public Socket sendBuffer;
            public byte[] buffer;

            public SocketsBuffer(Socket recvBuffer, Socket sendBuffer, byte[] buffer)
            {
                this.recvBuffer = recvBuffer;
                this.sendBuffer = sendBuffer;
                this.buffer = buffer;
            }
        }

    }
}
