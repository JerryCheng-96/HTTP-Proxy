using System;
using System.Collections.Generic;
using System.Text;

namespace HTTP_Proxy
{
    class HTTPRequest
    {
        public HTTPRequestHeader reqHdr;
        public byte[] data;
        public int reqLength;

        public HTTPRequest() { }

        public HTTPRequest(byte[] buffer, int bufLength)
        {
            reqHdr = new HTTPRequestHeader(buffer);
            reqLength = bufLength;

            if (reqHdr.hdrLength < reqLength)
            {
                data = new byte[bufLength - reqHdr.hdrLength];
                Array.Copy(buffer, reqHdr.hdrLength, data, 0, data.Length);
            }
            else { data = null; }
        }

        public byte[] GetRequestBytes()
        {
            var hdrBytes = reqHdr.GetHeaderBytes();
            if (data != null)
            {
                var resBytes = new byte[hdrBytes.Length + data.Length];
                hdrBytes.CopyTo(resBytes, 0);
                data.CopyTo(resBytes, hdrBytes.Length);
                return resBytes;
            }

            return hdrBytes;
        }
    }

    class HTTPRequestHeader
    {
        public enum HttpMethod { GET, POST, CONNECT, HEAD }

        public int hdrLength;
        public String relAddr;
        public String protocolVer;
        public int portNo = 80;
        public HttpMethod method;
        public Dictionary<String, String> httpHdrDict;
        public String firstLine;


        public HTTPRequestHeader() { }

        public HTTPRequestHeader(byte[] buffer)
        {
            int nowAtIndex = Array.IndexOf<byte>(buffer, 10, 0);

            while (Array.IndexOf<byte>(buffer, 10, nowAtIndex + 1) != -1 && buffer[nowAtIndex + 1] != 13)
            {
                nowAtIndex = Array.IndexOf<byte>(buffer, 10, nowAtIndex + 1);
            }

            var headerText = Encoding.UTF8.GetString(buffer, 0, nowAtIndex + 3);
            hdrLength = nowAtIndex + 3;
            httpHdrDict = new Dictionary<String, String>
            {
                { " ", "" },
                { "Host", "" },
                { "Proxy-Connection", "" },
                { "User-Agent", "" },
                { "Accept", "" },
                { "Accept-Encoding", "" },
                { "Accept-Language", "" },
                { "Cache-Control", "" },
                { "Cookie", "" },
                { "Pragma", "" }
            };
            var parsedLines = headerText.Split("\r\n");

            firstLine = parsedLines[0];
            for (int i = 1; i < parsedLines.Length; i++)
            {
                var parsedTheLine = parsedLines[i].Split(": ");

                if (parsedTheLine.Length > 1)
                {
                    httpHdrDict[parsedTheLine[0]] = parsedTheLine[1];
                }
            }

            var reqFirstLine = firstLine.Split(" ");
            if (reqFirstLine[0] == "GET")
            {
                method = HttpMethod.GET;
                relAddr = reqFirstLine[1].Substring(reqFirstLine[1].IndexOf(httpHdrDict["Host"]) + httpHdrDict["Host"].Length);
                protocolVer = reqFirstLine[reqFirstLine.Length - 1];
            }
            else if (reqFirstLine[0] == "CONNECT")
            {
                method = HttpMethod.CONNECT;
                var connectTo = reqFirstLine[1].Split(":");
                if (httpHdrDict["Host"] != "")
                {
                    httpHdrDict["Host"] = connectTo[0]; 
                }
                portNo = int.Parse(connectTo[1]);
            }
            else if (reqFirstLine[0] == "HEAD")
            {
                method = HttpMethod.HEAD;
            }
            else if (reqFirstLine[0] == "POST")
            {
                method = HttpMethod.POST;
            }

        }

        public byte[] GetHeaderBytes()
        {
            var strBuilder = new StringBuilder();

            switch (method)
            {
                case HttpMethod.GET:
                    strBuilder.Append("GET " + relAddr + " " + protocolVer + "\r\n");
                    foreach (var item in httpHdrDict)
                    {
                        if (item.Value != "") { strBuilder.Append(item.Key + ": " + item.Value + "\r\n"); }
                    }
                    strBuilder.Append("\r\n");
                    return Encoding.UTF8.GetBytes(strBuilder.ToString());
            }

            return null;
        }
    }
}
