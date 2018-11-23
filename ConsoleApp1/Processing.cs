using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace com.inspirationlabs.prerenderer
{
    class Processing
    {
        const int MAX_PROCESSORS = 100;
        SemaphoreSlim _semaphore = new SemaphoreSlim(MAX_PROCESSORS);
        HashSet<Task> _pending = new HashSet<Task>();
        int ElCount = 0;
        object _lock = new Object();

        async Task ProcessAsync(string data, string url, string path)
        {
            await _semaphore.WaitAsync();
            try
            {
                await Task.Run(async () =>
                {
                    string fpath = url.Replace('/', Path.DirectorySeparatorChar);
                    Directory.CreateDirectory(path + fpath);
                    string indexPath = Path.DirectorySeparatorChar + "index.html";
                    string cpath = path + fpath + indexPath;

                    using (StreamWriter outputFile = new StreamWriter(cpath))
                    {
                        await outputFile.WriteAsync(data);
                    }
                    ElCount++;
                    Console.WriteLine(ElCount + ": " + url);
                });
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async void QueueItemAsync(string data, string url, string path)
        {
            var task = ProcessAsync(data, url, path);
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
