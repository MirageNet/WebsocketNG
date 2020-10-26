using System.IO;
using System.Net.WebSockets;
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
            var responseBuffer = new MemoryStream();

            string key = GetKey(msg);
            string keyHash = Nonce.Hash(key);
            CreateResponse(keyHash, responseBuffer);
            responseBuffer.WriteTo(stream);
        }


        static string GetKey(string msg)
        {
            int start = msg.IndexOf(KeyHeaderString) + KeyHeaderString.Length;
            return msg.Substring(start, KeyLength);
        }

        static void CreateResponse(string keyHash, MemoryStream responseBuffer)
        {
            // compiler should merge these strings into 1 string before format
            string message = string.Format(
                "HTTP/1.1 101 Switching Protocols\r\n" +
                "Connection: Upgrade\r\n" +
                "Upgrade: websocket\r\n" +
                "Sec-WebSocket-Protocol: binary\r\n" +
                "Sec-WebSocket-Accept: {0}\r\n\r\n",
                keyHash);

            responseBuffer.SetLength(message.Length);
            Encoding.ASCII.GetBytes(message, 0, message.Length, responseBuffer.GetBuffer(), 0);
            responseBuffer.Position = message.Length;
        }
    }
}
