using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace Blitztext;

/// <summary>
/// Erzeugt und animiert die Blitztext-Tray-Icons. Vektor-Frames werden EINMAL beim Start gerendert
/// + gecacht (kein Neu-Zeichnen pro Tick → kein Handle-Leck über lange Läufe). Ein DispatcherTimer
/// (~90 ms) schaltet im animierten Zustand die gecachten Frames durch; statische Zustände setzen
/// das Icon einmal.
/// </summary>
internal sealed class TrayAnimator : IDisposable
{
    private const int IconSize = 32; // Windows skaliert auf den Tray-Slot (16/20/24) herunter
    private const int RecordingFrames = 20;   // ~1,8 s Puls bei 90 ms
    private const int ProcessingFrames = 24;

    private readonly Action<WindowIcon> _setIcon;
    private readonly DispatcherTimer _timer;
    private readonly Dictionary<TrayVisual, WindowIcon[]> _frames = new();

    private bool _reduceMotion;
    private TrayVisual _state = TrayVisual.Idle;
    private int _frameIndex;

    public TrayAnimator(Action<WindowIcon> setIcon, bool reduceMotion)
    {
        _setIcon = setIcon;
        _reduceMotion = reduceMotion;

        _frames[TrayVisual.Idle]       = Render(TrayVisual.Idle, 1);
        _frames[TrayVisual.Recording]  = Render(TrayVisual.Recording, RecordingFrames);
        _frames[TrayVisual.Processing] = Render(TrayVisual.Processing, ProcessingFrames);
        _frames[TrayVisual.Success]    = Render(TrayVisual.Success, 1);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(90) };
        _timer.Tick += (_, _) => OnTick();

        SetState(TrayVisual.Idle);
    }

    public WindowIcon InitialIcon => _frames[TrayVisual.Idle][0];

    public void SetState(TrayVisual state)
    {
        _state = state;
        _timer.Stop();

        var frames = _frames[state];
        bool animate = !_reduceMotion && frames.Length > 1;

        if (animate)
        {
            _frameIndex = 0;
            _setIcon(frames[0]);
            _timer.Start();
        }
        else
        {
            _frameIndex = 0;
            _setIcon(frames[0]);
        }
    }

    public void SetReduceMotion(bool reduce)
    {
        if (_reduceMotion == reduce) return;
        _reduceMotion = reduce;
        SetState(_state);
    }

    private void OnTick()
    {
        var frames = _frames[_state];
        if (frames.Length <= 1) { _timer.Stop(); return; }
        _frameIndex = (_frameIndex + 1) % frames.Length;
        _setIcon(frames[_frameIndex]);
    }

    private static WindowIcon[] Render(TrayVisual state, int count)
    {
        var arr = new WindowIcon[count];
        for (int i = 0; i < count; i++)
        {
            double phase = count <= 1 ? 0.0 : (double)i / count;
            arr[i] = RenderFrame(state, phase);
        }
        return arr;
    }

    private static WindowIcon RenderFrame(TrayVisual state, double phase)
    {
        var rtb = new RenderTargetBitmap(new PixelSize(IconSize, IconSize), new Vector(96, 96));
        using (var ctx = rtb.CreateDrawingContext())
        {
            BtEmblem.DrawTray(ctx, IconSize, state, phase);
        }

        using var ms = new MemoryStream();
        rtb.Save(ms);
        ms.Position = 0;
        var icon = new WindowIcon(ms);
        rtb.Dispose();
        return icon;
    }

    public void Dispose() => _timer.Stop();
}
