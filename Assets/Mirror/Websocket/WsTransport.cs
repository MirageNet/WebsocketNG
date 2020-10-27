using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using Cysharp.Threading.Tasks;
using Mirror.Websocket.Client;
using Mirror.Websocket.Server;
using UnityEngine;

namespace Mirror.Websocket
{

    public class WsTransport : Transport
    {
        [Tooltip("Port to use for server and client")]
        public int Port = 7778;

        public override IEnumerable<string> Scheme => new[] { "ws", "wss" };

        [Tooltip("disables nagle algorithm. lowers CPU% and latency but increases bandwidth")]
        public bool noDelay = true;

        // supported in all platforms
        public override bool Supported => true;

        // if specified the server does wss
        public X509Certificate2 certificate;

        #region Server
        WebSocketServer server;

        public override async UniTask<IConnection> AcceptAsync()
        {
            if (Application.platform == RuntimePlatform.WebGLPlayer)
                throw new PlatformNotSupportedException("Server mode is not supported in webgl");

            try
            {
                return await server.AcceptAsync();
            }
            catch (ObjectDisposedException)
            {
                // expected,  the connection was closed
                return null;
            }
            catch (ChannelClosedException)
            {
                return null;
            }
            finally
            {
                await UniTask.SwitchToMainThread();
            }
        }

        public override void Disconnect()
        {
            if (Application.platform == RuntimePlatform.WebGLPlayer)
                throw new PlatformNotSupportedException("Server mode is not supported in webgl");
            server.Stop();
        }

        public override UniTask ListenAsync()
        {
            if (Application.platform == RuntimePlatform.WebGLPlayer)
                throw new PlatformNotSupportedException("Server mode is not supported in webgl");

            server = new WebSocketServer(certificate);

            server.Listen(Port);

            return UniTask.CompletedTask;
        }

        public override IEnumerable<Uri> ServerUri()
        {
            if (Application.platform == RuntimePlatform.WebGLPlayer)
                throw new PlatformNotSupportedException("Server mode is not supported in webgl");

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
            if (uri.IsDefaultPort)
            {
                var builder = new UriBuilder(uri)
                {
                    Port = Port
                };
                uri = builder.Uri;
            }



            if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                var wsClient = new ConnectionWebGl();
                await wsClient.ConnectAsync(uri);
                return wsClient;
            }
            else
            {
                var wsClient = new ConnectionStandAlone();
                await wsClient.ConnectAsync(uri);
                return wsClient;
            }

        }

        #endregion

    }

}
