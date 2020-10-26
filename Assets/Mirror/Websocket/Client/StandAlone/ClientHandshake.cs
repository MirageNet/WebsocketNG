using System;
using System.IO;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;

namespace Mirror.Websocket.Client
{
    /// <summary>
    /// Handles Handshake to the server when it first connects
    /// <para>The client handshake does not need buffers to reduce allocations since it only happens once</para>
    /// </summary>
    internal class ClientHandshake
    {
        public static void Handshake(Stream stream, Uri uri)
        {
            byte[] keyBuffer = new byte[16];
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(keyBuffer);
            }

            string key = Convert.ToBase64String(keyBuffer);
            string keySum = key + Constants.HandshakeGUID;
            byte[] keySumBytes = Encoding.UTF8.GetBytes(keySum);

            byte[] keySumHash = SHA1.Create().ComputeHash(keySumBytes);

            string expectedResponse = Convert.ToBase64String(keySumHash);
            string handshake =
                $"GET /chat HTTP/1.1\r\n" +
                $"Host: {uri.Host}:{uri.Port}\r\n" +
                $"Upgrade: websocket\r\n" +
                $"Connection: Upgrade\r\n" +
                $"Sec-WebSocket-Key: {key}\r\n" +
                $"Sec-WebSocket-Version: 13\r\n" +
                "\r\n";
            byte[] encoded = Encoding.ASCII.GetBytes(handshake);
            stream.Write(encoded, 0, encoded.Length);

            byte[] responseBuffer = new byte[1000];

            string responseHeader =  stream.ReadHttpHeader();

            string acceptHeader = "Sec-WebSocket-Accept: ";
            int startIndex = responseHeader.IndexOf(acceptHeader) + acceptHeader.Length;
            int endIndex = responseHeader.IndexOf("\r\n", startIndex);
            string responseKey = responseHeader.Substring(startIndex, endIndex - startIndex);

            if (responseKey != expectedResponse)
            {
                throw new WebSocketException(WebSocketError.HeaderError,$"Response key incorrect, Response:{responseKey} Expected:{expectedResponse}");
            }
        }
    }
}