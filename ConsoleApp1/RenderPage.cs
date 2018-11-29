using PuppeteerSharp;
using System;
using System.Threading.Tasks;

namespace com.inspirationlabs.prerenderer
{
    class RenderPage
    {
        private Page pageTab;
        private Browser browser;
        private string pageUrl;
        private string pageHostUrl;
        int waitForTimeout = 30000;
        int retryCount = 0;
        int maxRetry = 3;
        string selector = "app-root.hydrated";

        public RenderPage(Page page, string url, string host)
        {
            pageTab = page;
            pageUrl = url;
            pageHostUrl = host;
        }

        public static async Task<Page> StartPage(Browser browser)
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

        /// <summary>
        /// Go to the url in the provided browser tab
        /// </summary>
        /// <param name="url"></param>
        /// <param name="page"></param>
        /// <param name="browser"></param>
        /// <returns></returns>
        public async Task SetPage(string url, Page page, Browser browser)
        {
            if (page == null || page.IsClosed)
            {
                page = await StartPage(browser);
            }
            await page.GoToAsync(url, new NavigationOptions
            {
                WaitUntil = new[]
                {
                     WaitUntilNavigation.DOMContentLoaded
                }
            });
        }

        public async void WaitAndEval()
        {
            try
            {
                await pageTab.WaitForSelectorAsync(selector, new WaitForSelectorOptions
                {
                    Timeout = waitForTimeout
                });
            }
            catch (Exception e)
            {
                if(retryCount < maxRetry)
                {
                    WaitAndEval();
                } else
                {
                    throw e;
                }
            }
        }
        
    }
}
