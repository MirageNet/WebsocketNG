using System;
using System.IO;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;

namespace Mirror.Websocket.Server
{
    /// <summary>
    /// Handles Handshakes from new clients on the server
    /// <para>The server handshake has buffers to reduce allocations when clients connect</para>
    /// </summary>
    internal static class ServerHandshake
    {
        const int KeyLength = 24;
        const string KeyHeaderString = "Sec-WebSocket-Key: ";

        static readonly byte[] GetBytes = Encoding.ASCII.GetBytes("GET");


        public static void Handshake(Stream stream)
        {
            var headerBuffer = new MemoryStream(GetBytes.Length);
            stream.ReadExact(headerBuffer, GetBytes.Length);

            if (!IsGet(headerBuffer.GetBuffer(), 0))
            {
                throw new WebSocketException(WebSocketError.NotAWebSocket, $"First bytes from client was not 'GET' for handshake, instead was {Encoding.ASCII.GetString(headerBuffer.GetBuffer(), 0, GetBytes.Length)}");
            }

            headerBuffer.SetLength(0);

            string msg = stream.ReadHttpHeader();

            AcceptHandshake(stream, msg);
        }

        static bool IsGet(byte[] getHeader, int offset)
        {
            for (int i = 0; i< getHeader.Length; i++)
            {
                if (getHeader[i + offset] != GetBytes[i])
                    return false;
            }
            return true;
        }

        static void AcceptHandshake(Stream stream, string msg)
        {
            MemoryStream keyBuffer = new MemoryStream();
            MemoryStream responseBuffer = new MemoryStream();

            GetKey(msg, keyBuffer);
            AppendGuid(keyBuffer);
            byte[] keyHash = CreateHash(keyBuffer);
            CreateResponse(keyHash, responseBuffer);

            responseBuffer.WriteTo(stream);
        }


        static void GetKey(string msg, MemoryStream keyBuffer)
        {
            int start = msg.IndexOf(KeyHeaderString) + KeyHeaderString.Length;
            keyBuffer.SetLength(KeyLength);
            Encoding.ASCII.GetBytes(msg, start, KeyLength, keyBuffer.GetBuffer(), 0);
        }

        static void AppendGuid(MemoryStream keyBuffer)
        {
            keyBuffer.Write(Constants.HandshakeGUIDBytes, 0, KeyLength);
        }

        static byte[] CreateHash(MemoryStream keyBuffer)
        {
            using (var sha1 = SHA1.Create())
            {
                return sha1.ComputeHash(keyBuffer.GetBuffer(), 0, (int)keyBuffer.Length);
            }
        }

        static void CreateResponse(byte[] keyHash, MemoryStream responseBuffer)
        {
            string keyHashString = Convert.ToBase64String(keyHash);

            // compiler should merge these strings into 1 string before format
            string message = string.Format(
                "HTTP/1.1 101 Switching Protocols\r\n" +
                "Connection: Upgrade\r\n" +
                "Upgrade: websocket\r\n" +
                "Sec-WebSocket-Accept: {0}\r\n\r\n",
                keyHashString);


            responseBuffer.Capacity = message.Length + 100;
            int bytes = Encoding.ASCII.GetBytes(message, 0, message.Length, responseBuffer.GetBuffer(), 0);
            responseBuffer.SetLength(bytes);
        }
    }
}
