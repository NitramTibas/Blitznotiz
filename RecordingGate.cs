namespace Blitztext;

internal enum RecordingState { Idle, Recording, Processing }

// Admission and generation checks are synchronous; microphone/network work is queued separately.
internal sealed class RecordingGate
{
    public const int ToggleTrigger = -1;
    public RecordingState State { get; private set; }
    public int Session { get; private set; }
    public int ActiveTrigger { get; private set; }
    public bool CaptureStarted { get; private set; }

    public bool TryStart(int trigger)
    {
        if (State != RecordingState.Idle) return false;
        Session++;
        ActiveTrigger = trigger;
        CaptureStarted = false;
        State = RecordingState.Recording;
        return true;
    }

    public bool TryBeginCapture(int session)
    {
        if (session != Session || State != RecordingState.Recording || CaptureStarted) return false;
        CaptureStarted = true;
        return true;
    }

    public bool TryStop(int trigger)
    {
        if (State != RecordingState.Recording || ActiveTrigger != trigger) return false;
        State = RecordingState.Processing;
        return true;
    }

    public bool IsCurrent(int session) => session == Session && State != RecordingState.Idle;
    public void Complete(int session)
    {
        if (session != Session) return;
        State = RecordingState.Idle;
        CaptureStarted = false;
    }

    public void Cancel()
    {
        Session++;
        State = RecordingState.Idle;
        CaptureStarted = false;
    }
}
