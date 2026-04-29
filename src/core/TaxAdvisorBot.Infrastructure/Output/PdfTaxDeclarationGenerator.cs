using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TaxAdvisorBot.Domain.Enums;
using TaxAdvisorBot.Domain.Models;

namespace TaxAdvisorBot.Infrastructure.Output;

/// <summary>
/// Generates a PDF reproduction of the DPFO (Daň z příjmů fyzických osob) tax declaration form.
/// Structured to match the official Czech Financial Administration form layout.
/// </summary>
public sealed class PdfTaxDeclarationGenerator
{
    public byte[] Generate(TaxReturn tr)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        // Pre-compute tax values
        var section6Base = tr.Section6GrossIncome;

        // Stock income from transactions
        var rsuIncomeCzk = tr.StockTransactions
            .Where(t => t.TransactionType == StockTransactionType.RsuVesting && t.ExchangeRate > 0)
            .Sum(t => t.TotalAcquisitionCost * t.ExchangeRate!.Value);

        var esppIncomeCzk = tr.StockTransactions
            .Where(t => t.TransactionType == StockTransactionType.EsppDiscount && t.ExchangeRate > 0)
            .Sum(t => t.TotalEsppDiscount * t.ExchangeRate!.Value);

        var taxableSales = tr.StockTransactions
            .Where(t => t.TransactionType == StockTransactionType.ShareSale && !t.IsExemptFromTax);
        var section10Income = taxableSales
            .Where(t => t.ExchangeRate > 0)
            .Sum(t => t.TotalSaleProceeds * t.ExchangeRate!.Value);
        var section10Expenses = taxableSales
            .Where(t => t.ExchangeRate > 0)
            .Sum(t => t.TotalAcquisitionCost * t.ExchangeRate!.Value);

        var exemptSalesGain = tr.StockTransactions
            .Where(t => t.TransactionType == StockTransactionType.ShareSale && t.IsExemptFromTax && t.ExchangeRate > 0)
            .Sum(t => t.CapitalGain * t.ExchangeRate!.Value);

        var dividendIncomeCzk = tr.StockTransactions
            .Where(t => t.TransactionType == StockTransactionType.Dividend && t.ExchangeRate > 0)
            .Sum(t => (t.GrossAmount ?? 0) * t.ExchangeRate!.Value);

        var taxWithheldAbroadCzk = tr.StockTransactions
            .Where(t => t.TransactionType == StockTransactionType.TaxWithheld && t.ExchangeRate > 0)
            .Sum(t => Math.Abs(t.GrossAmount ?? 0) * t.ExchangeRate!.Value);

        // Use stored §10 values if no transactions computed
        var s10Inc = section10Income > 0 ? section10Income : tr.Section10Income;
        var s10Exp = section10Expenses > 0 ? section10Expenses : tr.Section10Expenses;
        var s8Inc = dividendIncomeCzk > 0 ? dividendIncomeCzk : tr.Section8Income;

        // Total tax base
        var totalGrossIncome = section6Base + s8Inc + s10Inc + tr.Section7GrossIncome + tr.Section9Income;
        var totalExpenses = s10Exp + tr.Section7Expenses + tr.Section8Expenses + tr.Section9Expenses;
        var taxBase = totalGrossIncome - totalExpenses;

        // Deductions
        var pensionDed = Math.Min(tr.PensionFundContributions, 24_000m);
        var lifeDed = Math.Min(tr.LifeInsuranceContributions, 24_000m);
        var mortgageDed = Math.Min(tr.MortgageInterestPaid, 150_000m);
        var donationMin = Math.Max(taxBase * 0.02m, 1_000m);
        var donationMax = taxBase * 0.15m;
        var donationDed = tr.CharitableDonations >= donationMin
            ? Math.Min(tr.CharitableDonations, donationMax) : 0m;
        var totalDeductions = pensionDed + lifeDed + mortgageDed + donationDed + tr.TradeUnionFees;

        var taxBaseAfterDed = Math.Max(taxBase - totalDeductions, 0m);

