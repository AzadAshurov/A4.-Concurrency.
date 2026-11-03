# Image Pixelation — Single & Multi-Threaded Processing

A cross-platform image pixelation application built with **C# and .NET 10**. The application supports single-threaded and multi-threaded image processing with real-time visualization.

## 1. Download and Run

**You do not need Visual Studio, an IDE, or an installed .NET runtime to run the pre-built application.**

### Step 1 — Download

1. Open the **Releases** section of this GitHub repository.
2. Select the latest release (`v1.0.0`).
3. Download **`Pixelation-Release-AllPlatforms.zip`** from Assets.
4. Extract the ZIP file to any location on your computer.

### Step 2 — Choose Your Platform

The downloaded archive contains three executable versions and a shared test image:

```text
Pixelation-Release/
├── publish_windows/
│   └── Pixelation.exe
├── publish_mac_arm64/
│   └── Pixelation
├── publish_mac_intel/
│   └── Pixelation
└── ImageSourceAndResult/
    └── basicSource.jpg
```

**Windows (x64)**

Open the `publish_windows` folder and run `Pixelation.exe`.

Alternatively, open PowerShell inside that folder and execute:

```powershell
.\Pixelation.exe
```

**macOS — Apple Silicon (M1, M2, M3, M4, etc.)**

Open Terminal in `publish_mac_arm64` and execute:

```bash
chmod +x Pixelation
./Pixelation
```

**macOS — Intel**

Open Terminal in `publish_mac_intel` and execute:

```bash
chmod +x Pixelation
./Pixelation
```

**Note:** macOS may restrict unsigned applications. Depending on the system's security settings, the application may require approval before it can run. The macOS builds should be tested on the corresponding hardware.

Keep the shared `ImageSourceAndResult` folder alongside the three platform folders so that the test presets can locate the source image.

### Step 3 — Choose a Processing Mode

After launching the application, the following menu appears:

```text
Choose processing preset:

1. Single-threaded (30px, test image)
2. Multi-threaded (7px, test image)
3. Custom settings

Choice:
```

| Option | Description |
|---|---|
| 1 | Processes the provided test image sequentially using 30×30 pixel squares |
| 2 | Processes the provided test image in parallel using 7×7 pixel squares |
| 3 | Allows you to enter a custom image path, square size, and processing mode (S/M) |

Enter the corresponding number and press **Enter**.

Once started, a window opens and displays the image as it is progressively pixelated.

### Step 4 — Find the Result

The processed image is saved as **`result.jpg`**.

- **Preset modes (1 and 2):** Saved in the shared `ImageSourceAndResult` directory.
- **Custom mode (3):** Saved to the user's Desktop.

The output preserves the original image dimensions, even if the preview was resized to fit the screen.

---

## 2. How It Works

The program performs image pixelation through RGB color averaging.

1. Load the original JPG image.
2. Divide it into square blocks of the specified size.
3. Calculate the average red, green, and blue values for every block.
4. Replace every pixel in the block with its average color.
5. Update the displayed image progressively during processing.
6. Save the completed image as `result.jpg`.

Blocks near the image boundaries are processed even when the image dimensions are not divisible by the square size.

### Single-Threaded Mode (S)

The image is processed sequentially, from left to right and top to bottom.

Only one worker processes the image blocks at a time.

### Multi-Threaded Mode (M)

The program uses C#'s `Parallel.For` to process multiple rows of image blocks concurrently.

The maximum degree of parallelism is determined using:

```csharp
Environment.ProcessorCount
```

Each worker processes separate image regions, avoiding overlapping writes.

### Real-Time Visualization

The application uses **Avalonia UI** to display processing progress.

- Image processing runs on background threads.
- The interface refreshes approximately every 50 milliseconds.
- Completed blocks become visible as processing progresses.
- Large images are scaled down for display only.
- All calculations use the original image resolution.

A short artificial delay is included to make the processing easier to observe. It should be removed when measuring actual processing performance.

---

## 3. Running from Source Code

Developers can also build and run the application directly from the GitHub repository.

### Requirements

- .NET 10 SDK
- Windows or macOS
- Internet access for the initial NuGet package restoration

An IDE is optional.

### Clone the Repository

```bash
git clone <repository-url>
cd A4.-Concurrency
```

Replace `<repository-url>` with the GitHub repository's HTTPS clone URL.

### Restore and Run

```bash
dotnet restore
dotnet run
```

The required NuGet packages will be restored automatically.

### Run with Command-Line Arguments

The application also supports three arguments:

```text
Pixelation <image-path> <square-size> <S|M>
```

Example:

```bash
dotnet run -- "photo.jpg" 20 S
```

For parallel processing:

```bash
dotnet run -- "photo.jpg" 20 M
```

Here:

- `image-path` — path to a JPG image.
- `square-size` — positive integer defining the block dimensions.
- `S` — single-threaded processing.
- `M` — multi-threaded processing.

When using command-line arguments, the resulting image is saved on the Desktop.

---

## 4. Technologies Used

| Technology | Purpose |
|---|---|
| C# / .NET 10 | Application logic and execution |
| Avalonia UI | Cross-platform graphical interface |
| SkiaSharp | Image loading, pixel access, and JPEG encoding |
| Task Parallel Library | Parallel image processing |
| Git / GitHub | Version control and distribution |

## 5. Source Code Structure

```text
A4.-Concurrency/
├── ImageSourceAndResult/
│   └── basicSource.jpg
├── Program.cs
├── Pixelation.csproj
├── README.md
└── .gitignore
```

The application intentionally uses a minimal project structure.

All application logic, including the interface, processing algorithm, and concurrency, is implemented in `Program.cs`.

Generated build files and temporary directories are excluded from version control.

## 6. Building Standalone Executables

To create self-contained applications without requiring users to install .NET, use the following commands.

**Windows x64:**

```bash
dotnet publish Pixelation.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

**macOS ARM64:**

```bash
dotnet publish Pixelation.csproj -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

**macOS Intel:**

```bash
dotnet publish Pixelation.csproj -c Release -r osx-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

Each platform requires a separate build.

The pre-built executable packages are available under **GitHub Releases**.

---

## 7. Project Goals

This project demonstrates:

- Sequential and parallel image processing
- CPU-based concurrency
- RGB color averaging
- Real-time visualization of background operations
- Cross-platform application development
- Portable software distribution
- Incremental development using Git commits

The main goal is to compare sequential and multi-threaded processing while maintaining an understandable algorithm and a simple user experience.