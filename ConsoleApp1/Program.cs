using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PuppeteerSharp;

namespace com.inspirationlabs.prerenderer
{
    class Prerenderer
    {
        static void Main(string[] args)
        {
            // wait for MainTask (async)
            Maintask().Wait();
        }

        static async Task Maintask()
        {
            try
            {
                HttpClient client = new HttpClient();
                HttpResponseMessage response = await client.GetAsync("https://api.staging.mydriver-international.com/mydriver-cms/v3/cms/url");
                response.EnsureSuccessStatusCode();

                string responseBody = await response.Content.ReadAsStringAsync();
                JObject jObject = JObject.Parse(responseBody);
                JArray urldata = (JArray)jObject["data"];

                var fetcher = await new BrowserFetcher().DownloadAsync(BrowserFetcher.DefaultRevision);
                await DownloadAsync(urldata);
            } catch(Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        const int MAX_DOWNLOADS = 50;

        // download data
        static async Task DownloadAsync(JArray urls)
        {
            int count = 0;
            Processing processing = new Processing();

            using (SemaphoreSlim semaphore = new SemaphoreSlim(MAX_DOWNLOADS))
            using (Browser browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = false,
                Args =  new[] { "--no-sandbox", "--disable-setuid-sandbox" }
            }))
            {
                var tasks = urls.Select(async (urldata) =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        count++;
                        Page page = await browser.NewPageAsync();
                        page.DefaultNavigationTimeout = 120000;
                        var setIsServer = @"
                            Object.defineProperty(window, 'isServer', {
                                get() {
                                    return true
                                }
                            });
                        ";
                        await page.EvaluateOnNewDocumentAsync(setIsServer);
                        await page.SetRequestInterceptionAsync(true);
                        page.Request += (sender, e) =>
                        {
                            string resType = e.Request.ResourceType.ToString();
                            if (resType == "Image" || resType == "Font")
                            {
                                e.Request.AbortAsync();
                            } else
                            {
                                e.Request.ContinueAsync();
                            }
                        };
                        string path = (string)urldata.SelectToken("url");
                        string url = "http://localhost:2015" + path;
                        await page.GoToAsync(url, new NavigationOptions { WaitUntil = new[] { WaitUntilNavigation.Networkidle0 } });
                        string content = await page.GetContentAsync();
                        await page.CloseAsync();
                        Console.WriteLine(count);
                        // put the result on the processing pipeline
                        // processing.QueueItemAsync(content);
                    }
                    finally
                    {

                        semaphore.Release();
                    }
                });

                await Task.WhenAll(tasks.ToArray());
                // await processing.WaitForCompleteAsync();
            }
        }
    }
}
