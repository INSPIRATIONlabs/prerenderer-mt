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
using Microsoft.AspNetCore.Hosting;
using System.Xml.Linq;

namespace com.inspirationlabs.prerenderer
{
    class UrlElement
    {
        public string url { get; set; }
        public bool published { get; set; }
        public bool indexed { get; set; }
        public bool followed { get; set; }
    }
    /// <summary>
    /// Provides a CLI interface for prendering pages
    /// </summary>
    class Prerenderer
    {
        /// <summary>
        /// Subclass to provide the CLI options for the application
        /// </summary>
        public class Options
        {
            [Option('t', "threads",
               HelpText = "Thread count")]
            public int? Threads { get; set; }

            [Option('u', "urls",
               Required = true,
               HelpText = "http url to the list of urls in json format")]
            public string Urls { get; set; }

            [Option('c', "chromepath",
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

            [Option('h', "host",
               HelpText = "The host with the source project")]
            public string Host { get; set; }

            [Option('i', "injectFile",
                HelpText = "Path to a JS file to inject")]
            public string InjectPath { get; set; }

            [Option('r', "retry",
               HelpText = "Times to retry the Rendering",
                Default = 3)]
            public int Retries { get; set; }

            [Option('b', "basePath",
                Default = "",
                HelpText = "basePath for the rendering (only needed if it is not /)")]
            public string BasePath { get; set; }

            [Option('m', "siteMap",
                Default = true,
                HelpText = "Generate sitemap.xml")]
            public bool SiteMap { get; set; }
        }

        /// <summary>
        /// Provides all options which have been set by the CLI interface
        /// </summary>
        static Options CliOptions;
        /// <summary>
        /// The amount of threads for this process defaulting to the current count of available processors
        /// </summary>
        static int Threads = Environment.ProcessorCount;
        /// <summary>
        /// The source host where we fetch the data from
        /// </summary>
        static string Host = "http://localhost:5000";
        /// <summary>
        /// The whole list of urls as json
        /// </summary>
        /// <code>
        /// data: [
        ///   {
        ///     url: "/en/vivicity",
        ///     published: true,
        ///     indexed: true,
        ///     followed: true
        ///   }
        /// ]
        /// </code>
        static string UrlListUrl;
        /// <summary>
        /// The output path of the rendered contents
        /// </summary>
        static string OutputPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + Path.DirectorySeparatorChar + "output";
        /// <summary>
        /// The source path which provides the basic index.html and the javascript
        /// </summary>
        static string SourcePath;
        /// <summary>
        /// Path to a installed chromium version
        /// </summary>
        static string ChromiumPath;
        /// <summary>
        /// Path to a JS file which should be injected
        /// </summary>
        static string InjectFile;
        /// <summary>
        /// Reference to the created browser instance
        /// </summary>
        static Browser browser;
        /// <summary>
        /// Informations about the current working directory
        /// </summary>
        static DirectoryInfo Cwd;
        /// <summary>
        /// Main application run function
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            // parse the commandline Arguments and checks if everything is valid
            CommandLine.Parser.Default.ParseArguments<Options>(args).WithParsed<Options>(o =>
            {
                CliOptions = o;
                // checks if we got a commandline parameter to manipulate the threadcount
                if(CliOptions.Threads != null)
                {
                    Threads = CliOptions.Threads.GetValueOrDefault();
                }
                Console.WriteLine("Used Threads: " + Threads);
                // checks if the output path has been set by the commandline parameter and sets the static variable
                if (CliOptions.OutputPath != null)
                {
                    // check if the path is relative                  
                    OutputPath = Path.GetFullPath(CliOptions.OutputPath);
                }
                // sets the source path to the provided sourcepath by the commandline paramater
                SourcePath = CliOptions.SourcePath;
                // sets the url list to the parameter provided by the commandline parameter
                UrlListUrl = CliOptions.Urls;
                // sets the path to a existing chromium version in the operating system
                // if not set the prerenderer tries to download a chrome version
                if(CliOptions.ChromePath != null)
                {
                    ChromiumPath = CliOptions.ChromePath;
                }
                // sets the url to the host we try to render from
                if (CliOptions.Host != null)
                {
                    Host = CliOptions.Host;
                }
                if (CliOptions.InjectPath != null)
                {
                    InjectFile = CliOptions.InjectPath;
                }
                HttpServerStartup.basePath = CliOptions.BasePath;
                // initializes a http server on the sourcepath and defaults to port 5000 and 5001 if the needed certs are installed
                IWebHost webHost = new WebHostBuilder()
                .UseWebRoot(SourcePath)
                .UseKestrel()
                .UseStartup<HttpServerStartup>()
                .Build();

                // start webserver async
                webHost.RunAsync();
                RunApp();
                webHost.Dispose();
            }).WithNotParsed<Options>( o =>
            {
                Console.WriteLine("Missing options");
                Console.WriteLine("Press any key to quit");
                Console.ReadKey();
            });
        }

