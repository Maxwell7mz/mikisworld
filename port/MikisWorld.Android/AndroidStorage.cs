using Android.Content;
using MikisWorld.Core;
using System.Text.Json;

namespace MikisWorld.Android;

/// <summary>
/// Salvamento privado do Android.
/// Mantém os dados fora dos JARs e do diretório do projeto.
/// </summary>
public sealed class AndroidStorage : IStorage
{
    private readonly string _file;
    private readonly string _backup;
    private readonly string _temporary;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    public AndroidStorage(Context context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string directory = context.FilesDir?.AbsolutePath
            ?? throw new InvalidOperationException(
                "Não foi possível acessar o armazenamento privado.");

        _file = Path.Combine(directory, "mikisworld-save.json");
        _backup = _file + ".bak";
        _temporary = _file + ".tmp";
    }

    public SaveData Load()
    {
        SaveData data = TryLoad(_file);

        if (data != null)
            return Normalize(data);

        data = TryLoad(_backup);

        if (data != null)
            return Normalize(data);

        return new SaveData();
    }

    private static SaveData TryLoad(string path)
    {
        try
        {
            if (!File.Exists(path))
                return null;

            string json = File.ReadAllText(path);

            return JsonSerializer.Deserialize<SaveData>(
                json,
                JsonOptions);
        }
        catch (Exception ex) when (
            ex is IOException ||
            ex is UnauthorizedAccessException ||
            ex is JsonException)
        {
            return null;
        }
    }

    private static SaveData Normalize(SaveData data)
    {
        data.HighScores ??= new List<ScoreEntry>();

        data.HighScores.RemoveAll(entry =>
            entry == null ||
            entry.Name == null);

        return data;
    }

    public void Save(SaveData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        string json = JsonSerializer.Serialize(
            data,
            JsonOptions);

        try
        {
            using (var stream = new FileStream(
                _temporary,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.WriteThrough))
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(json);
                writer.Flush();
                stream.Flush(true);
            }

            // Mantém uma cópia do último salvamento válido.
            if (File.Exists(_file))
                File.Copy(_file, _backup, true);

            // Substituição do arquivo principal após escrita completa.
            File.Move(_temporary, _file, true);
        }
        catch
        {
            // Não apagar a cópia anterior em caso de falha.
            throw;
        }
        finally
        {
            // Um temporário incompleto nunca é tratado como salvamento.
            try
            {
                if (File.Exists(_temporary))
                    File.Delete(_temporary);
            }
            catch
            {
                // A próxima gravação poderá limpar o temporário.
            }
        }
    }
}
