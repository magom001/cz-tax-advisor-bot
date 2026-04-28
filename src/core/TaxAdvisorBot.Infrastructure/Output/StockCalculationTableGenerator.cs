using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TaxAdvisorBot.Domain.Enums;
using TaxAdvisorBot.Domain.Models;

namespace TaxAdvisorBot.Infrastructure.Output;

/// <summary>
/// Generates a Czech-language PDF calculation table showing stock compensation
/// transactions with daily and uniform ČNB exchange rates.
/// Supporting document for the tax office.
/// </summary>
public sealed class StockCalculationTableGenerator
{
    public byte[] Generate(TaxReturn taxReturn)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var vested = taxReturn.StockTransactions
            .Where(t => t.TransactionType == StockTransactionType.RsuVesting)
            .OrderBy(t => t.AcquisitionDate).ToList();

        var espp = taxReturn.StockTransactions
            .Where(t => t.TransactionType == StockTransactionType.EsppDiscount)
            .OrderBy(t => t.AcquisitionDate).ToList();

        var sales = taxReturn.StockTransactions
            .Where(t => t.TransactionType == StockTransactionType.ShareSale)
            .OrderBy(t => t.SaleDate ?? t.AcquisitionDate).ToList();

        var dividends = taxReturn.StockTransactions
            .Where(t => t.TransactionType == StockTransactionType.Dividend)
            .OrderBy(t => t.AcquisitionDate).ToList();

        var taxWithheld = taxReturn.StockTransactions
            .Where(t => t.TransactionType == StockTransactionType.TaxWithheld)
            .OrderBy(t => t.AcquisitionDate).ToList();

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.MarginHorizontal(30);
                page.MarginVertical(25);
                page.DefaultTextStyle(x => x.FontSize(8));

                page.Header().Column(col =>
                {
                    col.Item().Text($"Výpočet nepeněžního příjmu ze zahraničních akcií — rok {taxReturn.TaxYear}")
                        .FontSize(11).SemiBold();

                    col.Item().Height(5);

                    col.Item().Row(row =>
                    {
                        row.ConstantItem(80).Text("Poplatník:").SemiBold();
                        row.RelativeItem().Text($"{taxReturn.FirstName} {taxReturn.LastName}");
                        row.ConstantItem(80).Text("Rodné číslo:").SemiBold();
                        row.RelativeItem().Text(taxReturn.PersonalIdNumber ?? "—");
                    });

                    col.Item().Height(10);
                });

