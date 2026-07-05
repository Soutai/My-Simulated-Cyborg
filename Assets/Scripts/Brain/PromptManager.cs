using UnityEngine;

public class PromptManager : MonoBehaviour
{
    /// <summary>
    /// 动态生成发送给大脑（LLM）的具身智能语义Prompt
    /// </summary>
    public string GeneratePhysicsEnginePrompt(
        float satiety,
        NpcPersonality personality,
        string currentTimeStr,
        string serializedRadarJson,
        string leftHandItem,
        string rightHandItem,
        string currentGoal = "无")
    {
        string leftItemStr = string.IsNullOrEmpty(leftHandItem) ? "空无一物" : leftHandItem;
        string rightItemStr = string.IsNullOrEmpty(rightHandItem) ? "空无一物" : rightHandItem;

        // 🌟 核心机制语义化：根据性格动态注入底层的“因果物理规则”与“本能权重”
        string personalityRules = GetPersonalityRules(personality);

        string prompt =
            $"# 具身智能物理沙盒任务控制台\n" +
            $"系统时间: {currentTimeStr} | 饱食度: {satiety:F1}/100 | 性格本能: {personality}\n\n" +
            $"🌟 左手持物: {leftItemStr}\n" +
            $"🌟 右手持物: {rightItemStr}\n" +
            // 🌟【核心修改】：强行让大模型意识到它已经安排过这个任务了！
            $"🎯 身体当前正在推进执行的宏观战略 (Goal): {currentGoal}\n" +
            $"⚠️ 指挥官核心因果链原则：\n" +
            $"- 当你看到当前主要目标不是‘无’时，说明你的身体正在完美执行你上一轮下达的命令。\n" +
            $"- 你千万不要由于失忆，再次下达相同意图的初始动作（比如身体已经拿着木棍走向狼，你不要再下达‘去捡木棍’）！\n" +
            $"- 你应当保持时空连续性，在返回的 `plan_steps` 中，无缝规划在此战略【完成之后】或者【中途需要突变调整】时的下一步任务序列！\n\n" +

            "## 1. 原子物理原语（必须严格遵守）\n" +
            "- APPLY_FORCE: 必须提供 arg_x 和 arg_z！（arg_x >0=右, <0=左 | arg_z >0=前, <0=后，建议力度 2.0~4.0）\n" +
            "- APPROACH: 靠近某个物体（推荐使用，AI只需给出 target_id）\n" +
            "- MOVE_DIRECTION: 朝指定方向移动一段距离（需 arg_x、arg_z、strength）\n" +
            "- GRAB / RELEASE / USE_ITEM: 必须包含 \"hand\" 字段（\"Left\" 或 \"Right\"）！\n" +
            "- GRAB: 除了 hand，还必须提供 \"target_id\"，明确指出要抓取的具体物体，不要因为觉得目标\"很明显\"就省略！\n\n" +

            "## 2. 当前感知\n" +
            $"{serializedRadarJson}\n\n" +

            "## 3. 核心行为与性格本能准则\n" +
            "- 你是大脑，只负责输出原子原语和行动计划。小脑只会忠实按顺序执行你下发的原子原语。\n" +
            "- 移动必须通过物理方式实现（APPROACH / MOVE_DIRECTION / APPLY_FORCE）。\n" +
            "- 抓取和使用物品依赖实际接近（由执行器处理）。\n" +
            "- 🌟双手独立：可以右手持武器，左手做其他事。\n" +
            "- 🌟两段式进食：必须先 GRAB 食物到手中，再 USE_ITEM 吃掉。\n" +
            personalityRules + "\n\n" + // 🌟 注入非干预性的性格底层约束规则

            "## 4. 多步规划能力（核心机制）\n" +
            "- 你拥有优秀的战略规划能力。**请每次思考时都制定至少包含2个步骤的连贯行动计划**（plan_steps），让小脑可以连续执行。\n" +
            "- 你需要根据你的常识判断需要多少个步骤，把一个大目标分解成需要的步数。\n" +
            "- **【重要】如果当前处于没有明确目的的任务（如探路、巡逻、全图搜寻目标），你必须进行长程大范围规划，单次行动计划【必须包含 15 到 20 个连续移动步骤】，以网格状、螺旋状或Z字形彻底探查未探索区域，严禁只规划2-3步！**\n" +
            "- 探索/巡逻任务优秀示例（长程连续移动）：\n" +
            "  - MOVE_DIRECTION(X=0, Z=4)前方探索 → MOVE_DIRECTION(X=4, Z=4)右前探索 → MOVE_DIRECTION(X=4, Z=0)右方折返 → MOVE_DIRECTION(X=4, Z=-4)右后探查 → MOVE_DIRECTION(X=0, Z=-4)后方扫尾\n" +
            "- 推荐优先使用 APPROACH（靠近物体）和 MOVE_DIRECTION（方向移动）。\n" +
            "- 对于移动步骤，请明确给出参数，让单次移动有明显距离。\n" +
            "- 优秀示例：\n" +
            "  - APPROACH Stick → GRAB（Right） → APPROACH Wolf → USE_ITEM（Right）\n" +
            "  - MOVE_DIRECTION（向前） → APPROACH Fruit → GRAB（Left） → USE_ITEM（Left）\n" +
            "  - APPLY_FORCE 微调位置 → GRAB\n" +
            "- plan_steps 中每一项必须包含 description、arrival_op、hand（target_id / arg_x / arg_z / strength 根据 op 类型提供）。\n\n" +
            "- 【战略惊醒锚点】：如果你当前处于大范围搜寻、巡逻、探路等没有明确终点的长程移动任务（单次规划15-20步），" +
            "你必须在 JSON 的 `interrupt_anchor_type` 字段中，填入你本次搜寻的核心欲望目标类型（可选值：'Food'、'Enemy'、'Weapon'）。一旦身体在走路时雷达首次扫到该类型，" +
            "小脑将立刻掐断走格子计划交还控制权。如果是去抓取眼前特定物品等短程确定性连招，请务必将其设为 'None'。\n" +

            "## 5. 绝对限制 JSON 响应格式\n" +
            "必须严格返回标准的 JSON 格式块，不要包含任何 markdown 解释。\n" +
            "格式如下：\n" +
            "{\n" +
            "  \"monologue\": \"思考过程（中文）...\",\n" +
            "  \"primitive_commands\": [],\n" +
            "  \"goal\": \"短期目标描述\",\n" +
            "  \"interrupt_anchor_type\": \"Food\",\n" + 
            "  \"plan_steps\": [\n" +
            "    { \"description\": \"靠近木棍\", \"arrival_op\": \"APPROACH\", \"target_id\": \"Stick\", \"strength\": 1.0 },\n" +
            "    { \"description\": \"用右手抓取木棍\", \"arrival_op\": \"GRAB\", \"hand\": \"Right\", \"target_id\": \"Stick\" },\n" +
            "    { \"description\": \"向狼的方向移动\", \"arrival_op\": \"MOVE_DIRECTION\", \"arg_x\": -2.5, \"arg_z\": 1.0, \"strength\": 1.2 }\n" +
            "  ]\n" +
            "}";

        return prompt;
    }

