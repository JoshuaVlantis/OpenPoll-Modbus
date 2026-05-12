using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OpenSlave.Models;

/// <summary>
/// Wire-level fault simulation knobs for the slave: response delay,
/// random skipped responses, and forced Slave-Busy exception (06).
///
/// CRC-error injection is intentionally not modelled — TCP transports
/// don't carry CRC, and the future serial RTU slave will need a knob
/// added there if we want to corrupt the trailing CRC on the wire.
/// </summary>
public sealed class ErrorSimulation : INotifyPropertyChanged
{
    private int _responseDelayMs;
    private bool _skipResponses;
    private bool _returnExceptionBusy;

    public int ResponseDelayMs
    {
        get => _responseDelayMs;
        set { if (_responseDelayMs != value) { _responseDelayMs = value; OnChanged(); } }
    }

    /// <summary>When on, drops 1 in 10 responses to simulate a flaky link.</summary>
    public bool SkipResponses
    {
        get => _skipResponses;
        set { if (_skipResponses != value) { _skipResponses = value; OnChanged(); } }
    }

    /// <summary>When on, every response is replaced by Modbus exception 06 (Slave Device Busy).</summary>
    public bool ReturnExceptionBusy
    {
        get => _returnExceptionBusy;
        set { if (_returnExceptionBusy != value) { _returnExceptionBusy = value; OnChanged(); } }
    }

    public bool AnyActive =>
        _responseDelayMs > 0 || _skipResponses || _returnExceptionBusy;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnChanged([CallerMemberName] string? p = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AnyActive)));
    }
}
