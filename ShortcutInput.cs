using System;
using System.Collections.Generic;

namespace Blitztext;

// Shared by both native hooks. No audio, I/O, timers or UI work runs here.
// A press keeps its original routing decision until release, even after reconfiguration.
internal sealed class ShortcutInput
{
    private sealed record Press(bool Consumed, bool Legacy);
    private readonly Dictionary<int, Press> _pressed = new();
    private HashSet<int> _legacy = new();
    private DictationShortcut _binding = new();
    private bool _capturing, _draining, _captureHasResult;
    private DictationShortcut? _captureResult;
    public DictationShortcut? ActiveToggle { get; set; }
    public bool IsCapturing => _capturing || _draining;
    public event Action<int>? LegacyPressed;
    public event Action<int>? LegacyReleased;
    public event Action? EscapePressed;
    public event Action<DictationShortcut>? TogglePressed;
    public event Action<DictationShortcut?>? CaptureCompleted;

    public void Configure(IEnumerable<int> legacy, DictationShortcut? binding)
    {
        _legacy = new HashSet<int>(legacy);
        _binding = binding?.Copy() ?? new DictationShortcut();
        if (_binding.Validate(_legacy) != null) _binding.Enabled = false;
    }

    public void SeedPressed(int key) => _pressed.TryAdd(key, new Press(false, false));

    public void BeginCapture()
    {
        _capturing = true;
        _draining = false;
        _captureHasResult = false;
        _captureResult = null;
    }

    public void CancelCapture()
    {
        _capturing = false;
        _captureResult = null;
        _captureHasResult = false;
        _draining = _pressed.Count != 0;
    }

    private ShortcutModifiers Modifiers
    {
        get
        {
            var modifiers = ShortcutModifiers.None;
            foreach (int key in _pressed.Keys) modifiers |= DictationShortcut.ModifierFor(key);
            return modifiers;
        }
    }

    public bool Process(int key, bool down, bool injected = false)
    {
        if (injected) return false;
        if (!down)
        {
            if (!_pressed.Remove(key, out var press)) return false;
            if (press.Legacy && !_capturing && !_draining) LegacyReleased?.Invoke(key);
            if (_pressed.Count == 0)
            {
                _draining = false;
                if (_capturing && _captureHasResult)
                {
                    var result = _captureResult;
                    _capturing = false;
                    _captureHasResult = false;
                    _captureResult = null;
                    CaptureCompleted?.Invoke(result);
                }
            }
            return press.Consumed;
        }

        if (_pressed.TryGetValue(key, out var repeated)) return repeated.Consumed;
        var modifiers = Modifiers;
        if (_capturing || _draining)
        {
            _pressed[key] = new Press(true, false);
            if (_capturing && !_captureHasResult &&
                (key == KeyNames.VK_ESCAPE || DictationShortcut.IsAssignableKey(key)))
            {
                _captureHasResult = true;
                _captureResult = key == KeyNames.VK_ESCAPE ? null :
                    new DictationShortcut { Key = key, Modifiers = modifiers };
            }
            return true;
        }

        var binding = ActiveToggle ?? _binding;
        bool toggle = binding.Enabled && key == binding.Key && modifiers == binding.Modifiers;
        bool legacy = !toggle && _legacy.Contains(key);
        _pressed[key] = new Press(toggle || legacy, legacy);
        if (key == KeyNames.VK_ESCAPE) EscapePressed?.Invoke();
        else if (toggle) TogglePressed?.Invoke(binding.Copy());
        else if (legacy) LegacyPressed?.Invoke(key);
        return toggle || legacy;
    }
}
