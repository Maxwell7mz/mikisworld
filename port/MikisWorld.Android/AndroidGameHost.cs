using Android.Content;
using Android.Media;
using Android.OS;
using MikisWorld.Core;

namespace MikisWorld.Android;

public sealed class AndroidGameHost : IGameHost, IDisposable
{
    private const int SampleRate = 22050;

    private readonly AndroidAssets _assets;
    private readonly Context _context;
    private readonly Dictionary<Sfx, short[]> _cache = new();

    private AudioTrack _track;
    private bool _disposed;

    public AndroidGameHost(AndroidAssets assets, Context context)
    {
        _assets = assets ?? throw new ArgumentNullException(nameof(assets));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    private short[] LoadSound(Sfx sound)
    {
        if (_cache.TryGetValue(sound, out short[] cached))
            return cached;

        string filename = SfxFiles.FileName(sound);
        byte[] bytes = _assets.ReadResource(filename);

        RingTone ringtone = RingTone.Decode(bytes);
        short[] samples = ringtone.Render(SampleRate);

        _cache[sound] = samples;
        return samples;
    }

    public void Play(Sfx sound)
    {
        if (_disposed)
            return;

        try
        {
            StopSound();

            short[] samples = LoadSound(sound);

            if (samples.Length == 0)
                return;

            var attributes = new AudioAttributes.Builder()
                .SetUsage(AudioUsageKind.Game)
                .SetContentType(AudioContentType.Music)
                .Build();

            var format = new AudioFormat.Builder()
                .SetEncoding(Encoding.Pcm16bit)
                .SetSampleRate(SampleRate)
                .SetChannelMask(ChannelOut.Mono)
                .Build();

            _track = new AudioTrack.Builder()
                .SetAudioAttributes(attributes)
                .SetAudioFormat(format)
                .SetTransferMode(AudioTrackMode.Static)
                .SetBufferSizeInBytes(samples.Length * sizeof(short))
                .Build();

            _track.Write(samples, 0, samples.Length);
            _track.Play();
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Warn("MikisWorld", "Falha ao reproduzir áudio: " + ex.Message);
            StopSound();
        }
    }

    public void StopSound()
    {
        try
        {
            if (_track != null)
            {
                if (_track.PlayState == PlayState.Playing)
                    _track.Stop();

                _track.Release();
                _track.Dispose();
                _track = null;
            }
        }
        catch
        {
            _track = null;
        }
    }

    public void Vibrate(int frequency, int milliseconds)
    {
        if (_disposed || milliseconds <= 0)
            return;

        try
        {
            Vibrator vibrator =
                _context.GetSystemService(Context.VibratorService) as Vibrator;

            if (vibrator == null || !vibrator.HasVibrator)
                return;

            int duration = Math.Clamp(milliseconds, 1, 1500);
            int amplitude = Math.Clamp(frequency, 1, 255);

            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                vibrator.Vibrate(
                    VibrationEffect.CreateOneShot(duration, amplitude));
            }
            else
            {
#pragma warning disable CS0618
                vibrator.Vibrate(duration);
#pragma warning restore CS0618
            }
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Warn("MikisWorld", "Falha na vibração: " + ex.Message);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        StopSound();
        _cache.Clear();
    }
}