    /// <summary>
    /// 🧠 机制语义化映射：不包含 If-Else 决策，只向大模型输入个性的内在物理法则和欲望代价限制。
    /// </summary>
    private string GetPersonalityRules(NpcPersonality personality)
    {
        switch (personality)
        {
            case NpcPersonality.GLUTTONS:
                return "- 【性格本能-贪吃者(GLUTTONS)】：你对食物有着近乎病态的执念和成瘾性。在你的本能规则里，**只要你的饱食度低于 95.0，你的全部思维就会被‘对食物的强烈渴望’所支配**。\n" +
                       "  - 如果当前感知区域内【存在食物】：你必须无条件以最高优先级去靠近并吃掉它，甚至可以无视身边的危险。\n" +
                       "  - 如果当前感知区域内【没有食物】：**你绝不能盲目闲逛，你当前的核心目标（goal）必须定位为‘大范围地毯式搜寻食物(Fruit/Food)’**。你执行的所有探索和长距离移动，其核心动机都必须是为了翻找食物，而不是为了寻找恶狼或进行其他任务。";

            case NpcPersonality.RISK_AVOIDANT:
                return "- 【性格本能-极度怕死(RISK_AVOIDANT)】：你极度缺乏安全感，对物理伤害有着天然的恐惧。在你的本能规则里，**在双手没有任何武器（Weapon/Stick）保护的情况下，直接暴露在敌人（Enemy/Wolf）的攻击范围内会让你陷入极度惊恐**。你的无意识逻辑会迫使你优先远离威胁，或者不惜一切代价优先奔向最近的武器，在手无寸铁时正面迎敌完全违背你的生物本能。";

            case NpcPersonality.RISK_TAKER:
                return "- 【性格本能-亡命之徒(RISK_TAKER)】：你渴望风险带来的多巴胺刺激，体内充斥着极强的攻击性。在你的本能规则里，**敌人的出现不仅不是威胁，反而是最高优先级的猎物**。你会主动无视轻微的饥饿与疲劳，甚至在空手时也敢于向着危险方向正面逼近，在夺取武器或者发起冲锋前，任何安逸的行为（如进食或盲目闲逛）都会被你的狂热本能所排斥。";

            case NpcPersonality.NEUTRAL:
            default:
                return "- 【性格本能-理性中立】：你是一个纯粹基于物理得失计算的理性个体，没有特别的本能偏好。你可以完全根据常识、当前饱食度以及威胁的物理距离，自由且冷静地权衡生存策略。";
        }
    }
}