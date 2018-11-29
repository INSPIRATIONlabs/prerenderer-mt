using PuppeteerSharp;
using System;

namespace com.inspirationlabs.prerenderer
{
    class RenderPage
    {
        private Page pageTab;
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
