using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace Mirror.Websocket.Server
{
    public struct SslConfig
    {
    }

    internal class ServerSslHelper
    {
        readonly X509Certificate2 certificate;

        Stream CreateStream(NetworkStream stream)
        {
            var sslStream = new SslStream(stream, true);
            sslStream.AuthenticateAsServer(certificate, false, SslProtocols.Tls12, false);

            return sslStream;
        }
    }
}
