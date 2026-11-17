using Android.Content;
using Android.Graphics;
using Android.Views;
using MikisWorld.Core;
using CoreCanvas = MikisWorld.Core.Canvas;
using CoreBitmap = MikisWorld.Core.Bitmap;
using ABitmap = Android.Graphics.Bitmap;
using ACanvas = Android.Graphics.Canvas;

namespace MikisWorld.Android;

public sealed class GameView : View
{
    private readonly App _app;
    private readonly AndroidFontMetrics _fonts;

    private CoreBitmap _frame;
    private CoreCanvas _coreCanvas;
    private ABitmap _androidBitmap;

    private readonly Paint _imagePaint = new(PaintFlags.FilterBitmap);
    private readonly Paint _textPaint = new(PaintFlags.AntiAlias);
    private readonly Paint _buttonPaint = new(PaintFlags.AntiAlias);

    private long _lastFrame;
    private bool _running = true;

    private int _logicalWidth;
    private int _logicalHeight;

    private int _pressedKey = -1;
    private int _pressedAction = -1;

    public GameView(Context context, App app) : base(context)
    {
        _app = app ?? throw new ArgumentNullException(nameof(app));
        _fonts = new AndroidFontMetrics();

        Focusable = true;
        FocusableInTouchMode = true;

        _logicalWidth = app.ViewWidth;
        _logicalHeight = app.ViewHeight;

        _frame = new CoreBitmap(_logicalWidth, _logicalHeight);
        _coreCanvas = new CoreCanvas(_frame);
        _coreCanvas.Metrics = _fonts;

        _lastFrame = global::Android.OS.SystemClock.UptimeMillis();
    }

    public void Stop()
    {
        _running = false;
        ReleaseBitmap();
        _fonts.Dispose();
    }

    private void ReleaseBitmap()
    {
        if (_androidBitmap != null)
        {
            _androidBitmap.Dispose();
            _androidBitmap = null;
        }
    }

    protected override void OnDraw(ACanvas canvas)
    {
        base.OnDraw(canvas);

        if (!_running)
            return;

        long now = global::Android.OS.SystemClock.UptimeMillis();
        int dt = (int)Math.Clamp(now - _lastFrame, 1, 100);
        _lastFrame = now;

        try
        {
            _app.Update(dt);
            _coreCanvas.Texts.Clear();
            _app.Draw(_coreCanvas);

            DrawFrame(canvas);
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Error("MikisWorld", ex.ToString());

            canvas.DrawColor(Color.Black);
            using var errorPaint = new Paint(PaintFlags.AntiAlias)
            {
                Color = Color.White,
                TextSize = 14
            };

            canvas.DrawText(
                "Erro ao desenhar o jogo",
                20,
                40,
                errorPaint);
        }

        if (_running)
            PostInvalidateDelayed(16);
    }

    private void DrawFrame(ACanvas canvas)
    {
        ReleaseBitmap();

        _androidBitmap = ABitmap.CreateBitmap(
            _frame.Pixels,
            _logicalWidth,
            _logicalHeight,
            ABitmap.Config.Argb8888);

        canvas.DrawColor(Color.Black);

        float scale = Math.Min(
            (float)Width / _logicalWidth,
            (float)Height / _logicalHeight);

        float drawWidth = _logicalWidth * scale;
        float drawHeight = _logicalHeight * scale;

        float left = (Width - drawWidth) / 2f;
        float top = (Height - drawHeight) / 2f;

        var destination = new RectF(
            left,
            top,
            left + drawWidth,
            top + drawHeight);

        _imagePaint.FilterBitmap = false;
        canvas.DrawBitmap(_androidBitmap, null, destination, _imagePaint);

        DrawTexts(canvas, left, top, scale);
        DrawTouchControls(canvas, left, top, scale);
    }

