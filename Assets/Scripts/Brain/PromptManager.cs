using UnityEngine;

public class PromptManager : MonoBehaviour
{
    public string GeneratePhysicsEnginePrompt(float satiety, string personality, string currentTimeStr, string serializedRadarJson, string currentGrabbedItem)
    {
        string promptText =
            $"# 具身智能物理沙盒任务控制台\n" +
            $"系统时间: {currentTimeStr} | 角色当前饱食度: {satiety.ToString("F1")}/100 (低至0则生命消亡) | 行为性格倾向: {personality}\n\n" +
            $"🌟【当前双手持物状态】: {currentGrabbedItem}\n\n" + 
            $"## 1. 你的具身物理躯壳能力 (原子级物理原语)\n" +
            $"你无法直接用语言凭空让物体消失，你只能连续输出一组原语操作序列来影响物理世界。允许使用的原子操作码（op）包括：\n" +
            $"- {{\"op\": \"APPLY_FORCE\", \"arg_x\": f, \"arg_z\": f}} : 给自己的身体施加一个持续0.5秒的水平推力分量。f为浮点数(范围-5.0到5.0)。当你想往某个方向位移时使用它。例如相对坐标有果子在(x:3.0, z:0.0)，你应该连续施加正向的 arg_x 推力使其归零。\n" +
            $"- {{\"op\": \"GRAB\", \"target_id\": \"字符串\"}} : 尝试抓取身前2米范围内的物体。成功后物体将吸附在你的手中并随你移动。\n" +
            $"- {{\"op\": \"RELEASE\"}} : 丢开手中当前抓握的物体。\n" +
            $"- {{\"op\": \"USE_ITEM\"}} : 激活使用。如果手里有武器则发动挥舞撞击物理判定；如果脚下踩着食物（距离小于0.8米）则会触发吞咽进食。\n\n" +
            $"## 2. 传感器扫描到的周围物体绝对空间状态 (以你为坐标原点 0,0)\n" +
            $"以下是雷达实时传回的周围物品机制语义和相对坐标轴偏移列表：\n" +
            $"{serializedRadarJson}\n\n" +
            $"## 3. 终极行为准则与常识约束\n" +
            $"- 你不是在做单选题，你是在编写你身体的短效神经物理冲动信号。请根据人类常识组合动作。如需去某地拿东西，第一步一定是先施加多步 APPLY_FORCE 力让自己在空间上逼近它！\n" +
            $"- 必须严格输出指定的 JSON 响应格式，不要包含任何多余的 Markdown 解释。格式必须规范如下：\n" +
            $"{{\n" +
            $"  \"monologue\": \"根据常识：当前我的饱食度很低，且附近有狼，相对坐标在XX。我决定利用侧向力拉扯躲开它，顺便逼近地上的木棍，再进行抓取...\",\n" +
            $"  \"primitive_commands\": [\n" +
            $"    {{\"op\": \"APPLY_FORCE\", \"arg_x\": -3.5, \"arg_z\": 1.0}},\n" +
            $"    {{\"op\": \"GRAB\", \"target_id\": \"Stick_01\"}},\n" +
            $"    {{\"op\": \"USE_ITEM\"}}\n" +
            $"  ]\n" +
            $"}}";

        return promptText;
    }
}