        // Tax computation
        const decimal solidarityThreshold = 1_935_552m;
        decimal tax;
        if (taxBaseAfterDed <= solidarityThreshold)
            tax = taxBaseAfterDed * 0.15m;
        else
            tax = solidarityThreshold * 0.15m + (taxBaseAfterDed - solidarityThreshold) * 0.23m;
        tax = Math.Floor(tax);

        // Credits
        var totalCredits = tr.BasicTaxCredit + tr.SpouseTaxCredit + tr.StudentTaxCredit;
        var taxAfterCredits = Math.Max(tax - totalCredits, 0m);
        var taxAfterChild = taxAfterCredits - tr.ChildTaxBenefit;

        // Foreign tax credit (§38f)
        var czDivTax = dividendIncomeCzk * 0.15m;
        var foreignCredit = Math.Min(taxWithheldAbroadCzk, czDivTax);

        var finalTax = taxAfterChild - foreignCredit;
        var alreadyPaid = tr.Section6TaxWithheld + tr.TaxPaidAbroad;
        var toPay = finalTax - alreadyPaid;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(25);
                page.MarginVertical(20);
                page.DefaultTextStyle(x => x.FontSize(8));

                page.Header().Column(col =>
                {
                    col.Item().AlignCenter().Text("PŘIZNÁNÍ").FontSize(14).Bold();
                    col.Item().AlignCenter().Text("k dani z příjmů fyzických osob").FontSize(11).SemiBold();
                    col.Item().AlignCenter().Text($"za zdaňovací období (kalendářní rok) {tr.TaxYear}").FontSize(9);
                    col.Item().Height(5);
                    col.Item().LineHorizontal(2);
                    col.Item().Height(8);
                });