    private void DrawTexts(ACanvas canvas, float left, float top, float scale)
    {
        foreach (TextCommand command in _coreCanvas.Texts)
        {
            _textPaint.Color = new Color(
                255,
                (int)((command.Rgb >> 16) & 255),
                (int)((command.Rgb >> 8) & 255),
                (int)(command.Rgb & 255));

            _textPaint.TextSize =
                (command.Font == FontKind.Bold ? 12f : 10f) * scale;

            _textPaint.SetTypeface(
                command.Font == FontKind.Bold
                    ? Typeface.Create("sans-serif", TypefaceStyle.Bold)
                    : Typeface.Create("monospace", TypefaceStyle.Normal));

            float x = left + command.X * scale;
            float y = top + command.Y * scale;

            if ((command.Anchor & CoreCanvas.HCenter) != 0)
                _textPaint.TextAlign = Paint.Align.Center;
            else if ((command.Anchor & CoreCanvas.Right) != 0)
                _textPaint.TextAlign = Paint.Align.Right;
            else
                _textPaint.TextAlign = Paint.Align.Left;

            Paint.FontMetrics metrics = _textPaint.GetFontMetrics();

            if ((command.Anchor & CoreCanvas.Baseline) != 0)
            {
                // Y já representa a linha de base.
            }
            else if ((command.Anchor & CoreCanvas.VCenter) != 0)
            {
                y -= (metrics.Ascent + metrics.Descent) / 2f;
            }
            else if ((command.Anchor & CoreCanvas.Bottom) != 0)
            {
                y -= metrics.Descent;
            }
            else
            {
                y -= metrics.Ascent;
            }

            canvas.DrawText(command.Text ?? "", x, y, _textPaint);
        }
    }

    private void DrawTouchControls(
        ACanvas canvas,
        float left,
        float top,
        float scale)
    {
        // Os controles ficam disponíveis na parte inferior da tela.
        float y = top + _logicalHeight * scale - 52f * scale;
        float radius = Math.Max(15f, 17f * scale);

        _buttonPaint.Color = new Color(105, 255, 255, 255);
        _buttonPaint.SetStyle(Paint.Style.Fill);

        canvas.DrawCircle(left + 25f * scale, y, radius, _buttonPaint);
        canvas.DrawCircle(left + 65f * scale, y, radius, _buttonPaint);
        canvas.DrawCircle(left + _logicalWidth * scale - 28f * scale, y, radius, _buttonPaint);

        _buttonPaint.Color = Color.White;
        _buttonPaint.TextSize = Math.Max(12f, 15f * scale);
        _buttonPaint.TextAlign = Paint.Align.Center;

        canvas.DrawText("<", left + 25f * scale, y + 5f * scale, _buttonPaint);
        canvas.DrawText(">", left + 65f * scale, y + 5f * scale, _buttonPaint);
        canvas.DrawText("A", left + _logicalWidth * scale - 28f * scale,
            y + 5f * scale, _buttonPaint);
    }

    private void ReleaseVirtualKey()
    {
        if (_pressedKey >= 0)
        {
            _app.KeyReleased(_pressedKey, _pressedAction);
            _pressedKey = -1;
            _pressedAction = -1;
        }
    }

    private void PressVirtualKey(int key, int action)
    {
        ReleaseVirtualKey();

        _pressedKey = key;
        _pressedAction = action;

        _app.KeyPressed(key, action);
    }

    public override bool OnTouchEvent(MotionEvent e)
    {
        if (!_running)
            return false;

        float scale = Math.Min(
            (float)Width / _logicalWidth,
            (float)Height / _logicalHeight);

        if (scale <= 0)
            return false;

        float offsetX = (Width - _logicalWidth * scale) / 2f;
        float offsetY = (Height - _logicalHeight * scale) / 2f;

        int x = (int)((e.GetX() - offsetX) / scale);
        int y = (int)((e.GetY() - offsetY) / scale);

        x = Math.Clamp(x, 0, _logicalWidth - 1);
        y = Math.Clamp(y, 0, _logicalHeight - 1);

        switch (e.ActionMasked)
        {
            case MotionEventActions.Down:
            case MotionEventActions.PointerDown:
                _app.PointerMoved(x, y);
                _app.PointerPressed(x, y);

                if (y >= _logicalHeight * 0.62f)
                {
                    if (x < _logicalWidth * 0.25f)
                        PressVirtualKey(52, 2); // esquerda
                    else if (x < _logicalWidth * 0.48f)
                        PressVirtualKey(54, 5); // direita
                    else if (x >= _logicalWidth * 0.72f)
                        PressVirtualKey(53, 8); // pulo/confirmar
                }

                Invalidate();
                return true;

            case MotionEventActions.Move:
                _app.PointerMoved(x, y);
                return true;

            case MotionEventActions.Up:
            case MotionEventActions.PointerUp:
            case MotionEventActions.Cancel:
                ReleaseVirtualKey();
                Invalidate();
                return true;
        }

        return true;
    }
}
