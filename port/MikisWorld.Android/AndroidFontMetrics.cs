using Android.Graphics;
using MikisWorld.Core;

namespace MikisWorld.Android;

public sealed class AndroidFontMetrics : IFontMetrics, IDisposable
{
    private readonly Paint _small;
    private readonly Paint _bold;

    public AndroidFontMetrics()
    {
        _small = CreatePaint(10f, false);
        _bold = CreatePaint(12f, true);
    }

    private static Paint CreatePaint(float size, bool bold)
    {
        var paint = new Paint(PaintFlags.AntiAlias)
        {
            Color = Color.White,
            TextSize = size
        };

        paint.SetTypeface(
            bold
                ? Typeface.Create("sans-serif", TypefaceStyle.Bold)
                : Typeface.Create("monospace", TypefaceStyle.Normal));

        return paint;
    }

    private Paint Get(FontKind font) =>
        font == FontKind.Bold ? _bold : _small;

    public int StringWidth(string s, FontKind font)
    {
        return (int)Math.Ceiling(Get(font).MeasureText(s ?? ""));
    }

    public int Height(FontKind font)
    {
        Paint.FontMetrics metrics = Get(font).GetFontMetrics();
        return (int)Math.Ceiling(metrics.Descent - metrics.Ascent);
    }

    public void Dispose()
    {
        _small.Dispose();
        _bold.Dispose();
    }
}
