using System;
using System.IO;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;

namespace Mirage.Websocket
{
    public static class ReadHelper
    {
        // this isnt an offical max, just a reasonable size for a websocket handshake
        const int MaxHttpHeaderSize = 3000;

        /// <summary>
        /// reads exactly n bytes
        /// </summary>
        public static void ReadExact(this Stream stream, MemoryStream buffer, int length)
        {
            buffer.SetLength(buffer.Position + length);

            int outOffset = (int)buffer.Position;

            int readSoFar = 0;
            while (readSoFar < length)
            {
                int read = stream.Read(buffer.GetBuffer(), outOffset + readSoFar, length - readSoFar);

                if (read == 0)
                    throw new WebSocketException(WebSocketError.ConnectionClosedPrematurely);
                readSoFar += read;
            }

            buffer.Position += length;
        }

        public static byte ReadOneByte(this Stream stream)
        {
            try
            {
                int value = stream.ReadByte();
                if (value < 0)
                    throw new EndOfStreamException();
                return (byte)value;
            }
            catch (ObjectDisposedException)
            {
                throw new EndOfStreamException();
            }
            catch (IOException)
            {
                throw new EndOfStreamException();
            }
            catch (SocketException)
            {
                throw new EndOfStreamException();
            }
        }

        /// <summary>
        /// HTTP headers will end with \r\n\r\n
        /// </summary>
        public static readonly byte[] endOfHeader = Encoding.ASCII.GetBytes("\r\n\r\n");


        public static string ReadHttpHeader(this Stream stream)
        {
            var buffer = new MemoryStream();

            int read = 0;
            int endIndex = 0;
            int endLength = endOfHeader.Length;
            while (true)
            {
                int next = stream.ReadByte();
                if (next == -1)
                    throw new WebSocketException(WebSocketError.ConnectionClosedPrematurely);

                if (read >= MaxHttpHeaderSize)
                    throw new SocketException((int)SocketError.NoBufferSpaceAvailable);

                buffer.WriteByte((byte)next);
                read++;

                // if n is match, check n+1 next
                if (endOfHeader[endIndex] == next)
                {
                    endIndex++;
                    // when all is match return with read length
                    if (endIndex >= endLength)
                    {
                        // convert it all to string
                        return Encoding.ASCII.GetString(buffer.GetBuffer(), 0, read);
                    }
                }
                // if n not match reset to 0
                else
                {
                    endIndex = 0;
                }
            }
        }
    }
}
