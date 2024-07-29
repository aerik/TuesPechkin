using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TuesPechkin;

namespace Scratch
{
    class Program
    {
        static void Main(string[] args)
        {
            int maybeError = Marshal.GetLastWin32Error();
            if(maybeError > 0)
            {
                Console.WriteLine("Found error on startup: " + maybeError);
            }
            Trace.AutoFlush = true;
            var consoleTraceListener = new ConsoleTraceListener();
            Tracer.Source.Switch = new SourceSwitch("SourceSwitch", "Verbose");
            Tracer.Source.Listeners.Add(consoleTraceListener);

            SavePDF(@".\test1.pdf");

            Console.WriteLine("done");
            Console.ReadKey();
        }

        private static void SavePDF(string fileName)
        {
            string path = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
            var toolSet = new RemotingToolset<PdfToolset>(
                        new StaticDeployment(path));//wkhtmltox.dll

            var Converter = new AsyncConverter(toolSet);
            byte[] pdfBytes = null;
            HtmlToPdfDocument document = GetDocument();
            try
            {
                CancellationTokenSource cts = new CancellationTokenSource();
                pdfBytes = Converter.Convert(document);
                Console.WriteLine("**** Done with first pass ****");

                pdfBytes = Converter.Convert(document);
                CancellationTokenSource tokenSource = new CancellationTokenSource();
                var task = Converter.ConvertAsync(document,tokenSource.Token);
                task.Wait(500);
                tokenSource.Cancel();
                if (task.IsCompleted && !task.IsCanceled && !task.IsFaulted)
                {
                    pdfBytes = task.Result;
                }
                tokenSource = new CancellationTokenSource();
                task = Converter.ConvertAsync(document, tokenSource.Token);
                task.Wait(5000);
                tokenSource.Cancel();
                if (task.IsCompleted && !task.IsCanceled && !task.IsFaulted)
                {
                    pdfBytes = task.Result;
                }
                Converter.Abort();
            }
            catch(AggregateException agg)
            {
                Exception x = agg.Flatten();
            }
            catch (Exception ex) {
                Console.WriteLine("ERROR: " + ex.Message);
            }

            toolSet.Unload();
            if(pdfBytes != null) File.WriteAllBytes(fileName, pdfBytes);
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
