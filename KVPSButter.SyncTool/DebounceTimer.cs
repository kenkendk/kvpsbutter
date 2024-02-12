namespace KVPSButter.SyncTool;

/// <summary>
/// Debouncer for handling repeated signals with a consuming delay.
/// The timer handles repeated events by either calling <see cref="DebounceTimer.Reset"/> to repeatedly reset the timer ,
/// or by calling <see cref="DebounceTimer.Consume"/> which absorbs events within the timer period.
/// </summary>
public class DebounceTimer
{
    /// <summary>
    /// The lock guarding the write-able fields
    /// </summary>
    private readonly object m_lock = new();
    /// <summary>
    /// The callback to invoke when completed
    /// </summary>
    private readonly Action m_callback;
    /// <summary>
    /// The time to wait after receiving events
    /// </summary>
    private readonly TimeSpan m_delay;
    /// <summary>
    /// Flag signalling if the timer has already elapsed
    /// </summary>
    private bool m_completed = false;
    /// <summary>
    /// The time when the next invoke is needed
    /// </summary>
    private DateTime m_when;
    /// <summary>
    /// Unused task reference
    /// </summary>
    private Task m_task;

    /// <summary>
    /// Creates a new <see cref="DebounceTimer"/>
    /// </summary>
    /// <param name="callback">The method to call</param>
    /// <param name="delay">The delay to wait before calling</param>
    public DebounceTimer(Action callback, TimeSpan delay)
    {
        m_callback = callback ?? throw new ArgumentNullException(nameof(callback));
        if (m_delay.TotalMilliseconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(delay), delay, "Delay must be non-zero and positive");
        m_delay = delay;
        m_task = RunAsync();
    }

    /// <summary>
    /// Internal async runner task that waits until the timer has expired
    /// </summary>
    /// <returns>An awaitable task</returns>
    private async Task RunAsync()
    {
        lock (m_lock)
        {
            m_when = DateTime.UtcNow + m_delay;
            m_completed = false;
        }

        while (true)
        {
            TimeSpan waittime;
            bool completed;

            lock (m_lock)
            {
                waittime = m_when - DateTime.UtcNow;
                completed = m_completed = waittime.TotalMilliseconds < 1;
            }

            if (completed)
            {
                m_callback();
                return;
            }

            await Task.Delay(waittime);
        }
    }

    /// <summary>
    /// Resets the current timer, if running, otherwise starts a new
    /// </summary>
    public void Reset()
    {
        bool completed;
        lock (m_lock)
        {
            m_when = DateTime.UtcNow + m_delay;
            completed = m_completed;
        }

        // Restart if the timer has already fired
        if (completed)
            m_task = RunAsync();
    }

    /// <summary>
    /// Consumes an event, restarting the timer if needed
    /// </summary>
    public void Consume()
    {
        bool completed;
        lock (m_lock)
            completed = m_completed;

        // Restart if the timer has already fired
        if (completed)
            m_task = RunAsync();
    }
}