                page.Content().Column(col =>
                {
                    // Vested stock (RSU)
                    if (vested.Count > 0)
                    {
                        col.Item().Text("Přidělené akcie (RSU vesting) — §6").SemiBold().FontSize(9);
                        col.Item().Height(3);
                        col.Item().Element(c => BuildTransactionTable(c, vested));

                        var totalUsd = vested.Sum(t => t.TotalAcquisitionCost);
                        var totalCzkDaily = vested.Where(t => t.ExchangeRate > 0)
                            .Sum(t => t.TotalAcquisitionCost * t.ExchangeRate!.Value);

                        col.Item().Height(3);
                        col.Item().Text($"Celkem RSU: ${totalUsd:N2} / {totalCzkDaily:N2} Kč (denní kurz)")
                            .FontSize(8).SemiBold();
                        col.Item().Height(10);
                    }

                    // ESPP
                    if (espp.Count > 0)
                    {
                        col.Item().Text("ESPP sleva — §6").SemiBold().FontSize(9);
                        col.Item().Height(3);
                        col.Item().Element(c => BuildEsppTable(c, espp));

                        var totalDiscount = espp.Sum(t => t.TotalEsppDiscount);
                        col.Item().Height(3);
                        col.Item().Text($"Celkem ESPP sleva: ${totalDiscount:N2}")
                            .FontSize(8).SemiBold();
                        col.Item().Height(10);
                    }

                    // Share sales
                    if (sales.Count > 0)
                    {
                        col.Item().Text("Prodej akcií — §10").SemiBold().FontSize(9);
                        col.Item().Height(3);
                        col.Item().Element(c => BuildSalesTable(c, sales));

                        var taxable = sales.Where(s => !s.IsExemptFromTax).Sum(s => s.CapitalGain);
                        var exempt = sales.Where(s => s.IsExemptFromTax).Sum(s => s.CapitalGain);
                        col.Item().Height(3);
                        col.Item().Text($"Zdanitelný zisk: ${taxable:N2} | Osvobozeno (>3 roky): ${exempt:N2}")
                            .FontSize(8).SemiBold();
                        col.Item().Height(10);
                    }

                    // Dividends
                    if (dividends.Count > 0)
                    {
                        col.Item().Text("Dividendy — §8 (zdaněno v USA)").SemiBold().FontSize(9);
                        col.Item().Height(3);
                        col.Item().Element(c => BuildDividendTable(c, dividends));

                        var totalDivUsd = dividends.Sum(t => t.GrossAmount ?? 0);
                        var totalDivCzk = dividends.Where(t => t.ExchangeRate > 0)
                            .Sum(t => (t.GrossAmount ?? 0) * t.ExchangeRate!.Value);

                        col.Item().Height(3);
                        col.Item().Text($"Celkem dividendy: ${totalDivUsd:N2} / {totalDivCzk:N2} Kč")
                            .FontSize(8).SemiBold();
                        col.Item().Height(10);
                    }

                    // Tax withheld abroad
                    if (taxWithheld.Count > 0)
                    {
                        col.Item().Text("Sražená daň v zahraničí").SemiBold().FontSize(9);
                        col.Item().Height(3);
                        col.Item().Element(c => BuildDividendTable(c, taxWithheld));

                        var totalTaxUsd = taxWithheld.Sum(t => Math.Abs(t.GrossAmount ?? 0));
                        var totalTaxCzk = taxWithheld.Where(t => t.ExchangeRate > 0)
                            .Sum(t => Math.Abs(t.GrossAmount ?? 0) * t.ExchangeRate!.Value);

                        col.Item().Height(3);
                        col.Item().Text($"Celkem sražená daň: ${totalTaxUsd:N2} / {totalTaxCzk:N2} Kč")
                            .FontSize(8).SemiBold();
                        col.Item().Height(10);
                    }

                    // Summary
                    col.Item().Height(10);
                    col.Item().LineHorizontal(1);
                    col.Item().Height(5);
                    col.Item().Text("Souhrn").SemiBold().FontSize(10);
                    col.Item().Height(5);

                    var summaryItems = new List<(string Label, string Value)>
                    {
                        ("§6 příjem z RSU", $"{vested.Sum(t => t.TotalAcquisitionCost):N2} USD"),
                        ("§6 příjem z ESPP", $"{espp.Sum(t => t.TotalEsppDiscount):N2} USD"),
                        ("§8 dividendy (zdaněno v USA)", $"{dividends.Sum(t => t.GrossAmount ?? 0):N2} USD"),
                        ("Sražená daň v zahraničí", $"{taxWithheld.Sum(t => Math.Abs(t.GrossAmount ?? 0)):N2} USD"),
                        ("§10 příjem z prodeje (zdanitelný)", $"{sales.Where(s => !s.IsExemptFromTax).Sum(s => s.CapitalGain):N2} USD"),
                        ("§10 příjem z prodeje (osvobozený)", $"{sales.Where(s => s.IsExemptFromTax).Sum(s => s.CapitalGain):N2} USD"),
                        ("Daňový rok", taxReturn.TaxYear.ToString()),
                    };

                    foreach (var (label, value) in summaryItems)
                    {
                        col.Item().Row(row =>
                        {
                            row.ConstantItem(250).Text(label);
                            row.RelativeItem().Text(value).SemiBold();
                        });
                    }
                });

