using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Mirror.Websocket.Client
{
    using UniTaskChannel = Cysharp.Threading.Tasks.Channel;

    public class ConnectionStandAlone : IConnection
    {
        readonly TcpClient client = new TcpClient();
        readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        private Stream stream;
        private readonly BlockingCollection<MemoryStream> sendQueue = new BlockingCollection<MemoryStream>();

        private readonly Channel<MemoryStream> receiveQueue = UniTaskChannel.CreateSingleConsumerUnbounded<MemoryStream>();

        internal ConnectionStandAlone()
        {
        }

        private UniTaskCompletionSource connectCompletionSource;

        public async UniTask ConnectAsync(Uri serverAddress)
        {
            connectCompletionSource = new UniTaskCompletionSource();

            var receiveThread = new Thread(() => ConnectAndReceiveLoop(serverAddress))
            {
                IsBackground = true
            };
            receiveThread.Start();
            await connectCompletionSource.Task;
            await UniTask.SwitchToMainThread();
        }

        void ConnectAndReceiveLoop(Uri uri)
        {
            // connect and handshake
            try
            {
                Connect(uri);
                connectCompletionSource.TrySetResult();

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
                    MemoryStream message = Parser.ReadOneMessage(stream);
                    receiveQueue.Writer.TryWrite(message);
                }
            }
            catch (EndOfStreamException)
            {
                connectCompletionSource.TrySetResult();
                receiveQueue.Writer.TryComplete();
            }
            catch (Exception e)
            {
                connectCompletionSource.TrySetException(e);
            }
            finally
            {
                cancellationTokenSource.Cancel();
                stream?.Close();
                client?.Close();
            }
        }

        private void Connect(Uri uri)
        {
            try
            {
                client.Connect(uri.Host, uri.Port);
                // add ssl if needed
                stream = ClientSslHelper.CreateStream(client.GetStream(), uri);
                ClientHandshake.Handshake(stream, uri);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                client?.Close();
                connectCompletionSource.TrySetException(e);
                throw;
            }

            connectCompletionSource.TrySetResult();
        }

        public void Disconnect()
        {
            cancellationTokenSource.Cancel();
            client.Close();
        }

        public UniTask SendAsync(ArraySegment<byte> segment, int channel)
        {
            MemoryStream stream = SendLoop.PackageMessage(segment, true);

            sendQueue.Add(stream);

            return UniTask.CompletedTask;
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
            catch (OperationCanceledException)
            {
                throw new EndOfStreamException();
            }
            catch (ChannelClosedException)
            {
                throw new EndOfStreamException();
            }
            finally
            {
                await UniTask.SwitchToMainThread();
            }
        }

        public EndPoint GetEndPointAddress()
        {
            return client.Client.RemoteEndPoint;
        }
    }
}
