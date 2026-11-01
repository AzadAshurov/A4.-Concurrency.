
using System;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using static System.Net.Mime.MediaTypeNames;

class Program
{
    static void Main()
    {
        Console.Write("Image path: ");
        string inputPath = Console.ReadLine()!.Trim('"');

        Console.Write("Square size: ");
        int squareSize = int.Parse(Console.ReadLine()!);

        if (!File.Exists(inputPath) || squareSize <= 0)
        {
            Console.WriteLine("Invalid input!");
            return;
        }

        string outputPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            "result.jpg"
        );

        using Image<Rgba32> image = SixLabors.ImageSharp.Image.Load<Rgba32>(inputPath);

        int width = image.Width;
        int height = image.Height;

        for (int y = 0; y < height; y += squareSize)
        {
            for (int x = 0; x < width; x += squareSize)
            {
                int endX = Math.Min(x + squareSize, width);
                int endY = Math.Min(y + squareSize, height);

                long r = 0, g = 0, b = 0;
                int count = 0;

                
                for (int i = y; i < endY; i++)
                {
                    for (int j = x; j < endX; j++)
                    {
                        Rgba32 pixel = image[j, i];

                        r += pixel.R;
                        g += pixel.G;
                        b += pixel.B;
                        count++;
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
                        image[j, i] = average;
                    }
                }
            }
        }

        image.Save(outputPath);

        Console.WriteLine($"Saved: {outputPath}");
    }
}
