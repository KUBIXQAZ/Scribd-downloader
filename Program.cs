using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using WebDriverManager;
using WebDriverManager.DriverConfigs.Impl;

class Program
{
    static async Task Main()
    {
        string url = string.Empty;
        string pdfPath = "DocumentOutput.pdf";

        while (string.IsNullOrEmpty(url))
        {
            Console.WriteLine("Input scribd url:");
            url = Console.ReadLine() ?? string.Empty;
        }

        Console.Clear();

        Console.WriteLine($"Document url: {url}");
        Console.WriteLine("Fetching (give me a few minutes) ...");

        var html = ExtractPagesFromContainers(url);

        Console.WriteLine("Creating PDF file...");

        await GeneratePdfFromHtml(html, pdfPath);

        Console.WriteLine("PDF saved to: " + pdfPath);

        Console.ReadKey();
    }

    public static async Task GeneratePdfFromHtml(string htmlContent, string outputPath)
    {
        await new BrowserFetcher().DownloadAsync();
        using var browser = await Puppeteer.LaunchAsync(new LaunchOptions { Headless = true });
        using var page = await browser.NewPageAsync();
        await page.SetContentAsync(htmlContent);

        var bodyHeight = await page.EvaluateExpressionAsync<double>(
            "document.documentElement.scrollHeight"
        );

        var pdfOptions = new PdfOptions
        {
            PrintBackground = true,
            DisplayHeaderFooter = false,
            MarginOptions = new MarginOptions
            {
                Top = "0",
                Right = "0",
                Bottom = "0",
                Left = "0"
            },
            Height = $"{bodyHeight}px",
            PreferCSSPageSize = false,
            Scale = 1
        };

        await page.PdfAsync(outputPath, pdfOptions);
    }

    static string ExtractPagesFromContainers(string url)
    {
        new DriverManager().SetUpDriver(new ChromeConfig());

        var service = ChromeDriverService.CreateDefaultService();
        service.HideCommandPromptWindow = true;

        var options = new ChromeOptions();
        options.AddArgument("--headless");
        options.AddArgument("--window-size=1200,1600");
        options.AddArgument("--hide-scrollbars");

        using var driver = new ChromeDriver(service, options);
        driver.Navigate().GoToUrl(url);

        Console.WriteLine("Loading all pages...");
        ScrollToBottom(driver);
        Thread.Sleep(3000);

        Console.WriteLine($"Processing...");
        var pageContainers = driver.FindElements(By.CssSelector(".outer_page_container"));
        string pageHtml = ExtractPageContainerWithStyles(driver, pageContainers[0]);
        string htmlFilePath = CreatePageHtml(pageHtml);

        driver.Quit();
        return htmlFilePath;
    }

    static string ExtractPageContainerWithStyles(ChromeDriver driver, IWebElement container)
    {
        ((IJavaScriptExecutor)driver).ExecuteScript(@"
            const container = arguments[0];
            container.querySelectorAll('.between_page_portal_root').forEach(el => el.remove());
        ", container);

        return (string)driver.ExecuteScript(@"
            function extractPageWithStyles(container) {
                const clone = container.cloneNode(true);
                
                function copyAllStyles(source, target) {
                    const computed = getComputedStyle(source);
                    for (let i = 0; i < computed.length; i++) {
                        const prop = computed[i];
                        target.style[prop] = computed.getPropertyValue(prop);
                    }
                }
                
                copyAllStyles(container, clone);
                
                const allSourceElements = container.getElementsByTagName('*');
                const allCloneElements = clone.getElementsByTagName('*');
                
                for (let i = 0; i < allSourceElements.length; i++) {
                    copyAllStyles(allSourceElements[i], allCloneElements[i]);
                }

                return clone.outerHTML;
            }
            
            return extractPageWithStyles(arguments[0]);
        ", container);
    }

    static string CreatePageHtml(string pageContent)
    {
        string html = $@"
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset='UTF-8'>
                <meta name='viewport' content='width=device-width, initial-scale=1.0'>
            </head>
            <body>
                {pageContent}
            </body>
            </html>";

        return html;
    }

    static void ScrollToBottom(ChromeDriver driver)
    {
        int scrollStep = 250;
        int waitPerScroll = 300;
        int total = 0;

        while (true)
        {
            driver.ExecuteScript($"window.scrollBy(0, {scrollStep});");
            total += scrollStep;
            Thread.Sleep(waitPerScroll);

            long scrollHeight = Convert.ToInt64(driver.ExecuteScript("return document.documentElement.scrollHeight"));

            if (total >= scrollHeight) break;
        }
    }
}