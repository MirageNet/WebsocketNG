using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using AOT;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Mirage.Websocket.Client
{
    using Channel = Cysharp.Threading.Tasks.Channel;

    // this is the client implementation used by browsers
    public class ConnectionWebGl : IConnection
    {
        static int idGenerator = 0;
        static readonly Dictionary<int, ConnectionWebGl> clients = new Dictionary<int, ConnectionWebGl>();

        readonly Channel<byte[]> receivedQueue = Channel.CreateSingleConsumerUnbounded<byte[]>();

        int nativeRef = 0;
        readonly int id;

        public ConnectionWebGl()
        {
            id = Interlocked.Increment(ref idGenerator);
        }

        private Uri uri;

        UniTaskCompletionSource connectCompletionSource;
        public UniTask ConnectAsync(Uri uri)
        {
            clients[id] = this;
            connectCompletionSource = new UniTaskCompletionSource();

            this.uri = uri;

            nativeRef = SocketCreate(uri.ToString(), id, OnOpen, OnData, OnClose);
            return connectCompletionSource.Task;
        }

        public void Disconnect()
        {
            SocketClose(nativeRef);
        }

        // send the data or throw exception
        public UniTask SendAsync(ArraySegment<byte> segment, int channel)
        {
            SocketSend(nativeRef, segment.Array, segment.Count);
            return UniTask.CompletedTask;
        }

#region Javascript native functions
        [DllImport("__Internal")]
        static extern int SocketCreate(
            string url,
            int id,
            Action<int> onpen,
            Action<int, IntPtr, int> ondata,
            Action<int> onclose);

        [DllImport("__Internal")]
        static extern int SocketState(int socketInstance);

        [DllImport("__Internal")]
        static extern void SocketSend(int socketInstance, byte[] ptr, int length);

        [DllImport("__Internal")]
        static extern void SocketClose(int socketInstance);

#endregion

#region Javascript callbacks

        [MonoPInvokeCallback(typeof(Action))]
        public static void OnOpen(int id)
        {
            clients[id].connectCompletionSource.TrySetResult();
        }

        [MonoPInvokeCallback(typeof(Action))]
        public static void OnClose(int id)
        {
            clients[id].receivedQueue.Writer.Complete();
            clients.Remove(id);
        }

        [MonoPInvokeCallback(typeof(Action))]
        public static void OnData(int id, IntPtr ptr, int length)
        {
            // TODO: buffer pool
            byte[] data = new byte[length];
            Marshal.Copy(ptr, data, 0, length);

            clients[id].receivedQueue.Writer.TryWrite(data);
        }

        public async UniTask<int> ReceiveAsync(MemoryStream buffer)
        {
            try
            {
                byte[] data = await receivedQueue.Reader.ReadAsync();
                buffer.SetLength(0);

                buffer.Write(data, 0, data.Length);
                Debug.Log("Received data" + BitConverter.ToString(data));

                return 0;
            }
            catch (ChannelClosedException)
            {
                throw new EndOfStreamException();
            }
        }

        public EndPoint GetEndPointAddress()
        {
            return new DnsEndPoint(uri.Host, uri.Port);
        }
#endregion
    }
}
