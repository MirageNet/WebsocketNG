using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Mirror.Websocket
{
    public static class Nonce
    {
        /// <summary>
        /// Guid used for WebSocket Protocol
        /// </summary>
        private const string HandshakeGUID = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

        public static string Hash(string key)
        {
            key += HandshakeGUID;

            using (var sha1 = SHA1.Create())
            {
                byte[] hash = sha1.ComputeHash(Encoding.ASCII.GetBytes(key));

                return Convert.ToBase64String(hash);
            }
        }
    }
}
