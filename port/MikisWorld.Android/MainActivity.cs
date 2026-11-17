using Android.App;
using Android.Content.PM;
using Android.OS;
using MikisWorld.Core;
using AndroidGameActivity = Android.App.Activity;

namespace MikisWorld.Android;

[Activity(
    Label = "Miki's World",
    MainLauncher = true,
    Exported = true,
    ScreenOrientation = ScreenOrientation.Landscape,
    ConfigurationChanges =
        ConfigChanges.Orientation |
        ConfigChanges.ScreenSize |
        ConfigChanges.KeyboardHidden)]
public sealed class MainActivity : AndroidGameActivity
{
    private AndroidAssets _assets;
    private AndroidGameHost _host;
    private GameView _view;
    private App _app;

    protected override void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        ShowGameSelection();
    }

    private void ShowGameSelection()
    {
        string[] games =
        {
            "Miki's World 1: The Lost Tiara",
            "Miki's World 2: The Storm Master"
        };

        new AlertDialog.Builder(this)
            .SetTitle("Escolha o jogo")
            .SetItems(games, (_, args) =>
            {
                int game = args.Which + 1;
                ShowResolutionSelection(game);
            })
            .SetCancelable(false)
            .Show();
    }

    private void ShowResolutionSelection(int game)
    {
        string[] resolutions =
        {
            "176 x 144 - resolução original maior",
            "128 x 128 - resolução quadrada"
        };

        new AlertDialog.Builder(this)
            .SetTitle("Escolha a resolução")
            .SetItems(resolutions, (_, args) =>
            {
                GameSize size = args.Which == 0
                    ? GameSize.Large
                    : GameSize.Small;

                StartGame(game, size);
            })
            .SetCancelable(false)
            .Show();
    }

    private void StartGame(int game, GameSize resolution)
    {
        try
        {
            _assets = new AndroidAssets(Assets, game, resolution);
            _host = new AndroidGameHost(_assets, this);

            var storage = new AndroidStorage(this);
            var settings = new DisplaySettings
            {
                Resolution = resolution
            };

            var bindings = new InputBindings();

            AppInfo info = game == 1
                ? AppInfo.MikisWorld1
                : AppInfo.MikisWorld2;

            long seed = DateTime.UtcNow.Ticks;

            _app = new App(
                h => game == 1
                    ? new Game(_assets, h, seed)
                    : new Game2(_assets, h, seed),
                _assets,
                _host,
                storage,
                info,
                settings,
                bindings);

            _view = new GameView(this, _app);
            SetContentView(_view);
            _view.RequestFocus();
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Error("MikisWorld", ex.ToString());

            new AlertDialog.Builder(this)
                .SetTitle("Não foi possível iniciar")
                .SetMessage(ex.Message)
                .SetPositiveButton("Fechar", (_, _) => Finish())
                .Show();
        }
    }

    public override void OnBackPressed()
    {
        if (_app != null)
        {
            _app.Back();
            _view?.Invalidate();
            return;
        }

        base.OnBackPressed();
    }

    protected override void OnPause()
    {
        base.OnPause();
        _host?.StopSound();
    }

    protected override void OnDestroy()
    {
        _view?.Stop();
        _host?.Dispose();
        _assets?.Dispose();

        base.OnDestroy();
    }
}
