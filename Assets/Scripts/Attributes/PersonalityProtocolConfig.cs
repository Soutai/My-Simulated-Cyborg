using System.Collections.Generic;

/// <summary>
/// 具身智能物理沙盒 - 性格本能规则配置中心
/// 跟 SandboxProtocolConfig / PhysicsProtocolConfig 同一套模式：
/// 规则文本和数值阈值集中在这里，PromptManager 只管拼装 Prompt，不再手写死性格文案。
/// </summary>
public static class PersonalityProtocolConfig
{
    public struct PersonalityRule
    {
        // 仅贪吃者(GLUTTONS)用到；其余性格设为 0，模板里也不引用这个占位符
        public float lowSatietyThreshold;
        // 用 {0} 占位数值阈值的规则文本模板
        public string ruleTemplate;
    }

    private static readonly Dictionary<NpcPersonality, PersonalityRule> Registry =
        new Dictionary<NpcPersonality, PersonalityRule>()
        {
            {
                NpcPersonality.GLUTTONS,
                new PersonalityRule
                {
                    lowSatietyThreshold = 95f,
                    ruleTemplate =
                        "- 【性格本能-贪吃者(GLUTTONS)】：你对食物有着近乎病态的执念和成瘾性。在你的本能规则里，**只要你的饱食度低于 {0:F1}，你的全部思维就会被‘对食物的强烈渴望’所支配**。\n" +
                        "  - 如果当前感知区域内【存在食物】：你必须无条件以最高优先级去靠近并吃掉它，甚至可以无视身边的危险。\n" +
                        "  - 如果当前感知区域内【没有食物】：**你绝不能盲目闲逛，你当前的核心目标（goal）必须定位为‘大范围地毯式搜寻食物(Fruit/Food)’**。你执行的所有探索和长距离移动，其核心动机都必须是为了翻找食物，而不是为了寻找恶狼或进行其他任务。"
                }
            },
            {
                NpcPersonality.RISK_AVOIDANT,
                new PersonalityRule
                {
                    lowSatietyThreshold = 0f,
                    ruleTemplate =
                        "- 【性格本能-极度怕死(RISK_AVOIDANT)】：你极度缺乏安全感，对物理伤害有着天然的恐惧。在你的本能规则里，**在双手没有任何武器保护的情况下，直接暴露在敌人的攻击范围内会让你陷入极度惊恐**。你的无意识逻辑会迫使你优先远离威胁，或者不惜一切代价优先奔向最近的武器，在手无寸铁时正面迎敌完全违背你的生物本能。"
                }
            },
            {
                NpcPersonality.RISK_TAKER,
                new PersonalityRule
                {
                    lowSatietyThreshold = 0f,
                    ruleTemplate =
                        "- 【性格本能-亡命之徒(RISK_TAKER)】：你渴望风险带来的多巴胺刺激，体内充斥着极强的攻击性。在你的本能规则里，**敌人的出现不仅不是威胁，反而是最高优先级的猎物**。你会主动无视轻微的饥饿与疲劳，甚至在空手时也敢于向着危险方向正面逼近，在夺取武器或者发起冲锋前，任何安逸的行为（如进食或盲目闲逛）都会被你的狂热本能所排斥。"
                }
            },
            {
                NpcPersonality.NEUTRAL,
                new PersonalityRule
                {
                    lowSatietyThreshold = 0f,
                    ruleTemplate =
                        "- 【性格本能-理性中立】：你是一个纯粹基于物理得失计算的理性个体，没有特别的本能偏好。你可以完全根据常识、当前饱食度以及威胁的物理距离，自由且冷静地权衡生存策略。"
                }
            }
        };

    /// <summary>
    /// 获取某种性格最终展开好的规则文本（数值占位符已替换为实际配置值）。
    /// </summary>
    public static string GetRuleText(NpcPersonality personality)
    {
        if (!Registry.TryGetValue(personality, out var rule))
            rule = Registry[NpcPersonality.NEUTRAL];

        return string.Format(rule.ruleTemplate, rule.lowSatietyThreshold);
    }
}
