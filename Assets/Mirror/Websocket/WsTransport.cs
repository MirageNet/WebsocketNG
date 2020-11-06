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

        [Tooltip("The name of the file containing a pfx certificate for the server (i.e. certificate.pfx)")]
        [SerializeField]
        internal string CertificateName;

        [Tooltip("The passphrase for the certificate,  use $MYVARIABLE to read an environment variable")]
        [SerializeField]
        internal string Passphrase;

        #region Server
        WebSocketServer server;

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

            if (string.IsNullOrEmpty(CertificateName) )
            {
                server = new WebSocketServer(null);
            }

            else
            {
                // try loading the certificate
                X509Certificate2 certificate;

                string passphrase = GetPassphrase();

                if (passphrase == null)
                    certificate = new X509Certificate2(CertificateName);
                else
                    certificate = new X509Certificate2(CertificateName, passphrase);

                server = new WebSocketServer(certificate);
            }

            server.Connected.AddListener(c => Connected.Invoke(c));
            server.Started.AddListener(() => Started.Invoke());

            return server.Listen(Port);
        }

        private string GetPassphrase()
        {
            if (string.IsNullOrEmpty(Passphrase))
                return null;

            if (Passphrase.StartsWith("$"))
            {
                return Environment.GetEnvironmentVariable(Passphrase.Substring(1));
            }

            return Passphrase;
        }

        public override IEnumerable<Uri> ServerUri()
        {
            if (Application.platform == RuntimePlatform.WebGLPlayer)
                throw new PlatformNotSupportedException("Server mode is not supported in webgl");

            var builder = new UriBuilder
            {
                Host = Dns.GetHostName(),
                Port = Port,
                Scheme = string.IsNullOrEmpty(CertificateName) ? "ws" : "wss"
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
