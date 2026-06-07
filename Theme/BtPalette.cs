using System;
using Avalonia.Media;

namespace Blitztext;

/// <summary>
/// C#-Spiegel der Design-Tokens (siehe Theme/Tokens.axaml) für das Vektor-Rendering der Icons.
/// Werte aus dem ORAH-Design-System. Bei Änderung BEIDE Stellen (hier + Tokens.axaml) anpassen.
/// </summary>
internal static class BtPalette
{
    public static readonly Color Background   = Color.Parse("#050608");
    public static readonly Color Surface      = Color.Parse("#111827");
    public static readonly Color Surface2     = Color.Parse("#1F2937");
    public static readonly Color Border       = Color.Parse("#2B3442");
    public static readonly Color TextPrimary  = Color.Parse("#EAF2FA");
    public static readonly Color Muted        = Color.Parse("#8A97A8");
    public static readonly Color Glow         = Color.Parse("#8FD8FF");
    public static readonly Color Accent       = Color.Parse("#7CC7FF");
    public static readonly Color Transcribing = Color.Parse("#76E4F7");
    public static readonly Color Recording    = Color.Parse("#FF5C5C");
    public static readonly Color Success      = Color.Parse("#5FE3A8");
    public static readonly Color Warning      = Color.Parse("#F0B36A");

    public static Color WithAlpha(this Color c, double a) =>
        Color.FromArgb((byte)Math.Clamp(a * 255.0, 0, 255), c.R, c.G, c.B);
}
