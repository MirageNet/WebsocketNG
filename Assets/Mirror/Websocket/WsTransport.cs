using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using Cysharp.Threading.Tasks;
#if !UNITY_WEBGL || UNITY_EDITOR
using Ninja.WebSockets;
#endif

namespace Mirror.Websocket
{


    public class WsTransport : Transport
    {
        public int Port = 7778;

        public override IEnumerable<string> Scheme => new[] { "ws", "wss" };

        // supported in all platforms
        public override bool Supported => true;

#if UNITY_WEBGL && !UNITY_EDITOR

        public override Task<IConnection> AcceptAsync()
        {
            throw new PlatformNotSupportedException("WebGL builds can only be clients");
        }

        public override async Task<IConnection> ConnectAsync(Uri uri)
        {
            if (uri.IsDefaultPort)
            {
                var builder = new UriBuilder(uri)
                {
                    Port = Port
                };
                uri = builder.Uri;
            }

            var connection = new WebsocketConnectionWebGl();

            await connection.ConnectAsync(uri);

            return connection;
        }

        public override void Disconnect()
        {
            throw new PlatformNotSupportedException("WebGL builds can only be clients");
        }

        public override Task ListenAsync()
        {
            throw new PlatformNotSupportedException("WebGL builds can only be clients");
        }

        public override IEnumerable<Uri> ServerUri()
        {
            throw new PlatformNotSupportedException("WebGL builds can only be clients");
        }


#else
#region Server
        private TcpListener listener;
        private readonly IWebSocketServerFactory webSocketServerFactory = new WebSocketServerFactory();

        public override async UniTask<IConnection> AcceptAsync()
        {
            try
            {
                TcpClient tcpClient = await listener.AcceptTcpClientAsync();
                var options = new WebSocketServerOptions { KeepAliveInterval = TimeSpan.FromSeconds(30), SubProtocol = "binary" };

                Stream stream = tcpClient.GetStream();

                WebSocketHttpContext context = await webSocketServerFactory.ReadHttpHeaderFromStreamAsync(tcpClient, stream);

                WebSocket webSocket = await webSocketServerFactory.AcceptWebSocketAsync(context, options);
                return new WebsocketConnection(webSocket);
            }
            catch (ObjectDisposedException)
            {
                // expected,  the connection was closed
                return null;
            }
        }

        public override void Disconnect()
        {
            listener.Stop();
        }

        public override UniTask ListenAsync()
        {
            listener = TcpListener.Create(Port);
            listener.Server.NoDelay = true;
            listener.Start();
            return UniTask.CompletedTask;
        }

        public override IEnumerable<Uri> ServerUri()
        {
            var builder = new UriBuilder
            {
                Host = Dns.GetHostName(),
                Port = Port,
                Scheme = "ws"
            };

            return new[] { builder.Uri };
        }
#endregion

#region Client
        public override async UniTask<IConnection> ConnectAsync(Uri uri)
        {
            var options = new WebSocketClientOptions
            {
                NoDelay = true,
                KeepAliveInterval = TimeSpan.Zero,
                SecWebSocketProtocol = "binary"
            };

            if (uri.IsDefaultPort)
            {
                var builder = new UriBuilder(uri)
                {
                    Port = Port
                };
                uri = builder.Uri;
            }

            var clientFactory = new WebSocketClientFactory();
            WebSocket webSocket = await clientFactory.ConnectAsync(uri, options);

            return new WebsocketConnection(webSocket);
        }

#endregion
#endif

    }

}
