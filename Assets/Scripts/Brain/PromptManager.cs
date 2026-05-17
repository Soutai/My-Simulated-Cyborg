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
            $"- {{\"op\": \"APPLY_FORCE\", \"arg_x\": f, \"arg_z\": f}} : 给自己的身体施加一个水平推力。f为浮点数(建议范围-4.0到4.0)。\n" +
            $"   【重要坐标系定义】\n" +
            $"   - arg_x > 0  → 向你的【右方】移动\n" +
            $"   - arg_x < 0  → 向你的【左方】移动\n" +
            $"   - arg_z > 0  → 向你的【前方】移动\n" +
            $"   - arg_z < 0  → 向你的【后方】移动\n" +
            $"   示例：如果 Stick 的 relative_z = -6.0（在你背后），你应该使用 arg_z = -4.0 或更小的负数来靠近它。\n\n" +
            $"- {{\"op\": \"GRAB\", \"target_id\": \"字符串\"}} : 尝试抓取2.5米范围内的物体。成功后物体将吸附在你的手中。\n" +
            $"- {{\"op\": \"RELEASE\"}} : 丢开手中当前抓握的物体。\n" +
            $"- {{\"op\": \"USE_ITEM\"}} : 激活使用（挥舞武器或吃脚下食物）。\n\n" +
            $"## 2. 传感器扫描到的周围物体状态 (以你为坐标原点 0,0)\n" +
            $"以下是雷达实时传回的周围物品（包含 relative_x / relative_z）：\n" +
            $"{serializedRadarJson}\n\n" +
            $"## 3. 终极行为准则与常识约束\n" +
            $"- 你必须严格遵守上面的【坐标系定义】来决定 arg_x 和 arg_z 的正负方向。\n" +
            $"- 如需去某个物体处，第一步一定是施加多步 APPLY_FORCE 让自己逐步逼近它（建议每次力不要超过4.0）。\n" +
            $"- 必须严格输出指定的 JSON 响应格式，不要包含任何多余的 Markdown 解释。\n" +
            $"正确格式示例：\n" +
            $"{{\n" +
            $"  \"monologue\": \"我看到木棍在我的背后 relative_z=-6，我需要向后移动...\",\n" +
            $"  \"primitive_commands\": [\n" +
            $"    {{\"op\": \"APPLY_FORCE\", \"arg_x\": 0.0, \"arg_z\": -4.0}},\n" +
            $"    {{\"op\": \"APPLY_FORCE\", \"arg_x\": 0.0, \"arg_z\": -3.5}},\n" +
            $"    {{\"op\": \"GRAB\", \"target_id\": \"Stick\"}}\n" +
            $"  ]\n" +
            $"}}";

        return promptText;
    }
}