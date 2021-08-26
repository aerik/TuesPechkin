using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TuesPechkin;

namespace Scratch
{
    class Program
    {
        static void Main(string[] args)
        {
            Trace.AutoFlush = true;
            var consoleTraceListener = new ConsoleTraceListener();
            Tracer.Source.Switch = new SourceSwitch("SourceSwitch", "Verbose");
            Tracer.Source.Listeners.Add(consoleTraceListener);
            HtmlToPdfDocument document = GetDocument();

            SavePDF(document, @".\test1.pdf");
            System.Threading.Thread.Sleep(100);
            SavePDF(document, @".\test2.pdf");
            System.Threading.Thread.Sleep(100);
            SavePDF(document, @".\test3.pdf");
            System.Threading.Thread.Sleep(100);
            SavePDF(document, @".\test4.pdf");
            System.Threading.Thread.Sleep(100);
            SavePDF(document, @".\test5.pdf");
            Console.WriteLine("done");
            Console.ReadKey();
        }

        private static void SavePDF(HtmlToPdfDocument document, string fileName)
        {
            string path = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
            var toolSet = new RemotingToolset<PdfToolset>(
                new WinAnyCPUEmbeddedDeployment(
                        new StaticDeployment(path)));//wkhtmltox.dll

            var Converter = new ThreadSafeConverter(toolSet);
            byte[] pdfBytes = null;

            pdfBytes = Converter.Convert(document);
            toolSet.Unload();
            File.WriteAllBytes(fileName, pdfBytes);
        }

        private static HtmlToPdfDocument GetDocument()
        {
            var document = new HtmlToPdfDocument
            {
                GlobalSettings =
                {
                    ProduceOutline = false,
                    ColorMode = GlobalSettings.DocumentColorMode.Color,
                    DocumentTitle = "this is a test",
                    PaperSize = PaperKind.Letter, // Implicit conversion to PechkinPaperSize
                    DPI=300,
                    ImageDPI = 300,
                    ImageQuality = 100,
                    Margins =
                    {
                        Top = 12.7, /* half an inch in mm */
                        Left = 12.7,
                        Right = 12.7,
                        Unit = Unit.Millimeters
                    }
                },
                Objects = {
                    new ObjectSettings {
                        HtmlText = "<div>Some Test Document</div>", //_CSS +
                        FooterSettings = new FooterSettings{
                            //FontSize = 10,
                            ContentSpacing=0,/* this is always mm, space between content and footer */
                        },
                        WebSettings = new WebSettings
                        {
                            DefaultEncoding="UTF-8"
                        }
                    }
                }
            };
            return document;
        }
    }
}
