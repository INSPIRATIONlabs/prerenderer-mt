using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NDesk.Options;
using PuppeteerSharp;
using System.Reflection;

namespace com.inspirationlabs.prerenderer
{
    class Prerenderer
    {
        static string Host = "http://localhost:2015";
        static int Threads = Environment.ProcessorCount * 20;
        static string Jsonurl = "https://api.staging.mydriver-international.com/mydriver-cms/v3/cms/url";
        static string OutputPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + Path.DirectorySeparatorChar + "output";
        static DirectoryInfo Cwd;
        static void Main(string[] args)
        {
            var p = new OptionSet() {
               { "host=", "Set the hostname", v => Host = v },
               { "threads=", "Set the amount of paralell threads", (int v) => Threads = v },
               { "jsonurl=", "Set the endpoint url to get the url list", v => Jsonurl = v},
               { "outputpath=", "Set the path to output the contents", v => OutputPath = v }
            };
            List<string> extra = p.Parse(args);

            try
            {
                // delete outputpath if it exists
                if (OutputPath.Length > 0 && Directory.Exists(OutputPath))
                {
                    Directory.Delete(OutputPath, true);
                }
                if (OutputPath.Length > 0)
                {
                    Console.WriteLine("Creating outputpath " + OutputPath);
                    Cwd = Directory.CreateDirectory(OutputPath);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            // wait for MainTask (async)
            Maintask().Wait();
        }

        static async Task Maintask()
        {
            try
            {
                HttpClient client = new HttpClient();
                HttpResponseMessage response = await client.GetAsync(Jsonurl);
                response.EnsureSuccessStatusCode();

                string responseBody = await response.Content.ReadAsStringAsync();
                JObject jObject = JObject.Parse(responseBody);
                JArray urldata = (JArray)jObject["data"];
                urldata.AddFirst(JToken.Parse("{\"url\": \"/\", \"published\":true, \"indexed\":true,\"followed\":true}"));
                urldata.AddFirst(JToken.Parse("{\"url\": \"/de\", \"published\":true, \"indexed\":true,\"followed\":true}"));
                urldata.AddFirst(JToken.Parse("{\"url\": \"/en\", \"published\":true, \"indexed\":true,\"followed\":true}"));
                urldata.AddFirst(JToken.Parse("{\"url\": \"/es\", \"published\":true, \"indexed\":true,\"followed\":true}"));
                urldata.AddFirst(JToken.Parse("{\"url\": \"/it\", \"published\":true, \"indexed\":true,\"followed\":true}"));
                urldata.AddFirst(JToken.Parse("{\"url\": \"/nl\", \"published\":true, \"indexed\":true,\"followed\":true}"));
                urldata.AddFirst(JToken.Parse("{\"url\": \"/fr\", \"published\":true, \"indexed\":true,\"followed\":true}"));

                // testing
                while (urldata.Count >= 200)
                {
                    urldata.Remove(urldata.Last);
                }

                var fetcher = await new BrowserFetcher().DownloadAsync(BrowserFetcher.DefaultRevision);
                await DownloadAsync(urldata);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        // download data
        static async Task DownloadAsync(JArray urls)
        {
            Processing processing = new Processing();
            Queue<Page> qt = new Queue<Page>();

            try
            {
                using (SemaphoreSlim semaphore = new SemaphoreSlim(Threads))
                using (Browser browser = await Puppeteer.LaunchAsync(new LaunchOptions
                {
                    Headless = true,
                    Args = new[] { "--no-sandbox", "--disable-setuid-sandbox" }
                }))
                {
                    for (int i = 0; i <= Threads + 50; i++)
                    {
                        Page page = await browser.NewPageAsync();
                        page.DefaultNavigationTimeout = 150000;
                        var setIsServer = @"
                            function(){
                                Object.defineProperty(window, 'isServer', {
                                    get() {
                                        return true
                                    }
                                });
                            }
                        ";
                        await page.EvaluateOnNewDocumentAsync(setIsServer);

                        qt.Enqueue(page);
                    }

                    String scriptBody = await File.ReadAllTextAsync("assets/prerender.js");
                    var tasks = urls.Select(async (urldata) =>
                    {
                        await semaphore.WaitAsync();
                        Page page = qt.Dequeue();
                        try
                        {
                            string path = (string)urldata.SelectToken("url");
                            string url = Host + path;

                            await page.GoToAsync(url, new NavigationOptions { WaitUntil = new[] { WaitUntilNavigation.Networkidle0 } });

                            await page.MainFrame.EvaluateFunctionAsync(@"function(){"
                            + scriptBody
                            + "}");
                            string content = await page.GetContentAsync();

                            // put the result on the processing pipeline
                            processing.QueueItemAsync(content, path, OutputPath);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.Message);
                        }
                        finally
                        {
                            qt.Enqueue(page);
                            semaphore.Release();
                        }

                    });

                    await Task.WhenAll(tasks.ToArray());
                    await processing.WaitForCompleteAsync();
                    await browser.CloseAsync();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
    }
}
