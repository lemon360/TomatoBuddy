using System.IO;
using System.Text.Json;
namespace TomatoBuddy;
public class Preferences
{
 public int Focus { get; set; } = 25;
 public int ShortBreak { get; set; } = 5;
 public int LongBreak { get; set; } = 15;
 public int Interval { get; set; } = 4;
 public bool Sound { get; set; } = true;
 public bool AutoBreak { get; set; } = true;
 public bool AutoFocus { get; set; }
 public bool BreakWindow { get; set; } = true;
 public bool StrictRest { get; set; } = true;
 public string RestScene { get; set; } = "Plant";
 public string Task { get; set; } = "";
}
public record Session(DateTimeOffset FinishedAt, double Minutes, string Task);
public record RestHarvest(Guid Id, DateTimeOffset FinishedAt, double Minutes, string Scene);
public class AppData
{
 public Preferences Settings { get; set; } = new();
 public List<Session> Sessions { get; set; } = new();
 public List<RestHarvest> Harvests { get; set; } = new();
}
public class LocalStore
{
 public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TomatoBuddy");
 public string? Warning { get; private set; }
 public AppData Load()
 {
  var path = Path.Combine(Folder, "data.json");
  if (!File.Exists(path)) return new();
  try
  {
   var d = JsonSerializer.Deserialize<AppData>(File.ReadAllText(path)) ?? throw new JsonException();
   d.Settings ??= new(); d.Sessions ??= new(); d.Harvests ??= new();
   d.Settings.RestScene = RestGuidance.Normalize(d.Settings.RestScene);
   d.Settings.Focus = Math.Clamp(d.Settings.Focus, 1, 180); d.Settings.ShortBreak = Math.Clamp(d.Settings.ShortBreak, 1, 60);
   d.Settings.LongBreak = Math.Clamp(d.Settings.LongBreak, 1, 120); d.Settings.Interval = Math.Clamp(d.Settings.Interval, 2, 12);
   return d;
  }
  catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
  {
   Warning = "本地记录读取失败，已使用默认设置。原文件未覆盖。";
   try { File.Copy(path, path + ".recovery-" + DateTime.Now.ToString("yyyyMMddHHmmss")); } catch { }
   return new();
  }
 }
 public bool Save(AppData data)
 {
  try { Directory.CreateDirectory(Folder); var path = Path.Combine(Folder, "data.json"); File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true })); File.Move(path + ".tmp", path, true); return true; }
  catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Warning = "保存失败，请检查本地文件夹权限或磁盘空间。"; return false; }
 }
}
