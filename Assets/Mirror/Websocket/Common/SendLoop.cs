using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;

namespace Mirror.Websocket
{
    internal static class SendLoop
    {
        public static void Loop(BlockingCollection<MemoryStream> queue, Stream stream, CancellationToken cancellationToken)
        {
            try
            {
                // create write buffer for this thread
                // wait for message
                while (true)
                {
                    MemoryStream msg = queue.Take(cancellationToken);

                    stream.Write(msg.GetBuffer(), 0, (int)msg.Length);
                    
                    if (queue.Count == 0) {
                        stream.Flush();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // fine, someone stopped the connection
            }
            catch (SocketException)
            {
                // fine, someone stopped the connection
            }
        }

        // 0               1                 2               3          
        // 0 1 2 3 4 5 6 7 0 1 2 3 4 5 6 7 8 0 1 2 3 4 5 6 7 0 1 2 3 4 5 6 7
        // +-+-+-+-+-------+-+-------------+-------------------------------+
        // |F|R|R|R| opcode|M| Payload len |    Extended payload length    |
        // |I|S|S|S|  (4)  |A|     (7)     |             (16/64)           |
        // |N|V|V|V|       |S|             |   (if payload len==126/127)   |
        // | |1|2|3|       |K|             |                               |
        // +-+-+-+-+-------+-+-------------+ - - - - - - - - - - - - - - - +
        // |     Extended payload length continued, if payload len == 127  |
        // + - - - - - - - - - - - - - - - +-------------------------------+
        // |                               |Masking-key, if MASK set to 1  |
        // +-------------------------------+-------------------------------+
        // | Masking-key(continued)        |          Payload Data         |
        // +-------------------------------- - - - - - - - - - - - - - - - +
        // :                     Payload Data continued...                 :
        // + - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - +
        // |                     Payload Data continued...                 |
        // +---------------------------------------------------------------+
        /// <summary>
        /// Puts the data in a websocket envelope
        /// </summary>
        /// <returns></returns>
        internal static MemoryStream PackageMessage(ArraySegment<byte>data, bool masked)
        {
            int msgLength = data.Count;
            var buffer = new MemoryStream(data.Count + 16);
            const byte byte0 = Parser.FINISH_BIT | Parser.OPCODE_BINARY;

            buffer.WriteByte(byte0);
            byte maskbit = masked ? Parser.MASK_BIT : (byte)0;

            // write length,  3 possible formats
            if (msgLength < 126)
            {
                byte byte1 = (byte)(maskbit | msgLength);
                buffer.WriteByte(byte1);
            }
            else if (msgLength <= ushort.MaxValue)
            {
                byte byte1 = (byte)(maskbit | 126);
                buffer.WriteByte(byte1);

                buffer.WriteByte((byte)(msgLength >> 8));
                buffer.WriteByte((byte)msgLength );
            }
            else
            {
                byte byte1 = (byte)(maskbit | 127);
                buffer.WriteByte(byte1);
                long length = msgLength;

                for (int i = 7 * 8; i >= 0; i -= 8)
                    buffer.WriteByte((byte)(length >> i));
            }

            uint mask = 0;

            if (masked)
            {
                mask = GetMask();
                buffer.WriteByte((byte)(mask >> 24));
                buffer.WriteByte((byte)(mask >> 16));
                buffer.WriteByte((byte)(mask >> 8));
                buffer.WriteByte((byte)mask);
            }

            long position = buffer.Position;

            buffer.Write(data.Array, data.Offset, data.Count);

            if (masked)
            {
                Parser.ToggleMask(buffer.GetBuffer(), (int)position, data.Count, mask);
            }
            return buffer;
        }

        private static uint GetMask()
        {
            byte[] maskBytes = new byte[4];
            using (var random = new RNGCryptoServiceProvider())
            {
                random.GetBytes(maskBytes);

                uint mask = 0;
                for (int i=0; i< maskBytes.Length; i++)
                {
                    mask = (mask << 8) | maskBytes[i];
                }
                return mask;
            }
        }

    }
}
