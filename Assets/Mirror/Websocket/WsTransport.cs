using System;
using System.Collections.Generic;
using System.Net;
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

        [Tooltip("Protect against allocation attacks by keeping the max message size small. Otherwise an attacker might send multiple fake packets with 2GB headers, causing the server to run out of memory after allocating multiple large packets.")]
        public int maxMessageSize = 16 * 1024;

        [Tooltip("disables nagle algorithm. lowers CPU% and latency but increases bandwidth")]
        public bool noDelay = true;

        [Tooltip("Send would stall forever if the network is cut off during a send, so we need a timeout (in milliseconds)")]
        public int sendTimeout = 5000;

        [Tooltip("How long without a message before disconnecting (in milliseconds)")]
        public int receiveTimeout = 20000;

        [Tooltip("Caps the number of messages the server will process per tick. Allows LateUpdate to finish to let the reset of unity contiue incase more messages arrive before they are processed")]
        public int serverMaxMessagesPerTick = 10000;

        [Tooltip("Caps the number of messages the client will process per tick. Allows LateUpdate to finish to let the reset of unity contiue incase more messages arrive before they are processed")]
        public int clientMaxMessagesPerTick = 1000;


        // supported in all platforms
        public override bool Supported => true;

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

            // TODO: configure ssl
            server = new WebSocketServer(maxMessageSize, default);

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
