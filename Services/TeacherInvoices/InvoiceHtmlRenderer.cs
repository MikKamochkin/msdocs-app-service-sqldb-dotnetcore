using System.Globalization;
using System.Net;
using System.Text;
using DotNetCoreSqlDb.Models;
using Microsoft.Extensions.Options;

namespace DotNetCoreSqlDb.Services.TeacherInvoices;

public sealed class InvoiceHtmlRenderer(IOptions<TeacherInvoiceOptions> options)
{
    private static string E(string? s) => WebUtility.HtmlEncode(s ?? "");
    private static string Money(decimal d) => d.ToString("N2", CultureInfo.InvariantCulture);
    private static string Date(DateTime d) => d.ToString("MMMM d, yyyy", CultureInfo.GetCultureInfo("en-CA"));
    public string Render(TeacherInvoiceSnapshot invoice)
    {
        var i = invoice.Input;
        var o = options.Value;
        var rows = new StringBuilder();
        for (int n = 0; n < invoice.Lessons.Count; n++)
        {
            var l = invoice.Lessons[n];
            var assignment =
                $"French lesson with student {l.GroupName} - {l.DurationMinutes} mins";

            rows.Append(
                $"<tr>" +
                $"<td>{n + 1}</td>" +
                $"<td>{E(l.TorontoDateTime.ToString("MMM d, yyyy HH:mm", CultureInfo.InvariantCulture))}</td>" +
                $"<td>{E(assignment)}</td>" +
                $"<td class='money'>{Money(l.Amount)} {E(invoice.Currency)}</td>" +
                $"</tr>");
        }
        /*if (i.HasCorrection)
        {
            var correctionDate = i.CorrectionDate.HasValue
                ? E(Date(i.CorrectionDate.Value))
                : "";

            var correctionAmount = i.CorrectionAmount.HasValue
                ? Money(i.CorrectionAmount.Value)
                : "";

            rows.Append(
                $"<tr class='invoice-text-row'>" +
                $"<td>{invoice.Lessons.Count + 1}</td>" +
                $"<td>{correctionDate}</td>" +
                $"<td><strong>Correction description</strong>" +
                $"<div class='invoice-note-text'>{E(i.CorrectionDescription)}</div></td>" +
                $"<td class='money'>{correctionAmount} {E(invoice.Currency)}</td>" +
                $"</tr>");
        }*/

        var generalNotes = string.IsNullOrWhiteSpace(i.Note)
            ? ""
            : $"<div class='general-notes'><strong>Comments</strong><div class='invoice-note-text'>{E(i.Note)}</div></div>";

        return $$"""
        <!DOCTYPE html><html><head><meta charset="utf-8"><style>
        @page { size: Letter; margin: 18mm; }
        body { font: 11px Arial, sans-serif; color: #344054; }
        h1,h2,h3,strong { color: #183a60; } h1 { font-size: 32px; margin: 0; }
        h2 { font-size: 22px; margin: 0 0 4px; } h3 { margin-top: 22px; font-size: 12px; }
        .header,.meta,.addresses { width: 100%; border-collapse: collapse; }
        .header td { width: 50%; vertical-align: top; }.header td:last-child { text-align:right; }
        .header h1 { position: relative; top: -6px; }
        .meta { margin: 22px 0; }.meta td { padding: 8px; border-bottom: 1px solid #d6deea; width:33%; }
        .addresses td { width:50%; vertical-align:top; line-height:1.6; overflow-wrap:anywhere; }
        .label { font-size:10px; font-weight:bold; color:#667085; }
        .lessons { width:100%; border-collapse:collapse; table-layout:fixed; }
        .lessons th {
            background: #edf2f8;
            text-align: left;
            vertical-align: middle;
        }

        .lessons th.money {
            text-align: right;
        }
        .lessons td,.lessons th { padding:8px 5px; border-bottom:1px solid #d6deea; overflow-wrap:anywhere; }
        .lessons thead { display:table-header-group; }.lessons tr { break-inside:avoid; page-break-inside:avoid; }
        .money { text-align:right; }.total { margin-top:18px; border-top:3px solid #183a60; padding:12px 0; text-align:right; font-size:17px; }
        .signature { margin-top:25px; break-inside:avoid; page-break-inside:avoid; }
        .general-notes { margin-top:14px; }
        .invoice-note-text {
            white-space: pre-wrap;
            overflow-wrap: anywhere;
            word-wrap: break-word;
        }

        .lessons tr.invoice-text-row {
            break-inside: auto;
            page-break-inside: auto;
        }

        .lessons td {
            vertical-align: top;
        }
        </style></head><body>
        <table class="header"><tr><td><h2>{{E(i.Name)}}</h2>Independent Contractor | {{E(o.ServiceDescription)}} Services</td><td><h1>INVOICE</h1></td></tr></table>
        <table class="meta"><tr><td><span class="label">INVOICE NO.</span><br>{{E(i.InvoiceNumber)}}</td><td><span class="label">INVOICE DATE</span><br>{{Date(invoice.InvoiceDate)}}</td><td><span class="label">CURRENCY</span><br>{{E(invoice.Currency)}}</td></tr>
        <tr><td><span class="label">SERVICE PERIOD</span><br>{{Date(i.StartDate)}} - {{Date(i.EndDate)}}</td><td><span class="label">PAYMENT DUE</span><br>{{Date(invoice.PaymentDue)}}</td><td><span class="label">TOTAL DUE</span><br><strong>{{Money(invoice.Total)}} {{E(invoice.Currency)}}</strong></td></tr></table>
        <table class="addresses"><tr><td><strong>FROM</strong><br><strong>{{E(i.Name)}}</strong><br>{{E(i.StreetAddress)}}<br>{{E(i.City)}} {{E(i.ProvinceOrState)}} {{E(i.PostalCode)}}<br>{{E(i.Country)}}<br>{{E(i.Email)}}<br>{{E(i.PhoneNumber)}}</td>
        <td><strong>BILL TO</strong><br><strong>{{E(o.CompanyName)}}</strong><br>{{E(o.CompanyStreet)}}<br>{{E(o.CompanyCityLine)}}<br>{{E(o.CompanyEmail)}}</td></tr></table>
        <h3>ITEMS</h3><table class="lessons"><thead><tr><th style="width:5%">#</th><th style="width:25%">Service date/time</th><th style="width:50%">Assignment</th><th style="width:20%; text-align: right;">Amount {{E(invoice.Currency)}}</th></tr></thead><tbody>{{rows}}</tbody></table>
        <div class="total"><strong>TOTAL DUE: {{Money(invoice.Total)}} {{E(invoice.Currency)}}</strong></div>
        {{generalNotes}}
        <h3>PAYMENT INFORMATION</h3><p>Payment method: Electronic transfer<br>E-transfer email: {{E(i.ETransferEmail ?? i.Email)}}</p>
        <p>Please include invoice number {{E(i.InvoiceNumber)}} with the payment.</p>
        <div class="signature"><strong>CONTRACTOR SIGNATURE</strong><br>
        <signature-field name="Teacher signature" role="Teacher" required="true" style="display:inline-block;width:240px;height:80px"></signature-field>
        <p>{{E(i.Name)}} | Independent Contractor</p></div>
        </body></html>
        """;
    }
}
