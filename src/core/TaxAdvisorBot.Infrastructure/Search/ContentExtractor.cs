using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using HtmlAgilityPack;
using UglyToad.PdfPig;

namespace TaxAdvisorBot.Infrastructure.Search;

/// <summary>
/// Extracts clean text from fetched web content. Picks the right strategy based on content type:
/// HTML → DOM parse + text node extraction, PDF → PdfPig, plain text → pass through.
/// </summary>
public sealed class ContentExtractor
{
    private static readonly HashSet<string> SkipElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "script", "style", "noscript", "svg", "iframe", "object", "embed",
        "nav", "footer", "header", "aside", "form", "button", "input",
        "select", "textarea", "meta", "link", "head"
    };

    private readonly ILogger<ContentExtractor> _logger;

    public ContentExtractor(ILogger<ContentExtractor> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Extracts clean text from HTTP response content based on its content type.
    /// </summary>
    public async Task<string> ExtractAsync(HttpResponseMessage response, CancellationToken ct = default)
    {
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "text/html";

        return contentType switch
        {
            "application/pdf" => await ExtractFromPdfAsync(response, ct),
            "text/plain" => await response.Content.ReadAsStringAsync(ct),
            _ => await ExtractFromHtmlAsync(response, ct),
        };
    }

    private async Task<string> ExtractFromHtmlAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var html = await response.Content.ReadAsStringAsync(ct);

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Try to find the main content area first, fall back to body
        var contentNode = doc.DocumentNode.SelectSingleNode("//main")
            ?? doc.DocumentNode.SelectSingleNode("//article")
            ?? doc.DocumentNode.SelectSingleNode("//div[contains(@class,'content')]")
            ?? doc.DocumentNode.SelectSingleNode("//body")
            ?? doc.DocumentNode;

        var sb = new StringBuilder();
        ExtractTextNodes(contentNode, sb);

        var text = sb.ToString();
        // Collapse excessive whitespace
        text = System.Text.RegularExpressions.Regex.Replace(text, @"[ \t]+", " ");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\n{3,}", "\n\n");
        text = text.Trim();

        _logger.LogDebug("Extracted {Length} chars from HTML via DOM parser", text.Length);
        return text;
    }

    private static void ExtractTextNodes(HtmlNode node, StringBuilder sb)
    {
        if (node.NodeType == HtmlNodeType.Text)
        {
            var text = WebUtility.HtmlDecode(node.InnerText);
            if (!string.IsNullOrWhiteSpace(text))
            {
                sb.Append(text);
            }
            return;
        }

        if (node.NodeType != HtmlNodeType.Element)
            return;

        // Skip non-content elements
        if (SkipElements.Contains(node.Name))
            return;

        // Block-level elements get newlines
        var isBlock = node.Name is "p" or "div" or "br" or "h1" or "h2" or "h3"
            or "h4" or "h5" or "h6" or "li" or "tr" or "dt" or "dd" or "section"
            or "blockquote" or "pre" or "table";

        if (isBlock && sb.Length > 0 && sb[^1] != '\n')
            sb.AppendLine();

        foreach (var child in node.ChildNodes)
        {
            ExtractTextNodes(child, sb);
        }

        if (isBlock)
            sb.AppendLine();
    }

    private async Task<string> ExtractFromPdfAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        using var document = PdfDocument.Open(bytes);

        var sb = new StringBuilder();
        foreach (var page in document.GetPages())
        {
            var pageText = page.Text;
            if (!string.IsNullOrWhiteSpace(pageText))
            {
                sb.AppendLine(pageText);
                sb.AppendLine();
            }
        }

        var text = sb.ToString().Trim();
        _logger.LogDebug("Extracted {Length} chars from {Pages}-page PDF", text.Length, document.NumberOfPages);
        return text;
    }
}