                page.Content().Column(col =>
                {
                    // Section 1: Personal data
                    Section(col, "I. ODDÍL — Údaje o poplatníkovi");
                    PersonalDataGrid(col, tr);

                    // Section 2: Income
                    Section(col, "II. ODDÍL — Dílčí základy daně");

                    // §6
                    SubSection(col, "§6 — Příjmy ze závislé činnosti");
                    FieldRow(col, "Úhrn příjmů od všech zaměstnavatelů", R(section6Base));
                    if (rsuIncomeCzk > 0) FieldRow(col, "  z toho: nepeněžní příjem RSU (přepočteno denním kurzem ČNB)", R(rsuIncomeCzk));
                    if (esppIncomeCzk > 0) FieldRow(col, "  z toho: nepeněžní příjem ESPP sleva", R(esppIncomeCzk));
                    FieldRow(col, "Pojistné na sociální zabezpečení", R(tr.Section6SocialInsurance));
                    FieldRow(col, "Pojistné na zdravotní pojištění", R(tr.Section6HealthInsurance));
                    FieldRow(col, "Zálohy na daň z příjmů", R(tr.Section6TaxWithheld));
                    FieldRow(col, "Dílčí základ daně §6", R(section6Base), highlight: true);

                    // §8
                    if (s8Inc > 0)
                    {
                        SubSection(col, "§8 — Příjmy z kapitálového majetku");
                        FieldRow(col, "Dividendy (hrubá částka v Kč)", R(s8Inc));
                        FieldRow(col, "Daň sražená v zahraničí", R(taxWithheldAbroadCzk));
                        FieldRow(col, "Dílčí základ daně §8", R(s8Inc), highlight: true);
                    }

                    // §10
                    if (s10Inc > 0 || exemptSalesGain > 0)
                    {
                        SubSection(col, "§10 — Ostatní příjmy");
                        FieldRow(col, "Příjmy z prodeje cenných papírů (zdanitelné)", R(s10Inc));
                        FieldRow(col, "Výdaje (nabývací cena)", R(s10Exp));
                        FieldRow(col, "Dílčí základ daně §10", R(Math.Max(s10Inc - s10Exp, 0)), highlight: true);
                        if (exemptSalesGain > 0)
                            FieldRow(col, "Osvobozené příjmy (držení > 3 roky, §4)", R(exemptSalesGain));
                    }

                    // Section 3: Tax base
                    Section(col, "III. ODDÍL — Základ daně a daň");
                    FieldRow(col, "Základ daně (součet dílčích základů)", R(taxBase), highlight: true);

                    // §15 deductions
                    SubSection(col, "§15 — Nezdanitelná část základu daně");
                    if (pensionDed > 0) FieldRow(col, "Penzijní spoření (max 24 000 Kč)", R(pensionDed));
                    if (lifeDed > 0) FieldRow(col, "Životní pojištění (max 24 000 Kč)", R(lifeDed));
                    if (mortgageDed > 0) FieldRow(col, "Úroky z úvěru na bydlení (max 150 000 Kč)", R(mortgageDed));
                    if (donationDed > 0) FieldRow(col, "Dary (2–15 % základu daně)", R(donationDed));
                    FieldRow(col, "Celkem nezdanitelné části", R(totalDeductions), highlight: true);

                    FieldRow(col, "Základ daně po odečtení nezdanitelných částí", R(taxBaseAfterDed), highlight: true);

                    // Tax computation
                    SubSection(col, "Výpočet daně");
                    if (taxBaseAfterDed > solidarityThreshold)
                    {
                        FieldRow(col, $"Daň 15 % z {R(solidarityThreshold)}", R(solidarityThreshold * 0.15m));
                        FieldRow(col, $"Daň 23 % z {R(taxBaseAfterDed - solidarityThreshold)} (solidární zvýšení)", R((taxBaseAfterDed - solidarityThreshold) * 0.23m));
                    }
                    else
                    {
                        FieldRow(col, "Daň 15 %", R(taxBaseAfterDed * 0.15m));
                    }
                    FieldRow(col, "Daň (zaokrouhlena na celé Kč dolů)", R(tax), highlight: true);

                    // §35ba credits
                    SubSection(col, "§35ba — Slevy na dani");
                    FieldRow(col, "Sleva na poplatníka", R(tr.BasicTaxCredit));
                    if (tr.SpouseTaxCredit > 0) FieldRow(col, "Sleva na manžela/manželku", R(tr.SpouseTaxCredit));
                    if (tr.StudentTaxCredit > 0) FieldRow(col, "Sleva na studenta", R(tr.StudentTaxCredit));
                    FieldRow(col, "Daň po uplatnění slev", R(taxAfterCredits), highlight: true);

                    // §35c child benefit
                    if (tr.ChildTaxBenefit > 0 || tr.DependentChildrenCount > 0)
                    {
                        SubSection(col, "§35c — Daňové zvýhodnění na děti");
                        FieldRow(col, $"Počet vyživovaných dětí: {tr.DependentChildrenCount}", R(tr.ChildTaxBenefit));
                        FieldRow(col, "Daň po daňovém zvýhodnění", R(taxAfterChild), highlight: true);
                        if (taxAfterChild < 0)
                            FieldRow(col, "DAŇOVÝ BONUS (přeplatek k vrácení)", R(Math.Abs(taxAfterChild)), highlight: true);
                    }

                    // §38f foreign tax credit
                    if (foreignCredit > 0)
                    {
                        SubSection(col, "§38f — Zápočet daně zaplacené v zahraničí");
                        FieldRow(col, "Daň zaplacená v zahraničí", R(taxWithheldAbroadCzk));
                        FieldRow(col, "Maximální zápočet (15 % z dividend)", R(czDivTax));
                        FieldRow(col, "Uplatněný zápočet", R(foreignCredit));
                    }

                    // Final
                    Section(col, "IV. ODDÍL — Výsledná daňová povinnost");
                    FieldRow(col, "Daň celkem", R(finalTax), highlight: true);
                    FieldRow(col, "Úhrn sražených záloh zaměstnavatelem (§6)", R(tr.Section6TaxWithheld));
                    if (tr.TaxPaidAbroad > 0) FieldRow(col, "Daň zaplacená v zahraničí", R(tr.TaxPaidAbroad));
                    FieldRow(col, "Celkem zaplaceno", R(alreadyPaid));

                    col.Item().Height(8);
                    col.Item().LineHorizontal(2);
                    col.Item().Height(5);

                    if (toPay > 0)
                    {
                        col.Item().Background("#FFF3E0").Padding(8).Row(row =>
                        {
                            row.RelativeItem().Text("NEDOPLATEK — doplatit finančnímu úřadu:").SemiBold().FontSize(10);
                            row.ConstantItem(120).AlignRight().Text(R(toPay)).Bold().FontSize(10);
                            row.ConstantItem(30).AlignRight().Text("Kč").FontSize(10);
                        });
                    }
                    else if (toPay < 0)
                    {
                        col.Item().Background("#E8F5E9").Padding(8).Row(row =>
                        {
                            row.RelativeItem().Text("PŘEPLATEK — k vrácení:").SemiBold().FontSize(10);
                            row.ConstantItem(120).AlignRight().Text(R(Math.Abs(toPay))).Bold().FontSize(10);
                            row.ConstantItem(30).AlignRight().Text("Kč").FontSize(10);
                        });
                    }
                    else
                    {
                        col.Item().Padding(8).Text("Výsledná daňová povinnost: 0 Kč (vyrovnáno)").SemiBold().FontSize(10);
                    }

                    // Signature
                    col.Item().Height(20);
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Datum:").FontSize(8);
                            c.Item().Height(20);
                            c.Item().LineHorizontal(0.5f);
                        });
                        row.ConstantItem(30);
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Podpis poplatníka:").FontSize(8);
                            c.Item().Height(20);
                            c.Item().LineHorizontal(0.5f);
                        });
                    });
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

    private static void Section(ColumnDescriptor col, string title)
    {
        col.Item().Height(10);
        col.Item().Background("#E3F2FD").Padding(5).Text(title).Bold().FontSize(9);
        col.Item().Height(4);
    }

    private static void SubSection(ColumnDescriptor col, string title)
    {
        col.Item().Height(6);
        col.Item().Text(title).SemiBold().FontSize(8);
        col.Item().Height(2);
    }

    private static void FieldRow(ColumnDescriptor col, string label, string value, bool highlight = false)
    {
        col.Item().Row(row =>
        {
            if (highlight)
            {
                row.RelativeItem().Background("#F5F5F5").Padding(2).Text(label).SemiBold().FontSize(8);
                row.ConstantItem(120).Background("#F5F5F5").Padding(2).AlignRight().Text(value).SemiBold().FontSize(8);
                row.ConstantItem(30).Background("#F5F5F5").Padding(2).AlignRight().Text("Kč").FontSize(8);
            }
            else
            {
                row.RelativeItem().Padding(2).Text(label).FontSize(8);
                row.ConstantItem(120).Padding(2).AlignRight().Text(value).FontSize(8);
                row.ConstantItem(30).Padding(2).AlignRight().Text("Kč").FontSize(8);
            }
        });
    }

    private static void PersonalDataGrid(ColumnDescriptor col, TaxReturn tr)
    {
        col.Item().Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(1);
                c.RelativeColumn(2);
                c.RelativeColumn(1);
                c.RelativeColumn(2);
            });

            table.Header(header =>
            {
                header.Cell().Border(0.5f).Padding(3).Text("Jméno:").FontSize(8);
                header.Cell().Border(0.5f).Padding(3).Text(tr.FirstName ?? "—").FontSize(8);
                header.Cell().Border(0.5f).Padding(3).Text("Příjmení:").FontSize(8);
                header.Cell().Border(0.5f).Padding(3).Text(tr.LastName ?? "—").FontSize(8);
            });

            table.Cell().Border(0.5f).Padding(3).Text("Rodné číslo:").FontSize(8);
            table.Cell().Border(0.5f).Padding(3).Text(tr.PersonalIdNumber ?? "—").FontSize(8);
            table.Cell().Border(0.5f).Padding(3).Text("Datum narození:").FontSize(8);
            table.Cell().Border(0.5f).Padding(3).Text(tr.DateOfBirth?.ToString("dd.MM.yyyy") ?? "—").FontSize(8);

            table.Cell().Border(0.5f).Padding(3).Text("Finanční úřad:").FontSize(8);
            table.Cell().Border(0.5f).Padding(3).Text(tr.TaxOfficeCode ?? "—").FontSize(8);
            table.Cell().Border(0.5f).Padding(3).Text("Stav:").FontSize(8);
            table.Cell().Border(0.5f).Padding(3).Text(tr.Status.ToString()).FontSize(8);
        });
    }

    /// <summary>Format decimal as Czech number string.</summary>
    private static string R(decimal value) => value.ToString("N0");
}
