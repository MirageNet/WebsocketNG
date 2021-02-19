using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

namespace Mirage.Websocket.Client
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

            if (sslPolicyErrors == SslPolicyErrors.None)
                return true;

            if (sslPolicyErrors != SslPolicyErrors.RemoteCertificateChainErrors)
                return false;

            if (chain.ChainStatus.Length > 1)
                return false;

            X509ChainStatus chainStatus = chain.ChainStatus[0];

            if (chainStatus.Status != X509ChainStatusFlags.UntrustedRoot)
                return false;

            // problem is untrusted root, let's check our local store


            // let's check our own store for trusted certs
            // note unity cannot read the system store
            using (var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser))
            {
                store.Open(OpenFlags.ReadOnly);

                // get the last certificate in the chain
                X509ChainElement rootCert = chain.ChainElements[chain.ChainElements.Count - 1];

                // and check if we trust it

                X509Certificate2Collection found = store.Certificates.Find(X509FindType.FindByThumbprint, rootCert.Certificate.Thumbprint, true);

                return found.Count > 0;
            }
        }
    }
}
