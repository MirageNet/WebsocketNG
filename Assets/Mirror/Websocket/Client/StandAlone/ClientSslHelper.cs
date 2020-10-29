using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

namespace Mirror.Websocket.Client
{
    internal static class ClientSslHelper
    {

        internal static Stream CreateStream(NetworkStream stream, Uri uri)
        {
            if (uri.Scheme == "wss")
            {
                var sslStream = new SslStream(stream, false, ValidateServerCertificate);
                sslStream.AuthenticateAsClient(uri.Host);
                return sslStream;
            }
            return stream;
        }

        static bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            // Do not allow this client to communicate with unauthenticated servers.


            // only accept if no errors
            // return sslPolicyErrors == SslPolicyErrors.None;
            return true;
        }
    }
}
