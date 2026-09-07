using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
namespace TomatoBuddy;

public sealed class RestOverlay : IDisposable
{
 private readonly List<RestScreen> windows = new();
 private readonly DispatcherTimer pulse = new() { Interval = TimeSpan.FromMilliseconds(40) };
 private readonly System.Diagnostics.Stopwatch animation = System.Diagnostics.Stopwatch.StartNew();
 private readonly bool preview;
 private readonly string scene;
 private readonly Action emergency;
 private readonly KeyboardProc keyboardProc;
 private IntPtr hook;
 private bool disposed;
 private double progress;
 private string remaining = "";
 private bool completed;
 public RestOverlay(string scene, bool preview, Action emergency)
 {
  this.scene = scene; this.preview = preview; this.emergency = emergency;
  keyboardProc = KeyboardCallback;
  try { BuildWindows(); } catch { Dispose(); throw; }
  // Only ordinary task-switching shortcuts are intercepted. Secure attention and Task Manager remain available.
  if (!preview) { hook = SetWindowsHookEx(13, keyboardProc, GetModuleHandle(null), 0); if (hook == IntPtr.Zero) { Dispose(); throw new Win32Exception(Marshal.GetLastWin32Error(), "无法启用休息快捷键拦截"); } }
  Microsoft.Win32.SystemEvents.DisplaySettingsChanged += DisplaysChanged;
  pulse.Tick += Tick; pulse.Start();
 }
 public void Update(double value, string text) { progress = Math.Clamp(value, 0, 1); remaining = text; }
 public void Celebrate() { completed = true; progress = 1; remaining = "休息完成 · 收获 +1"; }
 internal void CheckProtectedCloseAndRender(string file)
 {
  foreach (var w in windows) { w.Close(); if (!w.IsVisible) throw new InvalidOperationException("休息窗口被普通关闭"); }
  var target = windows[0]; target.UpdateLayout();
  var bitmap = new RenderTargetBitmap((int)target.ActualWidth, (int)target.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(target);
  var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var output = System.IO.File.Create(file); encoder.Save(output);
 }
 private void BuildWindows()
 {
  foreach (var w in windows) w.Release(); windows.Clear();
  foreach (var screen in Forms.Screen.AllScreens) { var w = new RestScreen(scene, preview); windows.Add(w); w.Show(); w.Place(screen.Bounds); }
 }
 private void DisplaysChanged(object? sender, EventArgs e) => System.Windows.Application.Current.Dispatcher.BeginInvoke(() => { if (!disposed) BuildWindows(); });
 private void Tick(object? sender, EventArgs e)
 {
  foreach (var w in windows) { w.Render(progress, remaining, animation.Elapsed.TotalSeconds, completed); w.EnsureTopmost(); }
 }
 private IntPtr KeyboardCallback(int code, IntPtr message, IntPtr data)
 {
  if (code >= 0 && !disposed)
  {
   int key = Marshal.ReadInt32(data); bool down = message == (IntPtr)0x100 || message == (IntPtr)0x104;
   bool alt = (GetAsyncKeyState(0x12) & 0x8000) != 0, ctrl = (GetAsyncKeyState(0x11) & 0x8000) != 0, shift = (GetAsyncKeyState(0x10) & 0x8000) != 0;
   if (down && key == 0x7B && ctrl && alt && shift) { System.Windows.Application.Current.Dispatcher.BeginInvoke(emergency); return (IntPtr)1; }
   if (key == 0x5B || key == 0x5C || alt && (key == 9 || key == 27 || key == 0x73 || key == 32) || ctrl && !shift && key == 27) return (IntPtr)1;
  }
  return CallNextHookEx(hook, code, message, data);
 }
 public void Dispose()
 {
  if (disposed) return; disposed = true;
  pulse.Stop(); pulse.Tick -= Tick;
  Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= DisplaysChanged;
  if (hook != IntPtr.Zero) { UnhookWindowsHookEx(hook); hook = IntPtr.Zero; }
  foreach (var w in windows) w.Release(); windows.Clear();
 }
 private delegate IntPtr KeyboardProc(int code, IntPtr message, IntPtr data);
 [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int id, KeyboardProc callback, IntPtr module, uint thread);
 [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
 [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
 [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
 [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? name);
}

internal sealed class RestScreen : Window
{
 private readonly TextBlock countdown = new(), caption = new(), heading = new(), guidanceNote = new();
 private readonly System.Windows.Controls.Image actor;
 private readonly System.Windows.Controls.Image fruit;
 private readonly ScaleTransform fruitScale = new(.02, .02);
 private readonly RotateTransform sway = new();
 private readonly TranslateTransform bounce = new();
 private readonly ScaleTransform grow = new(1, 1);
 private readonly Border bar = new();
 private readonly string scene;
 private readonly bool preview;
 private bool released;
 private IntPtr handle;
 public RestScreen(string scene, bool preview)
 {
  this.scene = scene; this.preview = preview;
  Title = preview ? "番茄小伴 · 动画预览" : "番茄小伴 · 强制休息";
  WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; Topmost = true;
  Background = B("#FAF3E7"); Foreground = B("#514238"); FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei UI");
  var root = new Grid(); Content = root;
  var stack = new StackPanel { Width = 760, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = System.Windows.HorizontalAlignment.Center };
  root.Children.Add(new Viewbox { Child = stack, Stretch = Stretch.Uniform, Margin = new Thickness(60) });
  stack.Children.Add(Label(preview ? "12 秒动画预览 · 不计入收获" : "REST TIME  /  把这段时间留给自己", 15, "#9D8A71"));
  heading.Text = scene == "Dance" ? "工作先放下，跟小番茄晃一晃。" : "你在休息，小番茄在长大。"; heading.FontSize = 32; heading.FontWeight = FontWeights.Bold; heading.HorizontalAlignment = System.Windows.HorizontalAlignment.Center; heading.Margin = new Thickness(0, 18, 0, 2); stack.Children.Add(heading);
  var stage = new Grid { Width = 420, Height = 360, Margin = new Thickness(0, 10, 0, 0) };
  actor = new System.Windows.Controls.Image { Source = Asset(RestGuidance.IsGuided(scene) ? "wellness.png" : scene == "Dance" ? "dancer.png" : "plant.png"), Stretch = Stretch.Uniform, Width = 350, Height = 340, RenderTransformOrigin = new System.Windows.Point(.5, .9) };
  var transforms = new TransformGroup(); transforms.Children.Add(grow); transforms.Children.Add(sway); transforms.Children.Add(bounce); actor.RenderTransform = transforms; stage.Children.Add(actor);
  fruit = new System.Windows.Controls.Image { Source = Asset("icon.png"), Width = 83, Height = 83, HorizontalAlignment = System.Windows.HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(235, 116, 0, 0), RenderTransformOrigin = new System.Windows.Point(.5, 0), RenderTransform = fruitScale, Visibility = scene == "Plant" ? Visibility.Visible : Visibility.Collapsed };
  stage.Children.Add(fruit); stack.Children.Add(stage);
  guidanceNote.FontSize = 13; guidanceNote.Foreground = B("#9D8A71"); guidanceNote.TextAlignment = TextAlignment.Center; guidanceNote.TextWrapping = TextWrapping.Wrap; guidanceNote.Margin = new Thickness(20, 8, 20, 10); stack.Children.Add(guidanceNote);
  countdown.FontSize = 48; countdown.FontFamily = new System.Windows.Media.FontFamily("Segoe UI"); countdown.FontWeight = FontWeights.SemiBold; countdown.HorizontalAlignment = System.Windows.HorizontalAlignment.Center; stack.Children.Add(countdown);
  caption.TextWrapping = TextWrapping.Wrap; caption.TextAlignment = TextAlignment.Center; caption.MaxWidth = 700; caption.FontSize = 15; caption.Foreground = B("#9D8A71"); caption.HorizontalAlignment = System.Windows.HorizontalAlignment.Center; caption.Margin = new Thickness(0, 12, 0, 20); stack.Children.Add(caption);
  var track = new Border { Width = 300, Height = 6, Background = B("#E8DDCA"), CornerRadius = new CornerRadius(3) }; bar.Background = B("#E87760"); bar.CornerRadius = new CornerRadius(3); bar.HorizontalAlignment = System.Windows.HorizontalAlignment.Left; track.Child = bar; stack.Children.Add(track);
  stack.Children.Add(Label("离开屏幕，看看远处。倒计时结束后会自动返回。", 13, "#9D8A71"));
  root.Children.Add(new TextBlock { Text = preview ? "预览会自动结束" : "紧急退出：Ctrl + Alt + Shift + F12（本轮不结果）", Foreground = B("#B0A28F"), FontSize = 11, HorizontalAlignment = System.Windows.HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 20) });
  Opacity = 0; Loaded += (_, _) => BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(600)));
  Closing += (_, e) => { if (!released) e.Cancel = true; };
  PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape || e.Key == Key.System) e.Handled = true; };
 }
 private static TextBlock Label(string text, double size, string color) => new() { Text = text, FontSize = size, Foreground = B(color), HorizontalAlignment = System.Windows.HorizontalAlignment.Center, Margin = new Thickness(0, 17, 0, 0) };
 private static SolidColorBrush B(string text) => (SolidColorBrush)new BrushConverter().ConvertFromString(text)!;
 private static BitmapImage Asset(string name) => new(new Uri("pack://application:,,,/Assets/" + name));
 public void Place(System.Drawing.Rectangle bounds) { handle = new WindowInteropHelper(this).Handle; SetWindowPos(handle, (IntPtr)(-1), bounds.X, bounds.Y, bounds.Width, bounds.Height, 0x0040); }
 public void EnsureTopmost() { if (!released && handle != IntPtr.Zero) SetWindowPos(handle, (IntPtr)(-1), 0, 0, 0, 0, 0x0013); }
 public void Render(double p, string time, double seconds, bool complete)
 {
  countdown.Text = time; bar.Width = 300 * p;
  sway.Angle = Math.Sin(seconds * (scene == "Dance" ? 3.3 : 1.2)) * (scene == "Dance" ? 12 : 1.6);
  bounce.Y = scene == "Dance" ? -Math.Abs(Math.Sin(seconds * 3.3)) * 25 : 0;
  grow.ScaleX = scene == "Dance" ? 1 + Math.Sin(seconds * 6.6) * .035 : .78 + .22 * p; grow.ScaleY = scene == "Dance" ? 1 - Math.Sin(seconds * 6.6) * .035 : .78 + .22 * p;
  fruitScale.ScaleX = fruitScale.ScaleY = .05 + .95 * p; fruit.Opacity = Math.Clamp(p * 3, .15, 1);
  caption.Text = complete ? (preview ? "预览结束，选择你喜欢的休息伙伴。" : "好好休息，也是一种收获。今天又多了一颗果子。") : scene == "Dance" ? "摆摆手，伸伸腰，跟着小番茄放松一下。" : p < .33 ? "扎根、伸展，给自己一点生长的时间。" : p < .75 ? "果子正在慢慢长大，不着急。" : "就快成熟啦，再陪自己休息一会儿。";
  if (RestGuidance.IsGuided(scene) && !complete)
  {
   var step = RestGuidance.Get(scene, preview ? seconds * 6 : seconds);
   heading.Text = step.Title; caption.Text = step.Instruction; guidanceNote.Text = step.Note;
   sway.Angle = 0; bounce.Y = 0; grow.ScaleX = grow.ScaleY = 1;
   actor.Opacity = scene == "Eyes" && seconds > 10 ? .28 : .85;
  }
  if (complete) { guidanceNote.Text = "每一次认真休息，都值得一颗果子。"; actor.Opacity = 1; }
  if (complete) heading.Text = preview ? "这就是你的休息小伙伴。" : "休息完成，收获一颗小番茄！";
 }
 public void Release() { released = true; Close(); }
 [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
}
