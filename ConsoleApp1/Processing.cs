using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace com.inspirationlabs.prerenderer
{
    class Processing
    {
        const int MAX_PROCESSORS = 100;
        SemaphoreSlim _semaphore = new SemaphoreSlim(MAX_PROCESSORS);
        HashSet<Task> _pending = new HashSet<Task>();
        object _lock = new Object();

        async Task ProcessAsync(string data)
        {
            await _semaphore.WaitAsync();
            try
            {
                await Task.Run(() =>
                {
                    // simuate work
                    //Thread.Sleep(1000);
                    Console.WriteLine("done");
                });
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async void QueueItemAsync(string data)
        {
            var task = ProcessAsync(data);
            lock (_lock)
                _pending.Add(task);
            try
            {
                await task;
            }
            catch
            {
                if (!task.IsCanceled && !task.IsFaulted)
                    throw; // not the task's exception, rethrow
                           // don't remove faulted/cancelled tasks from the list
                return;
            }
            // remove successfully completed tasks from the list 
            lock (_lock)
                _pending.Remove(task);
        }

        public async Task WaitForCompleteAsync()
        {
            Task[] tasks;
            lock (_lock)
                tasks = System.Linq.Enumerable.ToArray(_pending);
            await Task.WhenAll(tasks);
        }
    }
}
