using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Blitztext;

/// <summary>
/// Offline-Erzeuger der App-Icon-.ico. Rendert das Blitztext-Zeichen (BtEmblem.DrawAppIcon) in
/// mehreren Größen und packt sie als PNG-Payloads in eine Multi-Size-.ico (Windows-Vista+-Format).
/// Aufruf über den versteckten CLI-Pfad: Blitztext.exe --gen-icon &lt;pfad.ico&gt; (siehe Program.cs).
/// </summary>
internal static class BtIconGenerator
{
    private static readonly int[] Sizes = { 256, 128, 64, 48, 32, 24, 16 };

    public static void Generate(string path)
    {
        var pngs = new List<byte[]>();
        foreach (var size in Sizes)
        {
            var rtb = new RenderTargetBitmap(new PixelSize(size, size), new Vector(96, 96));
            using (var ctx = rtb.CreateDrawingContext())
            {
                BtEmblem.DrawAppIcon(ctx, size);
            }
            using var ms = new MemoryStream();
            rtb.Save(ms);
            rtb.Dispose();
            pngs.Add(ms.ToArray());
        }
        WriteIco(path, Sizes, pngs);
    }

    /// <summary>Dev-Pfad: rendert App-Icon + Tray-Zustände als PNGs auf dunklem Grund zur Sichtprüfung.
    /// Aufruf: Blitztext.exe --gen-preview &lt;dir&gt;.</summary>
    public static void GeneratePreviews(string dir)
    {
        Directory.CreateDirectory(dir);
        RenderToFile(Path.Combine(dir, "appicon.png"), 256, ctx => BtEmblem.DrawAppIcon(ctx, 256));
        RenderToFile(Path.Combine(dir, "tray-idle.png"), 96, ctx => BtEmblem.DrawTray(ctx, 96, TrayVisual.Idle, 0));
        RenderToFile(Path.Combine(dir, "tray-recording.png"), 96, ctx => BtEmblem.DrawTray(ctx, 96, TrayVisual.Recording, 0));
        RenderToFile(Path.Combine(dir, "tray-processing.png"), 96, ctx => BtEmblem.DrawTray(ctx, 96, TrayVisual.Processing, 0));
        RenderToFile(Path.Combine(dir, "tray-success.png"), 96, ctx => BtEmblem.DrawTray(ctx, 96, TrayVisual.Success, 0));
        RenderToFile(Path.Combine(dir, "tray16.png"), 16, ctx => BtEmblem.DrawTray(ctx, 16, TrayVisual.Idle, 0));
    }

    private static void RenderToFile(string path, int size, Action<DrawingContext> draw)
    {
        var rtb = new RenderTargetBitmap(new PixelSize(size, size), new Vector(96, 96));
        using (var ctx = rtb.CreateDrawingContext())
        {
            ctx.DrawRectangle(new SolidColorBrush(BtPalette.Background), null, new Rect(0, 0, size, size));
            draw(ctx);
        }
        rtb.Save(path);
        rtb.Dispose();
    }

    private static void WriteIco(string path, int[] sizes, List<byte[]> pngs)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        using var fs = File.Create(path);
        using var w = new BinaryWriter(fs);

        // ICONDIR
        w.Write((short)0); // reserved
        w.Write((short)1); // type: 1 = icon
        w.Write((short)sizes.Length);

        int offset = 6 + 16 * sizes.Length;
        for (int i = 0; i < sizes.Length; i++)
        {
            int s = sizes[i];
            w.Write((byte)(s >= 256 ? 0 : s)); // Breite (0 = 256)
            w.Write((byte)(s >= 256 ? 0 : s)); // Höhe
            w.Write((byte)0);   // Palette
            w.Write((byte)0);   // reserviert
            w.Write((short)1);  // Farb-Ebenen
            w.Write((short)32); // Bits pro Pixel
            w.Write(pngs[i].Length);
            w.Write(offset);
            offset += pngs[i].Length;
        }

        foreach (var png in pngs) w.Write(png);
    }
}
