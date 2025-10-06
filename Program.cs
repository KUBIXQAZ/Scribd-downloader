using PuppeteerSharp;
using PuppeteerSharp.Media;

class Program
{
    static async Task Main()
    {
        string pdfPath = "DocumentOutput.pdf";

        Console.Write("Input Scribd URL: ");
        string url = Console.ReadLine() ?? string.Empty;

        Console.WriteLine("Processing...");

        await new BrowserFetcher().DownloadAsync();
        using var browser = await Puppeteer.LaunchAsync(new LaunchOptions { Headless = true });
        using var page = await browser.NewPageAsync();
        await page.GoToAsync(url);

        await page.EvaluateFunctionAsync(@"async () => {
            let total = 0;
            const step = 400;
            while (total < document.body.scrollHeight) {
                window.scrollBy(0, step);
                total += step;
                await new Promise(r => setTimeout(r, 200));
            }
        }");

        await page.EvaluateFunctionAsync(@"
            () => {
                const container = document.getElementById('pdf_document_scroll_container');
                if (!container) return;
                
                document.querySelectorAll('div[role=\""dialog\""], div.osano-cm-window__dialog').forEach(e => e.remove());
                document.querySelectorAll('.between_page_portal_root').forEach(e => e.remove());
                
                document.body.innerHTML = '';
                document.body.appendChild(container.cloneNode(true));
            }
        ");

        string htmlContent = await page.GetContentAsync();

        string pattern = @"@media print\{\._2P_-2J\{opacity:0\}\}";
        htmlContent = System.Text.RegularExpressions.Regex.Replace(htmlContent, pattern, "");

        await page.SetContentAsync(htmlContent);

        await page.PdfAsync(pdfPath, new PdfOptions
        {
            Format = PaperFormat.A4,
            PrintBackground = true,
            DisplayHeaderFooter = false,
            MarginOptions = new MarginOptions { Top = "0", Bottom = "0", Left = "0", Right = "0" },
            PreferCSSPageSize = false
        });

        Console.WriteLine($"PDF saved to {pdfPath}");

        Console.ReadKey();
    }
}