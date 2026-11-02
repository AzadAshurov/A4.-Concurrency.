using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;

using SkiaSharp;

class Program
{
    public static string InputPath = "";
    public static string OutputPath = "";
    public static int SquareSize;
    public static string Mode = "";

    [STAThread]
    static void Main(string[] args)
    {
        string sizeText;
        bool preset = false;

        if (args.Length == 3)
        {
            InputPath = args[0];
            sizeText = args[1];
            Mode = args[2].ToUpper();
        }
        else if (args.Length == 0)
        {
            Console.WriteLine("Choose processing preset:");
            Console.WriteLine("1. Single-threaded (30px, test image)");
            Console.WriteLine("2. Multi-threaded (7px, test image)");
            Console.WriteLine("3. Custom settings");
            Console.Write("\nChoice: ");

            string choice = Console.ReadLine() ?? "";

            if (choice == "1" || choice == "2")
            {
                string? path = FindPresetImage();

                if (path == null)
                {
                    Console.WriteLine("Test image not found!");
                    Console.WriteLine(
                        "Expected: ImageSourceAndResult/basicSource.jpg");
                    return;
                }

                InputPath = path;
                sizeText = choice == "1" ? "30" : "7";
                Mode = choice == "1" ? "S" : "M";
                preset = true;
            }
            else if (choice == "3")
            {
                Console.Write("Image path: ");
                InputPath = (Console.ReadLine() ?? "").Trim().Trim('"');

                Console.Write("Square size: ");
                sizeText = Console.ReadLine() ?? "";

                Console.Write("Mode (S/M): ");
                Mode = (Console.ReadLine() ?? "").Trim().ToUpper();
            }
            else
            {
                Console.WriteLine("Invalid choice!");
                return;
            }
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
        InputPath = Path.GetFullPath(InputPath);

        if (preset)
        {
            OutputPath = Path.Combine(
                Path.GetDirectoryName(InputPath)!,
                "result.jpg");
        }
        else
        {
            string desktop = Environment.GetFolderPath(
                Environment.SpecialFolder.DesktopDirectory);

            if (string.IsNullOrEmpty(desktop))
                desktop = Environment.CurrentDirectory;

            OutputPath = Path.Combine(desktop, "result.jpg");
        }

        Console.WriteLine($"\nImage: {InputPath}");
        Console.WriteLine($"Square size: {SquareSize}");
        Console.WriteLine($"Mode: {Mode}");
        Console.WriteLine($"Output: {OutputPath}");

        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .StartWithClassicDesktopLifetime(args);
    }

    static string? FindPresetImage()
    {
        string[] locations =
        {
            Environment.CurrentDirectory,
            AppContext.BaseDirectory
        };

        foreach (string location in locations)
        {
            DirectoryInfo? folder = new DirectoryInfo(location);

            while (folder != null)
            {
                string path = Path.Combine(
                    folder.FullName,
                    "ImageSourceAndResult",
                    "basicSource.jpg");

                if (File.Exists(path))
                    return path;

                folder = folder.Parent;
            }
        }

        return null;
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

    private SKColor[] original = [];
    private SKColor[] result = [];
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

            string outputPath = Program.OutputPath;

            await Task.Run(() =>
            {
                using var bitmap = new SKBitmap(
                    width, height, SKColorType.Rgba8888, SKAlphaType.Opaque);

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                        bitmap.SetPixel(x, y, result[y * width + x]);
                }

                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90)
                    ?? throw new Exception("Cannot encode JPEG image.");
                using var stream = File.Create(outputPath);

                data.SaveTo(stream);
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
        using var image = SKBitmap.Decode(Program.InputPath)
            ?? throw new Exception("Cannot load image.");

        width = image.Width;
        height = image.Height;

        original = new SKColor[checked(width * height)];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
                original[y * width + x] = image.GetPixel(x, y);
        }

        result = (SKColor[])original.Clone();

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
                    SKColor pixel = original[i * width + j];


                    r += pixel.Red;
                    g += pixel.Green;
                    b += pixel.Blue;
                }
            }

            SKColor average = new SKColor(
                (byte)(r / count),
                (byte)(g / count),
                (byte)(b / count)
            );
            Thread.Sleep(50); 


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

                SKColor pixel = done
                    ? result[pixelIndex]
                    : original[pixelIndex];

                int index = x * 4;

                previewRow[index] = pixel.Blue;
                previewRow[index + 1] = pixel.Green;
                previewRow[index + 2] = pixel.Red;
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
