using System;
using System.IO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Documents.Application.Services
{
    public class PdfGeneratorService
    {
        public PdfGeneratorService()
        {
            // QuestPDF требует указания лицензии для бесплатного использования в коммьюнити-режиме
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public byte[] GenerateMedicalReportPdf(string patientName, string complaints, string conclusion, string recommendations)
        {
            // Используем мощный Fluent API QuestPDF для верстки документа кодом
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    // Шрифт не указывается явно: QuestPDF по умолчанию использует встроенный Lato,
                    // который зарегистрирован всегда. Arial в списке зарегистрированных семейств
                    // отсутствует, и его явное указание роняло генерацию с DocumentDrawingException.
                    page.DefaultTextStyle(x => x.FontSize(11));

                    // 1. ШАПКА ДОКУМЕНТА (Header)
                    page.Header()
                        .Text("МЕДИЦИНСКОЕ ЗАКЛЮЧЕНИЕ - INNOWISE CLINIC")
                        .SemiBold().FontSize(18).FontColor(Colors.Blue.Medium); // ИСПРАВЛЕНО: Color -> FontColor

                    // 2. ТЕЛО ДОКУМЕНТА (Content)
                    page.Content().PaddingVertical(1, Unit.Centimetre).Column(column =>
                    {
                        column.Spacing(10);

                        column.Item().Text($"Пациент: {patientName}").Bold().FontSize(13);
                        column.Item().Text($"Дата формирования: {DateTime.Now.ToShortDateString()}").LineHeight(1.5f);
                        
                        // ИСПРАВЛЕНО: Для линий цвет задается через перегрузку самого метода LineHorizontal
                        column.Item().LineHorizontal(1, Unit.Point).LineColor(Colors.Grey.Lighten2);

                        // Секция жалоб
                        column.Item().Text("Жалобы пациента:").Bold();
                        column.Item().Background(Colors.Grey.Lighten4).Padding(10).Text(complaints);

                        // Секция заключения
                        column.Item().Text("Клиническое заключение:").Bold();
                        column.Item().Background(Colors.Grey.Lighten4).Padding(10).Text(conclusion);

                        // Секция рекомендаций
                        column.Item().Text("Рекомендации по лечению:").Bold();
                        
                        // ИСПРАВЛЕНО: Color -> FontColor
                        column.Item().Background(Colors.Blue.Lighten5).Padding(10).Text(recommendations).FontColor(Colors.Blue.Darken3);
                    });

                    // 3. ПОДВАЛ (Footer) с нумерацией страниц
                    page.Footer()
                        .AlignCenter()
                        .Text(x =>
                        {
                            x.Span("Страница ");
                            x.CurrentPageNumber();
                        });
                });
            }).GeneratePdf(); // Метод библиотеки генерирует готовый массив байт PDF
        }
    }
}
