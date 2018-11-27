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

       
    async Task ProcessAsync(Page page, Queue<Page> pageQueue, string scriptBody, string path, string url)
    {
        await _semaphore.WaitAsync();
        try
        {
            await Task.Run(async () =>
            {
                try
                {
                    await page.WaitForSelectorAsync("app-root.hydrated", new WaitForSelectorOptions
                    {
                        Timeout = 30000
                    });
                    await page.MainFrame.EvaluateFunctionAsync(@"function(){"
                    + scriptBody
                    + "}");
                    string content = await page.GetContentAsync();
                    pageQueue.Enqueue(page);
                    string fpath = url.Replace('/', Path.DirectorySeparatorChar);
                    Directory.CreateDirectory(path + fpath);
                    string indexPath = Path.DirectorySeparatorChar + "index.html";
                    string cpath = path + fpath + indexPath;

                    using (StreamWriter outputFile = new StreamWriter(cpath))
                    {
                        await outputFile.WriteAsync(content);
                    }
                    ElCount++;
                    Console.WriteLine(ElCount + ": " + page.Url);
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
    //async Task ProcessAsync(string data, string url, string path)
    //{
    //    await _semaphore.WaitAsync();
    //    try
    //    {
    //        await Task.Run(async () =>
    //        {
    //            string fpath = url.Replace('/', Path.DirectorySeparatorChar);
    //            Directory.CreateDirectory(path + fpath);
    //            string indexPath = Path.DirectorySeparatorChar + "index.html";
    //            string cpath = path + fpath + indexPath;

    //            using (StreamWriter outputFile = new StreamWriter(cpath))
    //            {
    //                await outputFile.WriteAsync(data);
    //            }
    //            ElCount++;
    //            Console.WriteLine(ElCount + ": " + url);
    //        });
    //    }
    //    finally
    //    {
    //        _semaphore.Release();
    //    }
    //}

    public async void QueueItemAsync(Page page, Queue<Page> pageQueue, string scriptBody, string path, string url)
        {
            var task = ProcessAsync(page, pageQueue, scriptBody, path, url);
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
