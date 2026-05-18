using UnityEngine;

public class PromptManager : MonoBehaviour
{
    // 🌟【精准修复版】：既保留了双手独立持物，又完美还原了底层物理推力的坐标控制参数！
    public string GeneratePhysicsEnginePrompt(
        float satiety,
        string personality,
        string currentTimeStr,
        string serializedRadarJson,
        string leftHandItem,
        string rightHandItem,
        string currentGoal = "无")
    {
        string leftItemStr = string.IsNullOrEmpty(leftHandItem) ? "空无一物" : leftHandItem;
        string rightItemStr = string.IsNullOrEmpty(rightHandItem) ? "空无一物" : rightHandItem;

        // 获取行为指导
        string behaviorGuidance = SandboxProtocolConfig.GetAllBehaviorGuidance();

        // 【新增调试】确认内容是否正常生成
        Debug.Log($"[PromptManager] behaviorGuidance 长度: {behaviorGuidance.Length} 字符");

        string prompt =
            $"# 具身智能物理沙盒任务控制台\n" +
            $"系统时间: {currentTimeStr} | 饱食度: {satiety:F1}/100 | 性格: {personality}\n\n" +
            $"🌟 左手持物: {leftItemStr}\n" +
            $"🌟 右手持物: {rightItemStr}\n" +
            $"🎯 当前主要目标: {currentGoal}\n\n" +

            "## 1. 原子物理原语（必须严格遵守坐标系与参数说明）\n" +
            "- APPLY_FORCE: 必须提供数值参数 arg_x 和 arg_z！规定：arg_x >0=向右推, <0=向左推 | arg_z >0=向前推, <0=向后推 (单次力度建议 -4.0 到 4.0 之间)\n" +
            "- GRAB / RELEASE / USE_ITEM: 执行这三个动作时，必须在指令中包含 \"hand\" 字段，指定操作是 \"Left\" 还是 \"Right\"！\n\n" +

            "## 2. 当前感知\n" + serializedRadarJson + "\n\n" +

            "## 3. 核心行为准则（双手协同协作与控制流托管）\n" +
            "- 你是大脑，负责制定持久目标（goal）和当下的即时物理原语操作。\n" +
            "- 除非出现更高优先级危险（狼极近、饱食度很低），否则要坚持当前目标。\n" +
            "- 小脑具有导航托管机制。当你下发长期目标（goal）和目标ID（goal_target_id）时，小脑会自动物理驱动肉身逼近目标。抵达后，小脑会自动释放你在 `goal_arrival_command` 中托付的临门一脚动作。\n" +
            "- 🌟【双手独立持物机制】：你拥有两只手，可以右手握着武器保持防卫，同时用空着的左手去捡起并使用其他物品（如食物），无需丢弃武器。\n" +
            "- 🌟【两段式物理进食】：你无法直接吃掉地上的水果。正确做法是：1. 发现某只手空闲，下发长期目标去 GRAB 水果，并指定该手（如 \"hand\": \"Left\"）；2. 当该水果成功进入你手中后，在下一轮决策里，对拿着水果的那只手发布 USE_ITEM 即可塞入嘴中消化。\n\n" +

            "## 4.5 多步规划能力（强烈推荐）\n" +
            "- 你拥有优秀的战略规划能力。**在大多数情况下，请每次思考时都制定2~4步连贯的行动计划**（plan_steps），让小脑可以连续执行而无需频繁请求我思考。\n" +
            "- 每个步骤都应该有明确的目标和动作。\n" +
            "- 允许合理的「储备」行为：例如先拿食物作为储备，之后再决定是否吃掉。\n\n" +
            "- 优秀示例：\n" +
            "  - 先拾取武器 → 再攻击威胁\n" +
            "  - 先抓取食物作为储备 → 后续视饱食度决定是否吃掉\n" +
            "  - 探索区域 → 寻找食物 → 进食\n" +
            "- plan_steps 数组中每一项必须包含 description、target_id、arrival_op、hand。\n" +
            "- 只有当整个计划全部执行完毕、或出现新的高优先级威胁、或饱食度过低时，才需要我重新决策。\n\n" +

            "## 4.6 持久目标能力（长期自主行为）\n" +
            "- 当你需要进行长时间行为（探索、警戒、觅食等）时，**优先考虑使用 `persistent_goal` + 多步 plan_steps 的组合**。\n" +
            "- SandboxProtocolConfig 中定义的行为策略如下：\n" +
            behaviorGuidance + "\n" +
            "- **在持久目标模式下，你必须主动规划多个有意义的移动步骤**（建议2~4步），每个步骤使用力度足够的 APPLY_FORCE（arg_x 和 arg_z 绝对值总和建议 2.5~4.0），让单次移动有明显距离（5-12米以上）。\n" +
            "- 避免输出单个小力度的 APPLY_FORCE，这会导致原地抽搐般的低效移动。\n" +
            "- 请在 monologue 中说明你的整体计划和每个步骤的理由。\n" +
            "- **重要**：短期明确动作用 `plan_steps`，长期持续行为推荐使用 `persistent_goal` + 多步 `plan_steps` 的方式。\n\n" +

            "## 5. 绝对限制 JSON 响应格式\n" +
            "必须严格返回标准的 JSON 格式块，不要包含任何 markdown 解释。在涉及到 GRAB/RELEASE/USE_ITEM 时必须包含 \"hand\" 字段。在涉及到 GRAB/RELEASE/USE_ITEM 的命令时，参数 \"hand\" 必须严格输出为 \"Left\" 或 \"Right\"，严禁留空或使用其他拼写。\n" +
            "格式如下：\n" +
            "{\n" +
            "  \"monologue\": \"思考过程（中文）...\",\n" +
            "  \"primitive_commands\": [\n" +
            "    { \"op\": \"APPLY_FORCE\", \"arg_x\": 0.5, \"arg_z\": -0.2, \"target_id\": \"\", \"hand\": \"\" }\n" +
            "  ],\n" +
            "  \"goal\": \"持久目标描述\",\n" +
            "  \"goal_target_id\": \"目标物体ID\",\n" +
            "  \"goal_arrival_command\": { \"op\": \"GRAB\", \"hand\": \"Left\", \"target_id\": \"Fruit_3\" },\n" +
            "  \"plan_steps\": [],\n" +
            "  \"persistent_goal\": \"区域警戒探索\"   // ← 长期行为时使用\n" +
            "}\n\n" +

            "扩展格式示例（推荐返回 plan_steps 或 persistent_goal）:\n" +
            "{\n" +
            "  \"monologue\": \"...\",\n" +
            "  \"primitive_commands\": [],\n" +
            "  \"goal\": \"消除威胁\",\n" +
            "  \"goal_target_id\": \"\",\n" +
            "  \"plan_steps\": [\n" +
            "    { \"description\": \"先拾取武器\", \"target_id\": \"Stick\", \"arrival_op\": \"GRAB\", \"hand\": \"Right\" },\n" +
            "    { \"description\": \"立即攻击狼\", \"target_id\": \"Wolf\", \"arrival_op\": \"USE_ITEM\", \"hand\": \"Right\" }\n" +
            "  ]\n" +
            "}\n" +

            "持久探索示例：\n" +
            "{\n" +
            "  \"monologue\": \"...\",\n" +
            "  \"primitive_commands\": [],\n" +
            "  \"goal\": \"区域警戒探索\",\n" +
            "  \"goal_target_id\": \"\",\n" +
            "  \"plan_steps\": [\n" +
            "    { \"description\": \"向东北前方未知区域移动\", \"target_id\": \"\", \"arrival_op\": \"\", \"hand\": \"\" },\n" +
            "    { \"description\": \"继续向东偏北方向探索\", \"target_id\": \"\", \"arrival_op\": \"\", \"hand\": \"\" }\n" +
            "  ],\n" +
            "  \"persistent_goal\": \"区域警戒探索\"\n" +
            "}\n";

        return prompt;
    }
}