        static void RunApp()
        {
            try
            {
                string op = OutputPath;
                if(CliOptions.BasePath.Length > 0)
                {
                    op = OutputPath + CliOptions.BasePath.Replace('/', Path.DirectorySeparatorChar);
                }
                // delete outputpath if it exists
                if (op.Length > 0 && Directory.Exists(op))
                {
                    Directory.Delete(op, true);
                }
                if (op.Length > 0)
                {
                    Console.WriteLine("Creating outputpath " + op);
                    Cwd = Directory.CreateDirectory(op);
                }
                // copy assets etc from source if they exist
                if (Directory.Exists(SourcePath)
                )
                {
                    List<string> dirs = new List<string>() { "assets", "build", "contents" };
                    dirs.ForEach((name) =>
                    {
                        if (Directory.Exists(SourcePath + Path.DirectorySeparatorChar + name))
                        {
                            CopyDirectory(
                                SourcePath + Path.DirectorySeparatorChar + name,
                                op + Path.DirectorySeparatorChar + name
                            );
                        }
                    });
                    if (File.Exists(SourcePath + Path.DirectorySeparatorChar + "robots.txt"))
                    {
                        File.Copy(
                            SourcePath + Path.DirectorySeparatorChar + "robots.txt",
                            op + Path.DirectorySeparatorChar + "robots.txt"
                        );
                    }
                    if (File.Exists(SourcePath + Path.DirectorySeparatorChar + "manifest.json"))
                    {
                        File.Copy(
                            SourcePath + Path.DirectorySeparatorChar + "manifest.json",
                            op + Path.DirectorySeparatorChar + "manifest.json"
                        );
                    }
                }
                else
                {
                    throw new Exception("Source directory does not exist");
                }
                // wait for MainTask (async)
                Maintask().Wait();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
        
        static async Task Maintask()
        {
            try
            {
                // create a new httpClient
                HttpClient client = new HttpClient();
                // get the list of urls from the url set by the cli options
                HttpResponseMessage response = await client.GetAsync(CliOptions.Urls);
                response.EnsureSuccessStatusCode();
                // get the content from the response
                string responseBody = await response.Content.ReadAsStringAsync();
                // parse the response to a JObject
                JObject jObject = JObject.Parse(responseBody);
                // get the array from the json object which provides the url
                JArray urldata = (JArray)jObject["data"];
                List<UrlElement> urllist = urldata.ToObject<List<UrlElement>>();
                List<UrlElement> filteredList = urllist.Where(x => x.published == true).ToList<UrlElement>();
                //testing
                //while (urldata.Count >= 300)
                //{
                //    urldata.Remove(urldata.Last);
                //}

                // Start the async download function
                await DownloadAsync(filteredList);
                // generate sitemap.xml
                if (CliOptions.SiteMap == true)
                {
                    string op = OutputPath;
                    if (CliOptions.BasePath.Length > 0)
                    {
                        op = OutputPath + CliOptions.BasePath.Replace('/', Path.DirectorySeparatorChar);
                    }
                    GenerateSiteMap(filteredList, op);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        /// <summary>
        /// Prerender everything in the provided url list
        /// </summary>
        /// <param name="urls"></param>
        /// <returns></returns>
        static async Task DownloadAsync(List<UrlElement> urls)
        {
            Processing processing = new Processing();
            Queue<RenderPage> qt = new Queue<RenderPage>();
            try
            {
                int cnt = 0;
                using (SemaphoreSlim semaphore = new SemaphoreSlim(Threads))
                using (browser = await RenderPage.StartBrowser(ChromiumPath))
                {
                    String scriptBody = "";
                    if(InjectFile != null) {
                        scriptBody = await File.ReadAllTextAsync(InjectFile);
                    }

                    var tasks = urls.Select(async (urldata) =>
                    {
                        await semaphore.WaitAsync();
                        if (qt.Count < 1)
                        {
                            Console.WriteLine("recreate page");
                            RenderPage rPage = new RenderPage(browser, OutputPath, Host, CliOptions.BasePath);
                            rPage.maxRetry = CliOptions.Retries;
                            rPage.scriptBody = scriptBody;
                            await rPage.StartPage();
                            qt.Enqueue(rPage);
                        }

                        RenderPage rendPage = qt.Dequeue();
                        try
                        {
                            string path = urldata.url;
                            await rendPage.SetPage(path);
                            cnt++;

                            // put the result on the processing pipeline
                            processing.QueueItemAsync(rendPage, qt);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.Message + " (" + CliOptions.BasePath + urldata.url + ")");
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    });
                    await Task.WhenAll(tasks.ToArray());
                    await processing.WaitForCompleteAsync();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            finally
            {
                if(browser != null)
                {
                    await browser.CloseAsync();
                }
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

        /// <summary>
        /// Generates a xml sitemap
        /// </summary>
        /// <param name="urllist"></param>
        /// <param name="outputpath"></param>
        static void GenerateSiteMap(List<UrlElement> urllist, string outputpath )
        {
            XDocument doc = new XDocument();
            XNamespace sitemapNs = "http://www.sitemaps.org/schemas/sitemap/0.9";
            XElement urlListEl = new XElement(sitemapNs + "urlset");
            urlListEl.Add(urllist.Select((x => new XElement(sitemapNs + "url", new XElement(sitemapNs + "loc", x.url)))));
            doc.Add(urlListEl);
            doc.Save(outputpath + "/sitemap.xml");
        }
    }
}