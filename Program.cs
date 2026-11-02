using System.Diagnostics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;

using SixLabors.ImageSharp.PixelFormats;
using ImageSharpImage = SixLabors.ImageSharp.Image;


class Program
{
    public static string InputPath = "";
    public static int SquareSize;
    public static string Mode = "";

    [STAThread]
    static void Main(string[] args)
    {
        string sizeText;

        if (args.Length == 3)
        {
            InputPath = args[0];
            sizeText = args[1];
            Mode = args[2].ToUpper();
        }
        else if (args.Length == 0)
        {
            Console.Write("Image path: ");
            InputPath = Console.ReadLine()!.Trim('"');

            Console.Write("Square size: ");
            sizeText = Console.ReadLine()!;

            Console.Write("Mode (S/M): ");
            Mode = Console.ReadLine()!.ToUpper();
        }
        else
        {
            Console.WriteLine("Usage: Pixelation image.jpg 20 S");
            return;
        }

        if (!File.Exists(InputPath) ||
            !int.TryParse(sizeText, out int size) ||
            size <= 0 ||
            (Mode != "S" && Mode != "M"))
        {
            Console.WriteLine("Invalid input!");
            return;
        }

        SquareSize = size;

        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .StartWithClassicDesktopLifetime(args);
    }
}

class App : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow();

        base.OnFrameworkInitializationCompleted();
    }
}

class MainWindow : Window
{
    private readonly Avalonia.Controls.Image view = new()
    {
        Stretch = Stretch.Uniform
    };

    private readonly TextBlock status = new()
    {
        Text = "Loading image..."
    };

    private readonly DispatcherTimer timer = new()
    {
        Interval = TimeSpan.FromMilliseconds(50)
    };

    private Rgba32[] original = [];
    private Rgba32[] result = [];
    private int[] completedSquares = [];

    private int width, height;
    private int columns, rows;
    private int previewWidth, previewHeight;
    private long completed;

    private WriteableBitmap? preview;
    private byte[] previewRow = [];
    private bool closed;

    public MainWindow()
    {
        Title = "Image Pixelation";
        Width = 1000;
        Height = 750;
        WindowState = WindowState.Maximized;

        var layout = new DockPanel
        {
            Margin = new Thickness(10)
        };

        status.Margin = new Thickness(0, 10, 0, 0);

        DockPanel.SetDock(status, Dock.Bottom);

        layout.Children.Add(status);
        layout.Children.Add(view);

        Content = layout;

        timer.Tick += (_, _) =>
        {
            DrawImage();

            double progress = completed * 100.0 / completedSquares.Length;

            status.Text = $"Mode: {Program.Mode} | Progress: {progress:F1}%";
        };

        Opened += async (_, _) => await StartProcessing();

        Closed += (_, _) =>
        {
            closed = true;
            timer.Stop();
            preview?.Dispose();
        };
    }

    private async Task StartProcessing()
    {
        try
        {
            await Task.Run(LoadImage);
            if (closed) return;

            double scale = Math.Min(1.0,
                Math.Min(1000.0 / width, 700.0 / height));

            previewWidth = Math.Max(1, (int)(width * scale));
            previewHeight = Math.Max(1, (int)(height * scale));

            previewRow = new byte[previewWidth * 4];

            preview = new WriteableBitmap(
                new PixelSize(previewWidth, previewHeight),
                new Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Opaque
            );

            view.Source = preview;
            DrawImage();

            Stopwatch watch = Stopwatch.StartNew();

            timer.Start();

            await Task.Run(ProcessImage);

            watch.Stop();
            timer.Stop();

            if (closed) return;

            DrawImage();

            status.Text = "Saving result.jpg...";

            string desktop = Environment.GetFolderPath(
                Environment.SpecialFolder.DesktopDirectory);

            if (string.IsNullOrEmpty(desktop))
                desktop = Environment.CurrentDirectory;

            string outputPath = Path.Combine(desktop, "result.jpg");

            await Task.Run(() =>
            {
                using var image = ImageSharpImage.LoadPixelData<Rgba32>(
                    result, width, height);

                image.Save(outputPath);
            });

            if (closed) return;

            status.Text = $"Done! {Program.Mode} | " +
                $"{watch.ElapsedMilliseconds} ms | Saved: {outputPath}";
        }
        catch (Exception e)
        {
            timer.Stop();

            if (!closed)
                status.Text = $"Error: {e.Message}";
        }
    }

    private void LoadImage()
    {
        using var image = ImageSharpImage.Load<Rgba32>(Program.InputPath);

        width = image.Width;
        height = image.Height;

        original = new Rgba32[checked(width * height)];
        image.CopyPixelDataTo(original);

        result = (Rgba32[])original.Clone();

        columns = (width - 1) / Program.SquareSize + 1;
        rows = (height - 1) / Program.SquareSize + 1;

        completedSquares = new int[checked(columns * rows)];
    }

    private void ProcessImage()
    {
        if (Program.Mode == "S")
        {
            for (int row = 0; row < rows; row++)
                ProcessRow(row);
        }
        else
        {
            Parallel.For(0, rows, new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount
            },
            row => ProcessRow(row));
        }
    }

    private void ProcessRow(int row)
    {
        int y = row * Program.SquareSize;

        for (int column = 0; column < columns; column++)
        {
            int x = column * Program.SquareSize;

            int endX = x + Math.Min(Program.SquareSize, width - x);
            int endY = y + Math.Min(Program.SquareSize, height - y);

            long r = 0, g = 0, b = 0;
            long count = (long)(endX - x) * (endY - y);

           
            for (int i = y; i < endY; i++)
            {
                for (int j = x; j < endX; j++)
                {
                    Rgba32 pixel = original[i * width + j];

                    r += pixel.R;
                    g += pixel.G;
                    b += pixel.B;
                }
            }

            Rgba32 average = new Rgba32(
                (byte)(r / count),
                (byte)(g / count),
                (byte)(b / count)
            );

          
            for (int i = y; i < endY; i++)
            {
                for (int j = x; j < endX; j++)
                {
                    result[i * width + j] = average;
                }
            }

            Volatile.Write(ref completedSquares[row * columns + column], 1);
            Interlocked.Increment(ref completed);
        }
    }

    private void DrawImage()
    {
        if (preview == null) return;

        using var frame = preview.Lock();

        for (int y = 0; y < previewHeight; y++)
        {
            int sourceY = (int)((long)y * height / previewHeight);

            for (int x = 0; x < previewWidth; x++)
            {
                int sourceX = (int)((long)x * width / previewWidth);

                int pixelIndex = sourceY * width + sourceX;
                int squareIndex =
                    (sourceY / Program.SquareSize) * columns +
                    (sourceX / Program.SquareSize);

                
                bool done = Volatile.Read(
                    ref completedSquares[squareIndex]) == 1;

                Rgba32 pixel = done
                    ? result[pixelIndex]
                    : original[pixelIndex];

                int index = x * 4;

                previewRow[index] = pixel.B;
                previewRow[index + 1] = pixel.G;
                previewRow[index + 2] = pixel.R;
                previewRow[index + 3] = 255;
            }

            Marshal.Copy(
                previewRow,
                0,
                IntPtr.Add(frame.Address, y * frame.RowBytes),
                previewRow.Length
            );
        }

        view.InvalidateVisual();
    }
}
