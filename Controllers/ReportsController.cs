using CEMPACKSYS.Data;
using CEMPACKSYS.Models;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Bibliography;
using DocumentFormat.OpenXml.Spreadsheet;
using Humanizer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace CEMPACKSYS.Controllers
{
    public class ReportsController : Controller
    {
        private readonly CEMPACKSYSContext _context;
        public ReportsController(CEMPACKSYSContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }
        public IActionResult Analytics()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetReport(DateTime from, DateTime to, string? shift)
        {
            var query = _context.WeightRecords
                .Where(x => x.ReceivedAt >= from && x.ReceivedAt <= to);

            if (!string.IsNullOrEmpty(shift))
            {
                query = shift switch
                {
                    "A" => query.Where(x => x.ReceivedAt.TimeOfDay >= TimeSpan.FromHours(6) &&
                                            x.ReceivedAt.TimeOfDay < TimeSpan.FromHours(14)),

                    "B" => query.Where(x => x.ReceivedAt.TimeOfDay >= TimeSpan.FromHours(14) &&
                                            x.ReceivedAt.TimeOfDay < TimeSpan.FromHours(22)),

                    "C" => query.Where(x => x.ReceivedAt.TimeOfDay >= TimeSpan.FromHours(22) ||
                                            x.ReceivedAt.TimeOfDay < TimeSpan.FromHours(6)),

                    _ => query
                };
            }

            var data = await query.OrderBy(x => x.ReceivedAt).ToListAsync();

            return Json(data);
        }
        public async Task<IActionResult> ExportExcel(DateTime from, DateTime to, string? shift)
        {
            var query = _context.WeightRecords
                .Where(x => x.ReceivedAt >= from && x.ReceivedAt <= to);

            query = ApplyShiftFilter(query, shift);

            var data = await query.OrderBy(x => x.ReceivedAt).ToListAsync();

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Report");

            worksheet.Cell(1, 1).Value = "Packer";
            worksheet.Cell(1, 2).Value = "Spout";
            worksheet.Cell(1, 3).Value = "Weight";
            worksheet.Cell(1, 4).Value = "Time";

            for (int i = 0; i < data.Count; i++)
            {
                worksheet.Cell(i + 2, 1).Value = data[i].PackerNo;
                worksheet.Cell(i + 2, 2).Value = data[i].SpoutNo;
                worksheet.Cell(i + 2, 3).Value = data[i].Weight;
                worksheet.Cell(i + 2, 4).Value = data[i].ReceivedAt;
            }

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;

            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "WeightReport.xlsx");
        }

        private IQueryable<WeightRecord> ApplyShiftFilter(IQueryable<WeightRecord> query, string? shift)
        {


            if (string.IsNullOrEmpty(shift))
                return query;

            return shift switch
            {
                "A" => query.Where(x => x.ReceivedAt.TimeOfDay >= TimeSpan.FromHours(6) &&
                                        x.ReceivedAt.TimeOfDay < TimeSpan.FromHours(14)),

                "B" => query.Where(x => x.ReceivedAt.TimeOfDay >= TimeSpan.FromHours(14) &&
                                        x.ReceivedAt.TimeOfDay < TimeSpan.FromHours(22)),

                "C" => query.Where(x => x.ReceivedAt.TimeOfDay >= TimeSpan.FromHours(22) ||
                                        x.ReceivedAt.TimeOfDay < TimeSpan.FromHours(6)),

                _ => query
            };
        }

        public async Task<IActionResult> ExportPDF(DateTime from, DateTime to, string? shift)
        {
            var query = _context.WeightRecords.Where(x => x.ReceivedAt >= from && x.ReceivedAt <= to);

            query = ApplyShiftFilter(query, shift);

            var data = await query.OrderBy(x => x.ReceivedAt).ToListAsync();
            QuestPDF.Settings.License = LicenseType.Community;

            var pdf = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(30);

                    page.Header()
                        .Text("Weight Report")
                        .FontSize(20)
                        .Bold()
                        .AlignCenter();

                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });

                        table.Header(header =>
                        {
                            header.Cell().Text("Packer").Bold();
                            header.Cell().Text("Spout").Bold();
                            header.Cell().Text("Weight").Bold();
                            header.Cell().Text("Time").Bold();
                        });

                        foreach (var item in data)
                        {
                            table.Cell().Text(item.PackerNo.ToString());
                            table.Cell().Text(item.SpoutNo.ToString());
                            table.Cell().Text(item.Weight.ToString("F2"));
                            table.Cell().Text(item.ReceivedAt.ToString());
                        }
                    });
                });
            }).GeneratePdf();
            return File(pdf, "application/pdf", "WeightReport.pdf");
        }

        [HttpGet]
        public IActionResult GetDashboardData(DateTime from, DateTime to, string? shift)
        {
            var query = _context.WeightRecords.Where(x => x.ReceivedAt >= from && x.ReceivedAt <= to);

            query = ApplyShiftFilter(query, shift);

            var data = query.ToList();

            var totalTons = data.Sum(x => x.Weight) / 1000m;
            var avgWeight = data.Any() ? data.Average(x => x.Weight) : 0;
            var totalRecords = data.Count;

            var tonsPerHour = data
                .GroupBy(x => x.ReceivedAt.Hour)
                .Select(g => new
                {
                    Hour = g.Key,
                    Tons = g.Sum(x => x.Weight) / 1000m
                })
                .OrderBy(x => x.Hour)
                .ToList();

            var avgWeightPerSpout = data
                .GroupBy(x => x.SpoutNo)
                .Select(g => new
                {
                    Spout = g.Key,
                    Avg = g.Average(x => x.Weight)
                })
                .OrderBy(x => x.Spout)
                .ToList();

            return Json(new
            {
                totalTons,
                avgWeight,
                totalRecords,
                hours = tonsPerHour.Select(x => x.Hour + ":00"),
                tonsPerHour = tonsPerHour.Select(x => x.Tons),
                spouts = avgWeightPerSpout.Select(x => "Spout " + x.Spout),
                avgWeightPerSpout = avgWeightPerSpout.Select(x => x.Avg)
            });
        }


    }

}


