using System;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.Events;

namespace Mirror.Websocket.Server
{
    public class WebSocketServer
    {
        internal Transport.ConnectEvent Connected = new Transport.ConnectEvent();
        internal UnityEvent Started = new UnityEvent();

        TcpListener listener;
        private readonly X509Certificate2 certificate;

        public WebSocketServer(X509Certificate2 certificate)
        {
            this.certificate = certificate;
        }

        public WebSocketServer()
        {
        }

        AutoResetUniTaskCompletionSource listenCompletion;

        public async UniTask Listen(int port)
        {
            try
            {

                listener = TcpListener.Create(port);
                listener.Start();

                listenCompletion = AutoResetUniTaskCompletionSource.Create();
                var acceptThread = new Thread(AcceptLoop)
                {
                    IsBackground = true
                };
                acceptThread.Start();

                Started.Invoke();

                await listenCompletion.Task;

                await UniTask.SwitchToMainThread();
            }
            finally
            {
                listener?.Stop();
            }
        }

        public void Stop()
        {
            listener?.Stop();
        }

        void AcceptLoop()
        {
            try
            {
                while (true)
                {
                    TcpClient client = listener.AcceptTcpClient();

                    var conn = new Connection(client, certificate);

                    // handshake needs its own thread as it needs to wait for message from client
                    var receiveThread = new Thread(() => HandshakeAndReceiveLoop(conn))
                    {
                        IsBackground = true
                    };
                    receiveThread.Start();
                }

            }
            catch (SocketException)
            {
                // fine,  someone stopped the connection
            }
            catch (Exception ex)
            {
                listenCompletion.TrySetException(ex);
            }
            finally
            {
                listenCompletion.TrySetResult();
            }

        }

        void HandshakeAndReceiveLoop(Connection conn)
        {
            conn.Handshake();

            NotifyAccept(conn).Forget();

            conn.SendAndReceive();
        }

        private async UniTaskVoid NotifyAccept(Connection conn)
        {
            await UniTask.SwitchToMainThread();
            Connected.Invoke(conn);
        }
    }
}