                page.Footer().AlignRight().Text(text =>
                {
                    text.Span("Strana ");
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
            });
        }).GeneratePdf();
    }

    private static void BuildTransactionTable(IContainer container, List<StockTransaction> transactions)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(70);  // Date
                c.ConstantColumn(50);  // Ticker
                c.ConstantColumn(45);  // Qty
                c.ConstantColumn(65);  // Price/share
                c.ConstantColumn(75);  // Total USD
                c.ConstantColumn(50);  // Daily rate
                c.ConstantColumn(80);  // Total CZK daily
                c.ConstantColumn(50);  // Uniform rate
                c.ConstantColumn(80);  // Total CZK uniform
            });

            // Header
            var headers = new[] { "Datum", "Akcie", "Ks", "Cena/ks", "Celkem USD", "Denní kurz", "Celkem Kč", "Jedn. kurz", "Celkem Kč" };
            table.Header(header =>
            {
                foreach (var h in headers)
                    header.Cell().Border(0.5f).Padding(3).Text(h).SemiBold().FontSize(7);
            });

            foreach (var tx in transactions)
            {
                table.Cell().Border(0.5f).Padding(2).Text(tx.AcquisitionDate.ToString("dd.MM.yyyy")).FontSize(7);
                table.Cell().Border(0.5f).Padding(2).Text(tx.Ticker).FontSize(7);
                table.Cell().Border(0.5f).Padding(2).AlignRight().Text($"{tx.Quantity:N0}").FontSize(7);
                table.Cell().Border(0.5f).Padding(2).AlignRight().Text($"${tx.AcquisitionPricePerShare:N2}").FontSize(7);
                table.Cell().Border(0.5f).Padding(2).AlignRight().Text($"${tx.TotalAcquisitionCost:N2}").FontSize(7);
                table.Cell().Border(0.5f).Padding(2).AlignRight().Text(tx.ExchangeRate?.ToString("N3") ?? "—").FontSize(7);
                table.Cell().Border(0.5f).Padding(2).AlignRight().Text(tx.ExchangeRate > 0 ? $"{tx.TotalAcquisitionCost * tx.ExchangeRate.Value:N2}" : "—").FontSize(7);
                table.Cell().Border(0.5f).Padding(2).AlignRight().Text("—").FontSize(7); // Uniform rate — filled later
                table.Cell().Border(0.5f).Padding(2).AlignRight().Text("—").FontSize(7);
            }
        });
    }

    private static void BuildEsppTable(IContainer container, List<StockTransaction> transactions)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(70);  // Date
                c.ConstantColumn(45);  // Qty
                c.ConstantColumn(65);  // Purchase price
                c.ConstantColumn(65);  // FMV
                c.ConstantColumn(50);  // Discount %
                c.ConstantColumn(70);  // Discount USD
                c.ConstantColumn(50);  // Daily rate
                c.ConstantColumn(80);  // Discount CZK
            });

            var headers = new[] { "Datum", "Ks", "Nákup/ks", "FMV/ks", "Sleva %", "Sleva USD", "Kurz", "Sleva Kč" };
            table.Header(header =>
            {
                foreach (var h in headers)
                    header.Cell().Border(0.5f).Padding(3).Text(h).SemiBold().FontSize(7);
            });

            foreach (var tx in transactions)
            {
                var discountPct = tx.EsppPurchasePricePerShare.HasValue && tx.AcquisitionPricePerShare > 0
                    ? (1 - tx.EsppPurchasePricePerShare.Value / tx.AcquisitionPricePerShare) * 100
                    : 0;

                table.Cell().Border(0.5f).Padding(2).Text(tx.AcquisitionDate.ToString("dd.MM.yyyy")).FontSize(7);
                table.Cell().Border(0.5f).Padding(2).AlignRight().Text($"{tx.Quantity:N0}").FontSize(7);
                table.Cell().Border(0.5f).Padding(2).AlignRight().Text($"${tx.EsppPurchasePricePerShare:N2}").FontSize(7);
                table.Cell().Border(0.5f).Padding(2).AlignRight().Text($"${tx.AcquisitionPricePerShare:N2}").FontSize(7);
                table.Cell().Border(0.5f).Padding(2).AlignRight().Text($"{discountPct:N1}%").FontSize(7);
                table.Cell().Border(0.5f).Padding(2).AlignRight().Text($"${tx.TotalEsppDiscount:N2}").FontSize(7);
                table.Cell().Border(0.5f).Padding(2).AlignRight().Text(tx.ExchangeRate?.ToString("N3") ?? "—").FontSize(7);
                table.Cell().Border(0.5f).Padding(2).AlignRight().Text(tx.ExchangeRate > 0 ? $"{tx.TotalEsppDiscount * tx.ExchangeRate.Value:N2}" : "—").FontSize(7);
            }
        });
    }

    private static void BuildSalesTable(IContainer container, List<StockTransaction> sales)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(70);  // Acquisition date
                c.ConstantColumn(70);  // Sale date
                c.ConstantColumn(50);  // Ticker
                c.ConstantColumn(40);  // Qty
                c.ConstantColumn(60);  // Acquisition price
                c.ConstantColumn(60);  // Sale price
                c.ConstantColumn(70);  // Gain/Loss
                c.ConstantColumn(50);  // Held years
                c.ConstantColumn(55);  // Exempt?
            });

            var headers = new[] { "Nabytí", "Prodej", "Akcie", "Ks", "Nákup/ks", "Prodej/ks", "Zisk/Ztráta", "Drženo", "Osvob." };
            table.Header(header =>
            {
                foreach (var h in headers)
                    header.Cell().Border(0.5f).Padding(3).Text(h).SemiBold().FontSize(7);
            });

            foreach (var tx in sales)
            {
                var heldYears = tx.SaleDate.HasValue
                    ? (tx.SaleDate.Value.ToDateTime(TimeOnly.MinValue) - tx.AcquisitionDate.ToDateTime(TimeOnly.MinValue)).Days / 365.25
                    : 0;

                table.Cell().Border(0.5f).Padding(2).Text(tx.AcquisitionDate.ToString("dd.MM.yyyy")).FontSize(7);
                table.Cell().Border(0.5f).Padding(2).Text(tx.SaleDate?.ToString("dd.MM.yyyy") ?? "—").FontSize(7);
                table.Cell().Border(0.5f).Padding(2).Text(tx.Ticker).FontSize(7);
                table.Cell().Border(0.5f).Padding(2).AlignRight().Text($"{tx.Quantity:N0}").FontSize(7);
                table.Cell().Border(0.5f).Padding(2).AlignRight().Text($"${tx.AcquisitionPricePerShare:N2}").FontSize(7);
                table.Cell().Border(0.5f).Padding(2).AlignRight().Text(tx.SalePricePerShare.HasValue ? $"${tx.SalePricePerShare:N2}" : "—").FontSize(7);
                table.Cell().Border(0.5f).Padding(2).AlignRight().Text($"${tx.CapitalGain:N2}").FontSize(7);
                table.Cell().Border(0.5f).Padding(2).AlignRight().Text($"{heldYears:N1} r.").FontSize(7);
                table.Cell().Border(0.5f).Padding(2).AlignCenter().Text(tx.IsExemptFromTax ? "ANO" : "NE").FontSize(7);
            }
        });
    }

    private static void BuildDividendTable(IContainer container, List<StockTransaction> transactions)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(70);  // Date
                c.ConstantColumn(50);  // Ticker
                c.ConstantColumn(80);  // Amount USD
                c.ConstantColumn(50);  // Daily rate
                c.ConstantColumn(80);  // Amount CZK
                c.ConstantColumn(100); // Broker
            });

            var headers = new[] { "Datum", "Akcie", "Částka USD", "Kurz", "Částka Kč", "Broker" };
            table.Header(header =>
            {
                foreach (var h in headers)
                    header.Cell().Border(0.5f).Padding(3).Text(h).SemiBold().FontSize(7);
            });

            foreach (var tx in transactions)
            {
                var amount = tx.GrossAmount ?? 0;
                table.Cell().Border(0.5f).Padding(2).Text(tx.AcquisitionDate.ToString("dd.MM.yyyy")).FontSize(7);
                table.Cell().Border(0.5f).Padding(2).Text(tx.Ticker).FontSize(7);
                table.Cell().Border(0.5f).Padding(2).AlignRight().Text($"${amount:N2}").FontSize(7);
                table.Cell().Border(0.5f).Padding(2).AlignRight().Text(tx.ExchangeRate?.ToString("N3") ?? "—").FontSize(7);
                table.Cell().Border(0.5f).Padding(2).AlignRight().Text(tx.ExchangeRate > 0 ? $"{amount * tx.ExchangeRate.Value:N2}" : "—").FontSize(7);
                table.Cell().Border(0.5f).Padding(2).Text(tx.BrokerName ?? "—").FontSize(7);
            }
        });
    }
}
