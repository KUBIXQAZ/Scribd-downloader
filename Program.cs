using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using PdfSharp.Pdf;
using PdfSharp.Drawing;

class Program
{
    static async Task Main()
    {
        string url = string.Empty;
        string pdfPath = "ImagesOutput.pdf";

        while (string.IsNullOrEmpty(url))
        {
            Console.WriteLine("Give me scribd.com document url:");
            url = Console.ReadLine() ?? string.Empty;
        }

        Console.Clear();

        Console.WriteLine($"Document url: {url}");
        Console.WriteLine("Fetching pages (give me a few minutes) ...");

        var imageUrls = GetImageUrlsFromPage(url);

        Console.WriteLine("Downloading images ...");

        var imageFiles = await DownloadImagesAsync(imageUrls);

        Console.WriteLine("Creating a pdf file ...");

        CreatePdfFromImages(imageFiles, pdfPath);

        Console.WriteLine("Pdf saved to: " + pdfPath);

        Console.ReadKey();
    }

    static List<string> GetImageUrlsFromPage(string url)
    {
        var imageUrls = new HashSet<string>();

        var service = ChromeDriverService.CreateDefaultService();
        service.HideCommandPromptWindow = true;

        var options = new ChromeOptions();
        options.AddArgument("--headless");

        using var driver = new ChromeDriver(service, options);
        driver.Navigate().GoToUrl(url);

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

        Thread.Sleep(2000);

        var images = driver.FindElements(By.CssSelector("img.absimg"));
        foreach (var img in images)
        {
            var src = img.GetAttribute("src");
            if (!string.IsNullOrEmpty(src))
            {
                if (!src.StartsWith("http"))
                {
                    var baseUri = new Uri(url);
                    src = new Uri(baseUri, src).ToString();
                }
                imageUrls.Add(src);
            }
        }

        driver.Quit();
        return imageUrls.ToList();
    }

    static async Task<List<string>> DownloadImagesAsync(List<string> imageUrls)
    {
        var httpClient = new HttpClient();
        var imageFiles = new List<string>();
        Directory.CreateDirectory("Downloaded");

        foreach (var url in imageUrls)
        {
            try
            {
                byte[] imageData = await httpClient.GetByteArrayAsync(url);
                string filename = Path.Combine("Downloaded", Path.GetFileName(new Uri(url).LocalPath));
                await File.WriteAllBytesAsync(filename, imageData);
                imageFiles.Add(filename);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to download image {url}: {ex.Message}");
            }
        }

        return imageFiles;
    }

    static void CreatePdfFromImages(List<string> imageFiles, string outputPdfPath)
    {
        using var doc = new PdfDocument();

        foreach (var imgPath in imageFiles)
        {
            var page = doc.AddPage();
            using var gfx = XGraphics.FromPdfPage(page);
            using var img = XImage.FromFile(imgPath);

            page.Width = img.PixelWidth * 72 / img.HorizontalResolution;
            page.Height = img.PixelHeight * 72 / img.VerticalResolution;

            gfx.DrawImage(img, 0, 0, page.Width, page.Height);
        }

        doc.Save(outputPdfPath);
    }
}