namespace TomatoBuddy;
public enum Phase { Focus, ShortBreak, LongBreak }
public class TimerEngine
{
 public Phase Phase { get; private set; } = Phase.Focus;
 public bool Running { get; private set; }
 public TimeSpan Remaining { get; private set; } = TimeSpan.FromMinutes(25);
 public TimeSpan Duration { get; private set; } = TimeSpan.FromMinutes(25);
 public int Completed { get; private set; }
 private DateTimeOffset deadline;
 public event Action<Phase, TimeSpan>? Finished;
 public void Select(Phase phase, TimeSpan duration) { Phase = phase; Duration = duration; Remaining = duration; Running = false; }
 public void Start(DateTimeOffset now) { if (Running) return; deadline = now + Remaining; Running = true; }
 public void Pause(DateTimeOffset now) { if (!Running) return; if (now >= deadline) { Tick(now); return; } Remaining = deadline - now; Running = false; }
 public void Reset() { Remaining = Duration; Running = false; }
 public void Tick(DateTimeOffset now)
 {
  if (!Running) return;
  Remaining = deadline - now;
  if (Remaining > TimeSpan.Zero) return;
  Remaining = TimeSpan.Zero; Running = false;
  if (Phase == Phase.Focus) Completed++;
  Finished?.Invoke(Phase, Duration);
 }
 public Phase Next(Phase finished, int interval) => finished == Phase.Focus ? (Completed % Math.Max(1, interval) == 0 ? Phase.LongBreak : Phase.ShortBreak) : Phase.Focus;
}
