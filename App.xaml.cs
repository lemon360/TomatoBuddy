using System.Threading;
using System.Windows;
namespace TomatoBuddy;
public partial class App : System.Windows.Application
{
 private Mutex? instance;
 private EventWaitHandle? activation;
 private RegisteredWaitHandle? activationWait;
 protected override void OnStartup(StartupEventArgs e)
 {
  base.OnStartup(e);
  if (e.Args.Contains("--test-rest")) { RunRestCheck(e.Args); return; }
  instance = new Mutex(true, "Local\\TomatoBuddy.Desktop", out bool first);
  if (!first) { try { EventWaitHandle.OpenExisting("Local\\TomatoBuddy.Activate").Set(); } catch (WaitHandleCannotBeOpenedException) { System.Windows.MessageBox.Show("番茄小伴正在启动，请稍后从系统托盘打开。", "番茄小伴"); } Shutdown(); return; }
  var window = new MainWindow(); MainWindow = window; window.Show();
  activation = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\TomatoBuddy.Activate");
  activationWait = ThreadPool.RegisterWaitForSingleObject(activation, (_, _) => Dispatcher.BeginInvoke(() => { window.Restore(); }), null, -1, false);
 }
 private async void RunRestCheck(string[] args)
 {
  RestOverlay? layer = null;
  var testScene = RestGuidance.Normalize(args.FirstOrDefault(a => a.StartsWith("--scene="))?.Substring(8) ?? (args.Contains("--dance") ? "Dance" : "Plant"));
  try
  {
   layer = new RestOverlay(testScene, false, () => { layer?.Dispose(); Shutdown(); });
   var watch = System.Diagnostics.Stopwatch.StartNew();
   bool rendered = false;
   while (watch.Elapsed.TotalSeconds < 8) { layer.Update(watch.Elapsed.TotalSeconds / 8, $"测试 · {8 - (int)watch.Elapsed.TotalSeconds:00} 秒"); await System.Threading.Tasks.Task.Delay(40); if (!rendered && watch.Elapsed.TotalSeconds > 5) { layer.CheckProtectedCloseAndRender(System.IO.Path.Combine(AppContext.BaseDirectory, "rest-" + testScene.ToLowerInvariant() + ".png")); rendered = true; } }
   layer.Celebrate(); await System.Threading.Tasks.Task.Delay(800);
   layer.Dispose(); layer = null;
   System.IO.File.WriteAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "rest-" + testScene.ToLowerInvariant() + "-check.txt"), "PASS: overlay created, animation updated, completion shown, windows closed and keyboard hook disposed.");
  }
  catch (Exception ex) { System.IO.File.WriteAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "rest-check-error.txt"), ex.ToString()); }
  finally { layer?.Dispose(); Shutdown(); }
 }
 protected override void OnExit(ExitEventArgs e) { activationWait?.Unregister(null); activation?.Dispose(); instance?.Dispose(); base.OnExit(e); }
}
