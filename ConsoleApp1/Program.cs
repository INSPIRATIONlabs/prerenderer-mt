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
        static string Host = "http://127.0.0.1:2015";
        static int Threads = 50;
        static string Jsonurl = "https://api.staging.mydriver-international.com/mydriver-cms/v3/cms/url";
        static string OutputPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + "/output";
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
                if(OutputPath.Length > 0 && Directory.Exists(OutputPath))
                {
                    Directory.Delete(OutputPath, true);
                }
                if(OutputPath.Length > 0)
                {
                    Console.WriteLine("Creating outputpath " + OutputPath);
                    Cwd = Directory.CreateDirectory(OutputPath);
                }
            } catch(Exception e)
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

                var fetcher = await new BrowserFetcher().DownloadAsync(BrowserFetcher.DefaultRevision);
                await DownloadAsync(urldata);
            } catch(Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
        
        // download data
        static async Task DownloadAsync(JArray urls)
        {
            int count = 0;
            Processing processing = new Processing();

            using (SemaphoreSlim semaphore = new SemaphoreSlim(Threads))
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
                        string url = Host + path;
                        await page.GoToAsync(url, new NavigationOptions { WaitUntil = new[] { WaitUntilNavigation.Networkidle0 } });
                        string content = await page.GetContentAsync();
                        
                        await page.CloseAsync();
                        using (StreamWriter outputFile = new StreamWriter(Path.Combine(Path.Combine(OutputPath, url), "/index.html")))
                        {
                            await outputFile.WriteAsync(content);
                        }
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
