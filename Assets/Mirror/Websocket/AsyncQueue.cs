using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Mirror.Websocket
{
    public class AsyncQueue<T>
    {
        // when data arrives it goes here
        private readonly Queue<T> received = new Queue<T>();

        // when a request arrives it goes here
        private readonly Queue<TaskCompletionSource<T>> requests = new Queue<TaskCompletionSource<T>>();

        public AsyncQueue()
        {

        }

        public void Enqueue(T data)
        {
            if (requests.Count > 0)
            {
                TaskCompletionSource<T> completionSource = requests.Dequeue();
                completionSource.SetResult(data);
            }
            else
            {
                received.Enqueue(data);
            }
        }

        public Task<T> DequeueAsync()
        {
            if (received.Count > 0)
            {
                T value = received.Dequeue();
                return Task.FromResult(value);
            }
            else
            {
                var taskCompletionSource = new TaskCompletionSource<T>();
                requests.Enqueue(taskCompletionSource);
                return taskCompletionSource.Task;
            }
        }
    }
}