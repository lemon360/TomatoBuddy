namespace TomatoBuddy;
// A reward belongs to one uninterrupted full break, never a preview or an aborted break.
public sealed class RestReward
{
 private Guid? active;
 public void Begin(bool preview) => active = preview ? null : Guid.NewGuid();
 public void Abort() => active = null;
 public Guid? Complete() { var id = active; active = null; return id; }
}
