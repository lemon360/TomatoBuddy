namespace TomatoBuddy;
public record GuidanceStep(string Title, string Instruction, string Note);
public static class RestGuidance
{
 public static readonly string[] Scenes = ["Plant", "Dance", "Eyes", "Move", "Calm", "None"];
 public static string Normalize(string? scene) => Scenes.Contains(scene) ? scene! : "Plant";
 public static bool IsGuided(string scene) => scene is "Eyes" or "Move" or "Calm";
 public static GuidanceStep Get(string scene, double elapsedSeconds)
 {
  // Real seconds, independent of total break length. After one gentle sequence, rest quietly.
  double t = Math.Max(0, elapsedSeconds);
  if (scene == "Eyes")
   return t < 10 ? new("先把视线移开屏幕", "选一个约 6 米外的物体，窗外或房间远端都可以。", "接下来的提示很少，不必盯着小番茄。")
    : t < 35 ? new("看看远处 · 至少 20 秒", "目光自然地落在远处，不用用力聚焦。", "远眺真实物体，不是屏幕中的风景。")
    : t < 50 ? new("轻轻眨眨眼", "自然、缓慢地眨几次眼，放松眼周。", "不揉眼、不按压眼球，也不用用力闭眼。")
    : new("继续让眼睛歇一会儿", "可以舒适地闭目片刻，或继续望向远处。", "无需看屏幕；倒计时结束会自动返回。") ;
  if (scene == "Move")
   return t < 10 ? new("先坐稳，双脚放平", "选择稳固的椅子，坐在舒服的位置，正常呼吸。", "只在舒适范围内活动；不适就停做，安静休息也算完成。")
    : t < 30 ? new("轻轻活动肩膀", "缓慢向后绕肩几次，再让肩膀自然落下。", "动作小一点，不耸紧肩膀，不大幅绕颈。")
    : t < 50 ? new("让脚踝动一动", "坐稳后，一只脚稍离地，缓慢勾脚、绷脚，再换另一侧。", "保持平衡，不憋气；有疼痛或头晕就停止动作。")
    : new("回到舒服的姿势", "双脚放稳，松开双手，接下来安静休息。", "无需重复做满整个休息时间，也不需要跟屏幕比速度。") ;
  return t < 15 ? new("放下手里的事情", "找个舒服的坐姿，让肩膀和双手放松。", "这段时间没有任务需要完成。")
   : new("按自己的节奏，自然呼吸", "不用屏息，也不必刻意深呼吸；舒服就好。", "可以闭目休息，无需跟随图像调整呼吸。") ;
 }
}
