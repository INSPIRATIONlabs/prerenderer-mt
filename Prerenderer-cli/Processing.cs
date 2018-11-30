using PuppeteerSharp;
using System;
using System.Collections;
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

        /// <summary>
        /// Process the element
        /// </summary>
        /// <param name="page"></param>
        /// <param name="pageQueue"></param>
        /// <returns></returns>
        async Task ProcessAsync(RenderPage page, Queue<RenderPage> pageQueue)
        {
            await _semaphore.WaitAsync();
            try
            {
                await Task.Run(async () =>
                {
                    try
                    {
                        string content = await page.GetContentAsync();
                        string url = page.GetOutputPath();
                        pageQueue.Enqueue(page);
                        
                        using (StreamWriter outputFile = new StreamWriter(url))
                        {
                            await outputFile.WriteAsync(content);
                        }

                        ElCount++;
                        Console.WriteLine(ElCount + ": " + page.pageUrl);
                    } catch (Exception er)
                    {
                        Console.WriteLine(er.Message);
                    }
                });
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Add items to the processing queue
        /// </summary>
        /// <param name="page"></param>
        /// <param name="pageQueue"></param>
        public async void QueueItemAsync(RenderPage page, Queue<RenderPage> pageQueue)
        {
            var task = ProcessAsync(page, pageQueue);
            lock (_lock)
                _pending.Add(task);
            try
            {
                await task;
            }
            catch
            {
                if (!task.IsCanceled && !task.IsFaulted)
                    throw;
                return;
            }
            // remove successfully completed tasks from the list 
            lock (_lock)
                _pending.Remove(task);
        }

        /// <summary>
        /// Wait for all tasks to be completed
        /// </summary>
        /// <returns></returns>
        public async Task WaitForCompleteAsync()
        {
            Task[] tasks;
            lock (_lock)
                tasks = System.Linq.Enumerable.ToArray(_pending);
            await Task.WhenAll(tasks);
        }
    }   
}
