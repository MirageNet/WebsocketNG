using System;
using System.Net.Sockets;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Mirror.Websocket.Server
{
    using UniTaskChannel = Cysharp.Threading.Tasks.Channel;

    public class WebSocketServer
    {
        private readonly Channel<IConnection> acceptQueue = UniTaskChannel.CreateSingleConsumerUnbounded<IConnection>();
        private readonly int maxMessageSize;

        TcpListener listener;

        public WebSocketServer(int maxMessageSize, SslConfig sslConfig)
        {
            this.maxMessageSize = maxMessageSize;
        }

        public void Listen(int port)
        {
            listener = TcpListener.Create(port);
            listener.Start();

            var acceptThread = new Thread(acceptLoop)
            {
                IsBackground = true
            };
            acceptThread.Start();
        }

        public async UniTask<IConnection> AcceptAsync()
        {
            IConnection connection = await acceptQueue.Reader.ReadAsync();

            // switch to main thread
            await UniTask.SwitchToMainThread();

            return connection;
        }

        public void Stop()
        {
            listener?.Stop();
        }

        void acceptLoop()
        {
            try
            {
                while (true)
                {
                    TcpClient client = listener.AcceptTcpClient();

                    var conn = new Connection(client);

                    // handshake needs its own thread as it needs to wait for message from client
                    var receiveThread = new Thread(() => HandshakeAndReceiveLoop(conn));
                    receiveThread.IsBackground = true;
                    receiveThread.Start();
                }

            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                throw;
            }
            finally
            {
                acceptQueue.Writer.TryComplete();
                listener?.Stop();
            }

        }

        void HandshakeAndReceiveLoop(Connection conn)
        {
            conn.Handshake();

            acceptQueue.Writer.TryWrite(conn);

            conn.SendAndReceive();
        }
    }
}
