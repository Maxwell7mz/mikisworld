using Android.Content.Res;
using Android.Graphics;
using MikisWorld.Core;
using System.IO.Compression;

namespace MikisWorld.Android;

/// <summary>
/// Carrega os recursos diretamente do JAR original escolhido.
/// Os arquivos originais do repositório não são modificados.
/// </summary>
public sealed class AndroidAssets : IAssets, IDisposable
{
    private readonly Dictionary<string, byte[]> _resources =
        new(StringComparer.Ordinal);

    public string JarName { get; }

    public AndroidAssets(
        AssetManager androidAssets,
        int game,
        GameSize resolution)
    {
        if (androidAssets == null)
            throw new ArgumentNullException(nameof(androidAssets));

        string suffix = resolution == GameSize.Large
            ? "176x144"
            : "128x128";

        JarName = game switch
        {
            1 => $"MikisWorld_{suffix}.jar",
            2 => $"MikisWorld2_{suffix}.jar",
            _ => throw new ArgumentOutOfRangeException(nameof(game))
        };

        LoadJar(androidAssets);
    }

    private void LoadJar(AssetManager androidAssets)
    {
        using Stream input = androidAssets.Open(
            $"JAR/{JarName}",
            global::Android.Content.Res.Access.Streaming);

        using var memory = new MemoryStream();
        input.CopyTo(memory);

        memory.Position = 0;

        using var archive = new ZipArchive(
            memory,
            ZipArchiveMode.Read,
            leaveOpen: false);

        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name))
                continue;

            using Stream entryStream = entry.Open();
            using var output = new MemoryStream();

            entryStream.CopyTo(output);

            _resources[entry.FullName] = output.ToArray();
        }

        if (_resources.Count == 0)
            throw new InvalidDataException(
                $"O JAR {JarName} não contém recursos.");
    }

    public byte[] ReadResource(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(
                "Nome de recurso inválido.",
                nameof(name));

        if (!_resources.TryGetValue(name, out byte[] data))
            throw new KeyNotFoundException(
                $"Recurso '{name}' não encontrado em {JarName}.");

        return data;
    }

    public (int Width, int Height) ImageSize(string name)
    {
        byte[] data = ReadResource(name);

        using global::Android.Graphics.Bitmap image =
            BitmapFactory.DecodeByteArray(data, 0, data.Length)
            ?? throw new InvalidDataException(
                $"Não foi possível decodificar a imagem '{name}'.");

        return (image.Width, image.Height);
    }

    public MikisWorld.Core.Bitmap LoadBitmap(string name)
    {
        byte[] data = ReadResource(name);

        using global::Android.Graphics.Bitmap image =
            BitmapFactory.DecodeByteArray(data, 0, data.Length)
            ?? throw new InvalidDataException(
                $"Não foi possível carregar a imagem '{name}'.");

        int width = image.Width;
        int height = image.Height;
        var pixels = new int[checked(width * height)];

        image.GetPixels(
            pixels,
            0,
            width,
            0,
            0,
            width,
            height);

        return new MikisWorld.Core.Bitmap(
            width,
            height,
            pixels);
    }

    public void Dispose()
    {
        _resources.Clear();
    }
}
