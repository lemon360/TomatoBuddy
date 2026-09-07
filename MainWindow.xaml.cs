using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
namespace TomatoBuddy;
public partial class MainWindow : Window
{
 private readonly TimerEngine timer = new();
 private readonly LocalStore store = new();
 private readonly AppData data;
 private readonly DispatcherTimer clock = new() { Interval = TimeSpan.FromMilliseconds(250) };
 private readonly Forms.NotifyIcon tray;
 private RestOverlay? overlay;
 private readonly RestReward reward = new();
 private bool restTransition;
 private bool RestLocked => overlay != null || restTransition;
 private Window? restWindow;
 private TextBlock? restTime;
 private bool exiting;
 private string activeTask = "";
 private DateTime lastDate = DateTime.Today;
 public MainWindow()
 {
  InitializeComponent(); data = store.Load();
  Icon = new BitmapImage(new Uri("pack://application:,,,/Assets/icon.png"));
  try { HeroImage.Source = new BitmapImage(new Uri("pack://application:,,,/Assets/companion.png")); } catch { }
  TaskInput.Text = data.Settings.Task;
  timer.Select(Phase.Focus, Duration(Phase.Focus)); timer.Finished += OnFinished;
  tray = new Forms.NotifyIcon { Icon = System.Drawing.SystemIcons.Application, Text = "番茄小伴 · 准备专注", Visible = true };
  using (var iconStream = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico")).Stream) tray.Icon = new System.Drawing.Icon(iconStream);
  var heroPanel = (Grid)HeroImage.Parent; heroPanel.SizeChanged += (_, _) => heroPanel.Clip = new RectangleGeometry(new Rect(0, 0, heroPanel.ActualWidth, heroPanel.ActualHeight), 24, 24);
  var menu = new Forms.ContextMenuStrip();
  menu.Items.Add("打开番茄小伴", null, (_, _) => Dispatcher.Invoke(Restore));
  menu.Items.Add("开始 / 暂停", null, (_, _) => Dispatcher.Invoke(() => ToggleTimer(this, new RoutedEventArgs())));
  menu.Items.Add(new Forms.ToolStripSeparator());
  menu.Items.Add("退出", null, (_, _) => Dispatcher.Invoke(ExitApp)); tray.ContextMenuStrip = menu;
  tray.DoubleClick += (_, _) => Dispatcher.Invoke(Restore);
  clock.Tick += (_, _) => { timer.Tick(DateTimeOffset.UtcNow); RefreshTimer(); if (lastDate != DateTime.Today) { lastDate = DateTime.Today; RefreshStats(); } };
  clock.Start(); RefreshTimer(); RefreshStats();
  if (store.Warning != null) StatusLabel.Text = store.Warning;
 }
 private static SolidColorBrush Brush(string hex) => (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
 private TimeSpan Duration(Phase phase) => TimeSpan.FromMinutes(phase switch { Phase.Focus => data.Settings.Focus, Phase.ShortBreak => data.Settings.ShortBreak, _ => data.Settings.LongBreak });
 private string PhaseName => timer.Phase == Phase.Focus ? "专注" : timer.Phase == Phase.ShortBreak ? "短休息" : "长休息";
 private void Persist() { if (!store.Save(data)) StatusLabel.Text = store.Warning; }
 public void Restore() { if (RestLocked) return; Show(); WindowState = WindowState.Normal; Activate(); }
 protected override void OnClosing(CancelEventArgs e)
 {
  if (!exiting) { e.Cancel = true; Hide(); tray.ShowBalloonTip(1800, "番茄小伴", "已收进系统托盘，计时会继续。右键托盘图标可退出。", Forms.ToolTipIcon.Info); }
  base.OnClosing(e);
 }
 private void ExitApp() { if (RestLocked) return; data.Settings.Task = TaskInput.Text.Trim(); Persist(); exiting = true; clock.Stop(); CloseRest(); tray.Visible = false; tray.Dispose(); System.Windows.Application.Current.Shutdown(); }
 private void ShowFocus(object sender, RoutedEventArgs e) => Page(FocusPage);
 private void ShowStats(object sender, RoutedEventArgs e) { RefreshStats(); Page(StatsPage); }
 private void ShowSettings(object sender, RoutedEventArgs e)
 {
  var s = data.Settings; FocusInput.Text = s.Focus.ToString(); ShortInput.Text = s.ShortBreak.ToString(); LongInput.Text = s.LongBreak.ToString(); IntervalInput.Text = s.Interval.ToString();
  SoundInput.IsChecked = s.Sound; AutoBreakInput.IsChecked = s.AutoBreak; AutoFocusInput.IsChecked = s.AutoFocus; BreakWindowInput.IsChecked = s.BreakWindow; StrictRestInput.IsChecked = s.StrictRest; SceneInput.SelectedIndex = Array.IndexOf(RestGuidance.Scenes, RestGuidance.Normalize(s.RestScene)); SettingsMessage.Text = ""; Page(SettingsPage);
 }
 private void Page(UIElement target) { FocusPage.Visibility = StatsPage.Visibility = SettingsPage.Visibility = Visibility.Collapsed; target.Visibility = Visibility.Visible; }
 private void SelectPhase(object sender, RoutedEventArgs e)
 {
  if (RestLocked) return;
  if (timer.Running || timer.Remaining != timer.Duration)
   if (System.Windows.MessageBox.Show(this, "切换会放弃当前未完成的一轮，是否继续？", "切换计时", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
  CloseRest(); timer.Select(Enum.Parse<Phase>((string)((System.Windows.Controls.Button)sender).Tag), Duration(Enum.Parse<Phase>((string)((System.Windows.Controls.Button)sender).Tag))); RefreshTimer();
 }
 private void ToggleTimer(object sender, RoutedEventArgs e)
 {
  if (RestLocked) return;
  if (timer.Running) timer.Pause(DateTimeOffset.UtcNow);
  else { if (timer.Phase == Phase.Focus && timer.Remaining == timer.Duration) activeTask = TaskInput.Text.Trim(); timer.Start(DateTimeOffset.UtcNow); if (timer.Phase != Phase.Focus && data.Settings.StrictRest) OpenStrictRest(); }
  RefreshTimer();
 }
 private void ResetTimer(object sender, RoutedEventArgs e)
 {
  if (RestLocked) return;
  if (timer.Remaining != timer.Duration && System.Windows.MessageBox.Show(this, "重置会放弃本轮进度，是否继续？", "重置计时", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
  CloseRest(); timer.Select(timer.Phase, Duration(timer.Phase)); RefreshTimer();
 }
 private void SaveTask(object sender, RoutedEventArgs e) { data.Settings.Task = TaskInput.Text.Trim(); Persist(); }
 private async void OnFinished(Phase phase, TimeSpan duration)
 {
  if (phase == Phase.Focus) { data.Sessions.Add(new Session(DateTimeOffset.Now, duration.TotalMinutes, string.IsNullOrWhiteSpace(activeTask) ? "自由专注" : activeTask)); data.Settings.Task = TaskInput.Text.Trim(); Persist(); RefreshStats(); }
  if (phase != Phase.Focus && overlay != null)
  {
   restTransition = true;
   var id = reward.Complete();
   if (id.HasValue) { data.Harvests.Add(new RestHarvest(id.Value, DateTimeOffset.Now, duration.TotalMinutes, data.Settings.RestScene)); Persist(); RefreshStats(); }
   overlay.Celebrate();
   await System.Threading.Tasks.Task.Delay(1800);
   overlay.Dispose(); overlay = null; restTransition = false;
  }
  if (data.Settings.Sound) System.Media.SystemSounds.Asterisk.Play();
  CloseRest(); var next = timer.Next(phase, data.Settings.Interval); timer.Select(next, Duration(next));
  tray.ShowBalloonTip(4000, phase == Phase.Focus ? "又收获一颗番茄！" : "休息结束，欢迎回来", phase == Phase.Focus ? "看看远处，眨眨眼，让眼睛放松一下。" : "准备好了，就开始下一段专注吧。", Forms.ToolTipIcon.Info);
  if (phase == Phase.Focus && (data.Settings.AutoBreak || data.Settings.StrictRest) || phase != Phase.Focus && data.Settings.AutoFocus) { activeTask = TaskInput.Text.Trim(); timer.Start(DateTimeOffset.UtcNow); }
  if (phase == Phase.Focus && data.Settings.StrictRest) OpenStrictRest();
  else if (phase == Phase.Focus && data.Settings.BreakWindow) OpenRest();
 }
 private void RefreshTimer()
 {
  var seconds = Math.Max(0, (int)Math.Ceiling(timer.Remaining.TotalSeconds)); var text = $"{seconds / 60:00}:{seconds % 60:00}";
  if (!restTransition) overlay?.Update(1 - timer.Remaining.TotalSeconds / timer.Duration.TotalSeconds, text);
  TimeLabel.Text = text; restTime?.SetCurrentValue(TextBlock.TextProperty, text + (timer.Running ? "" : " · 已暂停"));
  Title = timer.Running ? $"{text} · {PhaseName} — 番茄小伴" : "番茄小伴 · 把时间留给喜欢的事";
  tray.Text = $"番茄小伴 · {PhaseName} {text}";
  PhaseLabel.Text = timer.Running ? (timer.Phase == Phase.Focus ? "专心当下，慢慢发光" : "看看远方，松一口气") : "准备好了，随时开始";
  RoundLabel.Text = $"第 {timer.Completed % data.Settings.Interval + 1} / {data.Settings.Interval} 颗番茄";
  StartButton.Content = timer.Running ? "Ⅱ  暂停一下" : timer.Remaining != timer.Duration ? "▶  继续" + PhaseName : "▶  开始" + PhaseName;
  TaskInput.IsEnabled = !timer.Running;
  foreach (var b in new[] { FocusTab, ShortTab, LongTab }) { bool selected = (string)b.Tag == timer.Phase.ToString(); b.Background = Brush(selected ? "#FBE4D9" : "#F5F0E9"); b.Foreground = Brush(selected ? "#BD5C46" : "#9A8A7D"); }
  double progress = Math.Clamp(1 - timer.Remaining.TotalSeconds / timer.Duration.TotalSeconds, 0, .99999);
  if (progress <= 0) ProgressArc.Data = null;
  else { double a = progress * 2 * Math.PI; var figure = new PathFigure { StartPoint = new System.Windows.Point(139, 5) }; figure.Segments.Add(new ArcSegment(new System.Windows.Point(139 + 134 * Math.Sin(a), 139 - 134 * Math.Cos(a)), new System.Windows.Size(134, 134), 0, progress > .5, SweepDirection.Clockwise, true)); ProgressArc.Data = new PathGeometry(new[] { figure }); }
  CompanionText.Text = timer.Running ? timer.Phase == Phase.Focus ? "✦  你认真努力的样子，闪闪发光" : "✦  放下屏幕，小番茄陪你歇一会儿" : "✦  准备好啦，陪你种下下一颗番茄";
 }
 private void RefreshStats()
 {
  var today = data.Sessions.Where(s => s.FinishedAt.LocalDateTime.Date == DateTime.Today).ToList();
  TodayCount.Text = today.Count.ToString(); TodayMinutes.Text = today.Sum(s => s.Minutes).ToString("0"); DateLabel.Text = DateTime.Now.ToString("MM 月 dd 日");
  var dates = data.Sessions.Select(s => s.FinishedAt.LocalDateTime.Date).ToHashSet(); var day = dates.Contains(DateTime.Today) ? DateTime.Today : DateTime.Today.AddDays(-1); int streak = 0; while (dates.Contains(day)) { streak++; day = day.AddDays(-1); } StreakLabel.Text = streak.ToString();
  HarvestLabel.Text = $"今日收获 {data.Harvests.Count(h => h.FinishedAt.LocalDateTime.Date == DateTime.Today)} 颗果子 · 累计 {data.Harvests.Count} 颗 · 完整强制休息一次结一颗";
  StatsSummary.Text = $"累计完成 {data.Sessions.Count} 颗番茄 · 共专注 {data.Sessions.Sum(s => s.Minutes):0} 分钟 · 最近 7 天";
  WeekBars.Children.Clear(); var totals = Enumerable.Range(0, 7).Select(i => data.Sessions.Where(s => s.FinishedAt.LocalDateTime.Date == DateTime.Today.AddDays(i - 6)).Sum(s => s.Minutes)).ToArray(); double max = Math.Max(1, totals.Max());
  for (int i = 0; i < 7; i++) { var panel = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(8, 0, 8, 0) }; panel.Children.Add(new TextBlock { Text = $"{totals[i]:0} 分", HorizontalAlignment = System.Windows.HorizontalAlignment.Center, Foreground = Brush("#9A8A7D"), FontSize = 11, Margin = new Thickness(0, 0, 0, 6) }); panel.Children.Add(new Border { Height = Math.Max(5, totals[i] / max * 105), CornerRadius = new CornerRadius(7), Background = Brush(i == 6 ? "#E87760" : "#EEDACE") }); panel.Children.Add(new TextBlock { Text = i == 6 ? "今天" : DateTime.Today.AddDays(i - 6).ToString("M/d"), HorizontalAlignment = System.Windows.HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 0), FontSize = 12 }); WeekBars.Children.Add(panel); }
  HarvestList.Children.Clear();
  foreach (var group in data.Harvests.GroupBy(h => h.FinishedAt.LocalDateTime.Date).OrderByDescending(g => g.Key).Take(30))
   HarvestList.Children.Add(new TextBlock { Text = $"{group.Key:yyyy/MM/dd}    收获 {group.Count()} 颗果子    ·    休息 {group.Sum(h => h.Minutes):0} 分钟", Margin = new Thickness(0, 7, 0, 7), Foreground = Brush("#87936F") });
  if (data.Harvests.Count == 0) HarvestList.Children.Add(new TextBlock { Text = "完成一次强制休息，你的第一颗果子就会出现在这里。", Margin = new Thickness(0, 10, 0, 16), Foreground = Brush("#A8998A") });
  SessionList.Children.Clear();
  if (data.Sessions.Count == 0) SessionList.Children.Add(new TextBlock { Text = "这里还空着。完成第一轮专注，让小番茄记住你的努力。", Foreground = Brush("#A8998A"), Margin = new Thickness(0, 20, 0, 0) });
  foreach (var s in data.Sessions.AsEnumerable().Reverse().Take(100)) { var row = new DockPanel { Margin = new Thickness(0, 9, 0, 9) }; var time = new TextBlock { Text = $"{s.FinishedAt.LocalDateTime:MM/dd HH:mm}   ·   {s.Minutes:0} 分钟", Foreground = Brush("#A8998A"), FontSize = 12 }; DockPanel.SetDock(time, Dock.Right); row.Children.Add(time); row.Children.Add(new TextBlock { Text = "●   " + s.Task, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 0, 15, 0) }); SessionList.Children.Add(row); }
 }
 private void SaveSettings(object sender, RoutedEventArgs e)
 {
  if (RestLocked) return;
  if (!int.TryParse(FocusInput.Text, out int f) || f < 1 || f > 180 || !int.TryParse(ShortInput.Text, out int s) || s < 1 || s > 60 || !int.TryParse(LongInput.Text, out int l) || l < 1 || l > 120 || !int.TryParse(IntervalInput.Text, out int i) || i < 2 || i > 12) { SettingsMessage.Text = "请输入整数：专注 1–180，短休息 1–60，长休息 1–120，轮数 2–12。"; return; }
  var p = data.Settings; p.Focus = f; p.ShortBreak = s; p.LongBreak = l; p.Interval = i; p.Sound = SoundInput.IsChecked == true; p.AutoBreak = AutoBreakInput.IsChecked == true; p.AutoFocus = AutoFocusInput.IsChecked == true; p.BreakWindow = BreakWindowInput.IsChecked == true; p.StrictRest = StrictRestInput.IsChecked == true; p.RestScene = RestGuidance.Scenes[Math.Clamp(SceneInput.SelectedIndex, 0, RestGuidance.Scenes.Length - 1)];
  if (!timer.Running && timer.Remaining == timer.Duration) timer.Select(timer.Phase, Duration(timer.Phase));
  SettingsMessage.Text = store.Save(data) ? "已保存。按照自己的节奏，慢慢来。" : store.Warning; RefreshTimer();
 }
 private void OpenStrictRest()
 {
  if (overlay != null) return;
  try { reward.Begin(false); overlay = new RestOverlay(data.Settings.RestScene, false, EmergencyRest); }
  catch (Exception ex) { reward.Abort(); timer.Pause(DateTimeOffset.UtcNow); StatusLabel.Text = "强制休息启动失败：" + ex.Message; Restore(); }
 }
 private void EmergencyRest()
 {
  if (restTransition) return;
  reward.Abort(); overlay?.Dispose(); overlay = null;
  timer.Select(Phase.Focus, Duration(Phase.Focus));
  StatusLabel.Text = "已紧急结束休息，本轮未收获果子。";
  Restore(); RefreshTimer();
 }
 private async void PreviewRest(object sender, RoutedEventArgs e)
 {
  if (RestLocked || timer.Running) { SettingsMessage.Text = "请先暂停当前计时，再预览动画。"; return; }
  restTransition = true;
  RestOverlay? previewWindow = null;
  try
  {
   previewWindow = new RestOverlay(RestGuidance.Scenes[Math.Clamp(SceneInput.SelectedIndex, 0, RestGuidance.Scenes.Length - 1)], true, () => {});
   var watch = System.Diagnostics.Stopwatch.StartNew();
   while (watch.Elapsed.TotalSeconds < 12) { previewWindow.Update(watch.Elapsed.TotalSeconds / 12, $"预览 · {12 - (int)watch.Elapsed.TotalSeconds:00} 秒"); await System.Threading.Tasks.Task.Delay(40); }
  }
  catch (Exception ex) { SettingsMessage.Text = "预览失败：" + ex.Message; }
  finally { previewWindow?.Dispose(); restTransition = false; }
 }
 private void OpenRest()
 {
  restWindow = new Window { Title = "番茄小伴 · 休息一下", Width = 490, Height = 490, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterScreen, Background = Brush("#FBF7F1"), Topmost = true, FontFamily = FontFamily };
  var stack = new StackPanel { Margin = new Thickness(30) }; if (HeroImage.Source != null) stack.Children.Add(new System.Windows.Controls.Image { Source = HeroImage.Source, Height = 185, Stretch = Stretch.UniformToFill });
  stack.Children.Add(new TextBlock { Text = "辛苦啦，让眼睛透透气。", FontSize = 23, FontWeight = FontWeights.Bold, HorizontalAlignment = System.Windows.HorizontalAlignment.Center, Margin = new Thickness(0, 18, 0, 10) });
  stack.Children.Add(new TextBlock { Text = "望望远处，眨眨眼，再轻轻伸个懒腰。", HorizontalAlignment = System.Windows.HorizontalAlignment.Center, Foreground = Brush("#9A8A7D") });
  restTime = new TextBlock { FontSize = 30, HorizontalAlignment = System.Windows.HorizontalAlignment.Center, Margin = new Thickness(0, 13, 0, 13) }; stack.Children.Add(restTime);
  var close = new System.Windows.Controls.Button { Content = timer.Running ? "收起提醒，继续休息" : "开始休息" }; close.Click += (_, _) => { if (!timer.Running) timer.Start(DateTimeOffset.UtcNow); CloseRest(); }; stack.Children.Add(close);
  restWindow.Content = stack; restWindow.Closed += (_, _) => { restWindow = null; restTime = null; }; RefreshTimer(); restWindow.Show();
 }
 private void CloseRest() { restWindow?.Close(); restWindow = null; restTime = null; }
 private void Export(object sender, RoutedEventArgs e)
 {
  var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "CSV 文件|*.csv", FileName = "番茄小伴-专注记录.csv" }; if (dialog.ShowDialog(this) != true) return;
  static string Csv(string s) { if (s.TrimStart().StartsWith('=') || s.TrimStart().StartsWith('+') || s.TrimStart().StartsWith('-') || s.TrimStart().StartsWith('@')) s = "'" + s; return "\"" + s.Replace("\"", "\"\"") + "\""; }
  try { File.WriteAllText(dialog.FileName, "完成时间,专注分钟,任务\r\n" + string.Join("\r\n", data.Sessions.Select(s => $"{s.FinishedAt:O},{s.Minutes.ToString(System.Globalization.CultureInfo.InvariantCulture)},{Csv(s.Task)}")), new UTF8Encoding(true)); StatusLabel.Text = "专注记录已导出。"; }
  catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { System.Windows.MessageBox.Show(this, "导出失败，请检查文件是否被占用及保存位置权限。", "导出记录"); }
 }
}
