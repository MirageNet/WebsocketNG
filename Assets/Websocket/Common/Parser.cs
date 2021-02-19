using System.IO;
using System.Net.WebSockets;

namespace Mirage.Websocket
{
    struct MessageHeader
    {
        internal bool finished;
        internal bool masked;
        internal int opcode;
        internal uint mask;
        internal long length;
    }

    public static class Parser
    {
        public const byte OPCODE_BINARY = 2;
        public const byte OPCODE_CLOSE = 8;
        public const byte OPCODE_MASK = 0b0000_1111;

        public const byte FINISH_BIT = 0b1000_0000;
        public const byte MASK_BIT = 0b1000_0000;
        public const byte LENGTH_MASK = 0b0111_1111;

        // lets do 100Kb max
        const int MaxMessageSize = 100_000;

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

        private static MessageHeader ReadHeader(Stream stream )
        {
            byte byte0 = stream.ReadOneByte();

            bool finished = (byte0 & FINISH_BIT) != 0;
            int opcode = byte0 & OPCODE_MASK;

            byte byte1 = stream.ReadOneByte();

            bool masked = (byte1 & MASK_BIT) != 0;

            long length = byte1 & LENGTH_MASK;

            if (length == 126)
            {
                length = stream.ReadOneByte();
                length = (length << 8) | stream.ReadOneByte();
            }
            else if (length == 127)
            {
                for (int i = 0; i < 8; i++)
                {
                    length = (length << 8) | stream.ReadOneByte();
                }
            }
            uint mask = 0;

            if (masked)
            {
                mask = stream.ReadOneByte();
                mask = (mask << 8) | stream.ReadOneByte();
                mask = (mask << 8) | stream.ReadOneByte();
                mask = (mask << 8) | stream.ReadOneByte();
            }

            var header =  new MessageHeader
            {
                finished = finished,
                masked = masked,
                length = length,
                opcode = opcode,
                mask = mask
            };

            Validate(header);
            return header;
        }

        private static void Validate(MessageHeader header)
        {
            if (!header.finished)
                throw new WebSocketException(WebSocketError.Faulted, "We don't support fragments yet");

            if (header.length == 0)
            {
                throw new WebSocketException(WebSocketError.NotAWebSocket);
            }

            if (header.length > MaxMessageSize)
            {
                throw new WebSocketException(WebSocketError.Faulted, $"Message is too long {header.length}");
            }
        }

        internal static void ToggleMask(byte[] src, int srcOffset, int messageLength, uint mask)
        {
            for (int i = 0; i < messageLength; i++)
            {
                // max index = 3 2 1 0 3 2 1 0 3 2 1 0 ....
                int maskIndex = 3 - (i & 0x3);
                byte maskByte = (byte)(mask >> (maskIndex * 8) );
                src[srcOffset + i] = (byte)(src[srcOffset + i] ^ maskByte);
            }
        }

        public static MemoryStream ReadOneMessage(Stream stream)
        {
            var buffer = new MemoryStream();

            MessageHeader header = ReadHeader(stream);

            ReadMessagePayload(stream, buffer, header);

            MessageHeader fragmentHeader = header;
            while (!fragmentHeader.finished)
            {
                fragmentHeader = ReadHeader(stream);
                ReadMessagePayload(stream, buffer, fragmentHeader);
            }

            switch (header.opcode)
            {
                case OPCODE_BINARY:
                    return buffer;
                case OPCODE_CLOSE:
                    throw new EndOfStreamException();
                default:
                    throw new WebSocketException(WebSocketError.InvalidMessageType, "Expected a binary or close message");
            }
        }

        private static void ReadMessagePayload(Stream stream, MemoryStream buffer, MessageHeader header)
        {
            long start = buffer.Position;

            stream.ReadExact(buffer, (int)header.length);

            if (header.masked)
            {
                ToggleMask(buffer.GetBuffer(), (int)start, (int)header.length, header.mask);
            }
        }
    }
}
