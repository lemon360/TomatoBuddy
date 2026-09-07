using TomatoBuddy;
int checks = 0;
void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); checks++; }
var now = DateTimeOffset.UtcNow;
var t = new TimerEngine(); int events = 0; t.Finished += (_, _) => events++;
t.Start(now); t.Tick(now.AddSeconds(20)); Check(t.Remaining == TimeSpan.FromMinutes(25) - TimeSpan.FromSeconds(20), "deadline countdown");
t.Pause(now.AddSeconds(30)); var remaining = t.Remaining; t.Tick(now.AddHours(1)); Check(t.Remaining == remaining, "pause freezes countdown");
t.Start(now.AddHours(1)); t.Tick(now.AddHours(1).Add(remaining)); Check(events == 1 && t.Completed == 1 && !t.Running, "completion exactly once");
t.Tick(now.AddDays(1)); Check(events == 1, "no duplicate completion after sleep");
Check(t.Next(Phase.Focus, 4) == Phase.ShortBreak, "short break follows focus");
for(int i = 0; i < 3; i++) { t.Select(Phase.Focus, TimeSpan.FromSeconds(1)); t.Start(now); t.Tick(now.AddSeconds(2)); }
Check(t.Next(Phase.Focus, 4) == Phase.LongBreak, "fourth focus gives long break");
t.Select(Phase.ShortBreak, TimeSpan.FromSeconds(1)); t.Start(now); t.Tick(now.AddDays(1)); Check(t.Completed == 4, "break does not count as focus");
Check(t.Next(Phase.ShortBreak, 4) == Phase.Focus, "break returns to focus");
t.Select(Phase.Focus, TimeSpan.FromMinutes(10)); t.Start(now); t.Tick(now.AddMinutes(1)); t.Reset(); Check(!t.Running && t.Remaining == TimeSpan.FromMinutes(10), "reset restores duration");
t.Select(Phase.LongBreak, TimeSpan.FromMinutes(15)); Check(!t.Running && t.Remaining == TimeSpan.FromMinutes(15), "select phase updates duration");
Console.WriteLine($"{checks} checks passed");

var reward = new RestReward();
reward.Begin(false); Check(reward.Complete().HasValue, "full break yields one fruit");
Check(reward.Complete() == null, "duplicate break completion yields nothing");
reward.Begin(true); Check(reward.Complete() == null, "preview yields no fruit");
reward.Begin(false); reward.Abort(); Check(reward.Complete() == null, "emergency abort yields no fruit");
var migrated = System.Text.Json.JsonSerializer.Deserialize<AppData>("{\"Settings\":{\"Focus\":25},\"Sessions\":[]}")!;
Check(migrated.Settings.StrictRest && migrated.Harvests.Count == 0, "old data gets strict mode and empty harvest collection");
var chained = new TimerEngine(); chained.Select(Phase.Focus, TimeSpan.FromSeconds(1));
chained.Finished += (_, _) => { chained.Select(Phase.ShortBreak, TimeSpan.FromMinutes(5)); chained.Start(now.AddSeconds(1)); };
chained.Start(now); chained.Pause(now.AddSeconds(1));
Check(chained.Running && chained.Phase == Phase.ShortBreak, "pause at focus deadline cannot pause automatically started mandatory rest");
Console.WriteLine($"Total: {checks} checks passed");

Check(RestGuidance.Normalize("Eyes") == "Eyes" && RestGuidance.Normalize("Move") == "Move" && RestGuidance.Normalize("Calm") == "Calm", "new scene IDs persist");
Check(RestGuidance.Normalize("unknown") == "Plant", "invalid scene falls back");
Check(RestGuidance.Get("Eyes", 10).Title == RestGuidance.Get("Eyes", 34).Title, "distance viewing lasts at least 20 real seconds");
Check(RestGuidance.Get("Eyes", 35).Title.Contains("眨"), "eye sequence advances to gentle blinking");
Check(RestGuidance.Get("Move", 50).Title == RestGuidance.Get("Move", 900).Title, "movement ends in quiet rest without endless exercise");
Check(RestGuidance.Get("Calm", 20).Instruction.Contains("不用屏息"), "calm does not require breath holding");
Console.WriteLine($"Total: {checks} checks passed");

Check(new Preferences().AutoFocus && new Preferences().AutoBreak, "new installs continuously cycle by default");
var oldSettings = new AppData { Settings = new Preferences { AutoFocus = false, AutoBreak = false } };
LocalStore.UpgradeContinuity(oldSettings);
Check(oldSettings.Settings.AutoFocus && oldSettings.Settings.AutoBreak, "existing installations migrate to continuous defaults");
oldSettings.Settings.AutoFocus = false; LocalStore.UpgradeContinuity(oldSettings);
Check(!oldSettings.Settings.AutoFocus, "subsequent user opt-out is preserved");
Check(RestGuidance.Normalize("None") == "None" && !RestGuidance.IsGuided("None"), "no transition is a persistent valid scene");
var continuous = new TimerEngine(); var preferences = new Preferences(); var tickTime = now; int cycles = 0;
continuous.Select(Phase.Focus, TimeSpan.FromSeconds(1));
continuous.Finished += (phase, duration) => { cycles++; var next = continuous.Next(phase, 4); continuous.Select(next, TimeSpan.FromSeconds(1)); if (preferences.AutoStartAfter(phase)) continuous.Start(tickTime); };
continuous.Start(tickTime);
for(int i = 0; i < 8; i++) { tickTime = tickTime.AddSeconds(1); continuous.Tick(tickTime); }
Check(cycles == 8 && continuous.Running && continuous.Phase == Phase.Focus && continuous.Completed == 4, "four focus-break rounds run continuously including long break");
Console.WriteLine($"Total: {checks} checks passed");
