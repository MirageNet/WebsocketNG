using System;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Mirror.Websocket.Server
{
    using UniTaskChannel = Cysharp.Threading.Tasks.Channel;

    public class WebSocketServer
    {
        private readonly Channel<IConnection> acceptQueue = UniTaskChannel.CreateSingleConsumerUnbounded<IConnection>();

        TcpListener listener;
        private readonly X509Certificate2 certificate;

        public WebSocketServer(X509Certificate2 certificate)
        {
            this.certificate = certificate;
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

                    var conn = new Connection(client, certificate);

                    // handshake needs its own thread as it needs to wait for message from client
                    var receiveThread = new Thread(() => HandshakeAndReceiveLoop(conn));
                    receiveThread.IsBackground = true;
                    receiveThread.Start();
                }

            }
            catch (SocketException)
            {
                // fine,  someone stopped the connection
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
