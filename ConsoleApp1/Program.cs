using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using PuppeteerSharp;
using System.Reflection;
using CommandLine;

namespace com.inspirationlabs.prerenderer
{
    class Prerenderer
    {
        public class Options
        {
            [Option('h', "host",
                Required = true,
                Default = "http://localhost:2015",
                HelpText = "Host url to render")]
            public string Host { get; set; }

            [Option('t', "threads",
               Required = true,
               HelpText = "Thread count")]
            public int Threads { get; set; }

            [Option('u', "urls",
               Required = true,
                Default = "https://api.mydriver.com/mydriver-cms/v3/cms/url",
               HelpText = "http url to the list of urls in json format")]
            public string Urls { get; set; }

            [Option('c', "chromepath",
               Required = true,
               HelpText = "Path to chromium binary")]
            public string ChromePath { get; set; }

            [Option('o', "output",
               Required = true,
               HelpText = "Path to output the data")]
            public string OutputPath { get; set; }

            [Option('s', "source",
               Required = true,
               HelpText = "Sourcepath to the build files of the js project")]
            public string SourcePath { get; set; }
        }

        static string Host = "http://localhost:2015";
        static int Threads = Environment.ProcessorCount;
        static string Jsonurl = "https://api.mydriver.com/mydriver-cms/v3/cms/url";
        static string OutputPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + Path.DirectorySeparatorChar + "output";
        static string SourcePath = "C:\\Users\\eberhardschneider\\Development\\sixt\\mydriver-seo-web\\www";
        static string ChromiumPath = "/usr/bin/chromium-browser";
        static DirectoryInfo Cwd;
        static void Main(string[] args)
        {
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
                // copy assets etc from source if they exist
                if (Directory.Exists(SourcePath)
                )
                {
                    List<string> dirs = new List<string>() {"assets", "build", "contents"};
                    dirs.ForEach((name) =>
                    {
                    if (Directory.Exists(SourcePath + Path.DirectorySeparatorChar + name))
                        {
                            CopyDirectory(
                                SourcePath + Path.DirectorySeparatorChar + name,
                                OutputPath + Path.DirectorySeparatorChar + name
                            );
                        }
                    });
                    if (File.Exists(SourcePath + Path.DirectorySeparatorChar + "robots.txt"))
                    {
                        File.Copy(
                            SourcePath + Path.DirectorySeparatorChar + "robots.txt",
                            OutputPath + Path.DirectorySeparatorChar + "robots.txt"
                        );
                    }
                    if (File.Exists(SourcePath + Path.DirectorySeparatorChar + "manifest.json"))
                    {
                        File.Copy(
                            SourcePath + Path.DirectorySeparatorChar + "manifest.json",
                            OutputPath + Path.DirectorySeparatorChar + "manifest.json"
                        );
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            // wait for MainTask (async)
            Maintask().Wait();
            Console.WriteLine("Press any key to quit");
            Console.ReadKey();
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

                //testing
                //while (urldata.Count >= 200)
                //{
                //    urldata.Remove(urldata.Last);
                //}

                // var fetcher = await new BrowserFetcher().DownloadAsync(BrowserFetcher.DefaultRevision);
                await DownloadAsync(urldata);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        static async Task<Page> StartPage(Browser browser)
        {
            Page p = await browser.NewPageAsync();
            // p.DefaultNavigationTimeout = 120000;
            var setIsServer = @"
                            function(){
                                Object.defineProperty(window, 'isServer', {
                                    get() {
                                        return true
                                    }
                                });
                            }
                        ";
            await p.EvaluateOnNewDocumentAsync(setIsServer);
            await p.SetRequestInterceptionAsync(true);
            p.Request += (sender, e) =>
            {
                string resType = e.Request.ResourceType.ToString();
                if (resType == "Image" || resType == "Font")
                {
                    e.Request.AbortAsync();
                }
                else
                {
                    e.Request.ContinueAsync();
                }
            };
            return p;
        }

        static async Task<string> GetContent(string url, Page page, string scriptBody, Browser browser)
        {
            if (page == null || page.IsClosed)
            {
                page = await StartPage(browser);
            }
            await page.GoToAsync(url, new NavigationOptions
            {
                WaitUntil = new[]
                {
                     WaitUntilNavigation.Load
                }
            });
            await page.WaitForSelectorAsync("app-root.hydrated", new WaitForSelectorOptions
            {
                Timeout = 5000
            });
            await page.MainFrame.EvaluateFunctionAsync(@"function(){"
                + scriptBody
                + "}");
            string content = await page.GetContentAsync();
            return content;
        }

        // download data
        static async Task DownloadAsync(JArray urls)
        {
            Processing processing = new Processing();
            Queue<Page> qt = new Queue<Page>();

            try
            {
                using (SemaphoreSlim semaphore = new SemaphoreSlim(Threads * 2))
                using (Browser browser = await Puppeteer.LaunchAsync(new LaunchOptions
                {
                    Headless = true,
                    ExecutablePath = ChromiumPath,
                    Args = new[] { "--no-sandbox", "--disable-setuid-sandbox" }
                }))
                {
                    for (int i = 0; i <= Threads * 4; i++)
                    {
                        Page page = await StartPage(browser);
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

                            string content = await GetContent(url, page, scriptBody, browser);
                            
                            // put the result on the processing pipeline
                            processing.QueueItemAsync(content, path, OutputPath);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.Message + " (" + (string)urldata.SelectToken("url") + ")");
                            Console.WriteLine(e.StackTrace);
                            try
                            {
                                string path = (string)urldata.SelectToken("url");
                                string url = Host + path;
                                string content = await GetContent(url, page, scriptBody, browser);
                                processing.QueueItemAsync(content, path, OutputPath);
                            } catch(Exception er)
                            {
                                Console.WriteLine(er.Message + " (" + (string)urldata.SelectToken("url") + ")");
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine("Second error!!!");
                                Console.ForegroundColor = ConsoleColor.Gray;
                            }
                        }
                        finally {
                            if (!page.IsClosed)
                            {
                                qt.Enqueue(page);
                            }
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

        static void CopyDirectory(string SourcePath, string DestinationPath)
        {
            //Now Create all of the directories
            foreach (string dirPath in Directory.GetDirectories(SourcePath, "*",
                SearchOption.AllDirectories))
                Directory.CreateDirectory(dirPath.Replace(SourcePath, DestinationPath));

            //Copy all the files & Replaces any files with the same name
            foreach (string newPath in Directory.GetFiles(SourcePath, "*.*",
                SearchOption.AllDirectories))
                File.Copy(newPath, newPath.Replace(SourcePath, DestinationPath), true);
        }
    }
}