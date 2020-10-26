using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Mirror.Websocket.Server
{
    using UniTaskChannel = Cysharp.Threading.Tasks.Channel;

    /// <summary>
    /// Connection to a client
    /// </summary>
    public class Connection : IConnection
    {
        private readonly BlockingCollection<MemoryStream> sendQueue = new BlockingCollection<MemoryStream>();
        private readonly Channel<MemoryStream> receiveQueue = UniTaskChannel.CreateSingleConsumerUnbounded<MemoryStream>();

        readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        private readonly TcpClient client;
        readonly Stream stream;

        public Connection(TcpClient client)
        {
            this.client = client;
            stream = client.GetStream();
        }

        public void Handshake()
        {
            ServerHandshake.Handshake(stream);
        }

        public void SendAndReceive()
        {
            try
            {

                var sendThread = new Thread(() =>
                {
                    SendLoop.Loop(sendQueue, stream, cancellationTokenSource.Token);
                })
                {
                    IsBackground = true
                };
                sendThread.Start();

                while (true)
                {
                    MemoryStream message = MessageParser.ReadOneMessage(stream);
                    receiveQueue.Writer.TryWrite(message);
                }
            }
            catch (EndOfStreamException)
            {
                receiveQueue.Writer.TryComplete();
            }
            finally
            {
                cancellationTokenSource.Cancel();
                stream?.Close();
                client?.Close();
            }
        }
        
        public void Disconnect()
        {
            stream?.Close();
            client?.Close();
        }

        public EndPoint GetEndPointAddress()
        {
            return client.Client.RemoteEndPoint;
        }

        public async UniTask<int> ReceiveAsync(MemoryStream buffer)
        {
            try
            {
                MemoryStream receiveMsg = await receiveQueue.Reader.ReadAsync(cancellationTokenSource.Token);
                buffer.SetLength(0);
                receiveMsg.WriteTo(buffer);
                return 0;
            }
            catch (ChannelClosedException)
            {
                throw new EndOfStreamException();
            }
        }

        public UniTask SendAsync(ArraySegment<byte> segment, int channel = 0)
        {
            MemoryStream stream = SendLoop.PackageMessage(segment, false);

            sendQueue.Add(stream);

            return UniTask.CompletedTask;
        }
    }
}