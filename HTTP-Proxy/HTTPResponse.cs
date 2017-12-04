using System;
using System.Collections.Generic;
using System.Text;

namespace HTTP_Proxy
{
    class HTTPResponse
    {
        public HTTPResponseHeader respHdr;
        public byte[] data;
        public int respLength;

        public HTTPResponse() { }

        public HTTPResponse(byte[] buffer, int bufLength)
        {
            respHdr = new HTTPResponseHeader(buffer);
            respLength = bufLength;

            if (respHdr.hdrLength < respLength)
            {
                data = new byte[respLength - respHdr.hdrLength];
                Array.Copy(buffer, respHdr.hdrLength, data, 0, data.Length);
            }
            else { data = null; }

        }

        public byte[] GetResponseBytes()
        {
            var hdrBytes = respHdr.GetHeaderBytes();
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

    class HTTPResponseHeader
    {
        public int hdrLength;
        public String firstLine;
        public int statusCode;
        public Dictionary<String, String> httpHdrDict;

        public HTTPResponseHeader() { }

        public HTTPResponseHeader(byte[] buffer)
        {
            int nowAtIndex = Array.IndexOf<byte>(buffer, 10, 0);

            while (Array.IndexOf<byte>(buffer, 10, nowAtIndex + 1) != -1 && buffer[nowAtIndex + 1] != 13)
            {
                nowAtIndex = Array.IndexOf<byte>(buffer, 10, nowAtIndex + 1);
            }

            var headerText = Encoding.UTF8.GetString(buffer, 0, nowAtIndex + 3);
            hdrLength = nowAtIndex + 3;
            httpHdrDict = new Dictionary<String, String>();
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
            statusCode = int.Parse(reqFirstLine[1]);
        }

        public byte[] GetHeaderBytes()
        {
            var strBuilder = new StringBuilder();

            strBuilder.Append(firstLine + "\r\n");
            foreach (var item in httpHdrDict)
            {
                if (item.Value != "") { strBuilder.Append(item.Key + ": " + item.Value + "\r\n"); }
            }
            strBuilder.Append("\r\n");
            return Encoding.UTF8.GetBytes(strBuilder.ToString());
        }

    }
}
