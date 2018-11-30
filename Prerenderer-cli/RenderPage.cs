using PuppeteerSharp;
using System;
using System.IO;
using System.Threading.Tasks;

namespace com.inspirationlabs.prerenderer
{
    class RenderPage
    {
        public string pageUrl;
        public string scriptBody;
        public int waitForTimeout = 30000;
        public int maxRetry = 3;
        public string selector = "app-root.hydrated";

        private Page pageTab;
        private Browser pBrowser;
        private string pageHostUrl;
        private string baseUrl;
        private string pagePath;
        private string outputDir;
        private int retryCount = 0;
        /// <summary>
        /// Constructor for the class
        /// </summary>
        /// <param name="browser"></param>
        /// <param name="outDir"></param>
        /// <param name="hostUrl"></param>
        /// <param name="basePath"></param>
        public RenderPage(Browser browser, string outDir, string hostUrl, string basePath = "")
        {
            // the browser instance
            pBrowser = browser;
            if(hostUrl.EndsWith("/"))
            {
                hostUrl = hostUrl.Substring(0, hostUrl.Length - 1);
            }
            pageHostUrl = hostUrl;
            // the output directory
            outputDir = outDir;
            if(basePath.Equals("/"))
            {
                basePath = "";
            }
            if (basePath.EndsWith("/"))
            {
                basePath = basePath.Substring(0, basePath.Length - 1);
            }
            baseUrl = basePath;
        }

        /// <summary>
        /// Returns a new browser object
        /// </summary>
        /// <param name="chromePath">Sets the path to a existing chrome or chromium installation</param>
        /// <returns></returns>
        public static async Task<Browser> StartBrowser(string chromePath = null)
        {
            // Download chrome if there hasn't been a chromium provided
            if (chromePath == null)
            {
                Console.WriteLine("Download Chrome binary");
                await new BrowserFetcher().DownloadAsync(BrowserFetcher.DefaultRevision);
            }
            LaunchOptions lopts = new LaunchOptions
            {
                Headless = true,
                Args = new[] { "--no-sandbox", "--disable-setuid-sandbox", "--disable-gpu" },
                DefaultViewport = new ViewPortOptions
                {
                    Width = 456,
                    Height = 789
                }
            };
            if (chromePath != null)
            {
                lopts.ExecutablePath = chromePath;
            }
            Browser curBrow = await Puppeteer.LaunchAsync(lopts);
            return curBrow;
        }

        /// <summary>
        /// Open a new tab in the browser
        /// </summary>
        /// <returns></returns>
        public async Task<Page> StartPage()
        {
            Page p = await pBrowser.NewPageAsync();
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
            pageTab = p;
            return p;
        }

        /// <summary>
        /// Go to the url in the provided browser tab
        /// </summary>
        /// <param name="url"></param>
        /// <param name="page"></param>
        /// <param name="browser"></param>
        /// <returns></returns>
        public async Task SetPage(string path)
        {
            if(pageTab == null || pageTab.IsClosed)
            {
                pageTab = await StartPage();
            }
            if(!path.StartsWith("/"))
            {
                path = "/" + path;
            }
            pagePath = path;
            pageUrl = pageHostUrl + baseUrl + pagePath;
            if (pageTab == null || pageTab.IsClosed)
            {
                pageTab = await StartPage();
            }
            await pageTab.GoToAsync(pageUrl, new NavigationOptions
            {
                WaitUntil = new[]
                {
                     WaitUntilNavigation.DOMContentLoaded
                }
            });
        }

        /// <summary>
        /// Get the content from a page after navigating to it
        /// </summary>
        /// <returns></returns>
        public async Task<string> GetContentAsync()
        {
            await WaitAndEval();
            await InjectScript();
            string content = await pageTab.GetContentAsync();
            return content;
        }

        /// <summary>
        /// Inject a script to the content
        /// </summary>
        /// <returns></returns>
        public async Task InjectScript()
        {
            if (scriptBody.Length > 0 && pageTab != null)
            {
                await pageTab.MainFrame.EvaluateFunctionAsync(@"function(){"
                + scriptBody
                + "}");
            }
        }

        /// <summary>
        /// Wait for a element and try to retry
        /// </summary>
        /// <returns></returns>
        public async Task WaitAndEval()
        {
            try
            {
                await pageTab.WaitForSelectorAsync(selector, new WaitForSelectorOptions
                {
                    Timeout = waitForTimeout
                });
                retryCount = 0;
            }
            catch (Exception e)
            {
                retryCount++;
                if(retryCount <= maxRetry)
                {
                    Console.WriteLine("Retry " + retryCount + " for " + pagePath);
                    await pageTab.CloseAsync();
                    await StartPage();
                    await SetPage(pagePath);
                    await WaitAndEval();
                } else
                {
                    // reset the retryCount and bubble up the exception
                    retryCount = 0;
                    throw e;
                }
            }
        }

        /// <summary>
        /// Return the filePath for a element
        /// </summary>
        /// <returns></returns>
        public string GetOutputPath()
        {
            string combPath = baseUrl + pagePath;
            string fpath = combPath.Replace('/', Path.DirectorySeparatorChar);
            Directory.CreateDirectory(outputDir + fpath);
            string indexPath = Path.DirectorySeparatorChar + "index.html";
            string cpath = outputDir + fpath + indexPath;
            return cpath;
        }
    }
}
