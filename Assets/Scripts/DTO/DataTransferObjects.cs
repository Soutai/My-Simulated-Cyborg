using System;
using System.Collections.Generic;
using UnityEngine;

namespace EmbodiedAI.DTO
{
    [Serializable]
    public class PrimitiveCommand
    {
        public string op;                    // "APPROACH", "MOVE_DIRECTION", "APPLY_FORCE", "GRAB", "RELEASE", "USE_ITEM"
        public string hand = "";             // Left / Right （保留你原来的双手机制）
        public string target_id = "";
        public float arg_x = 0f;
        public float arg_z = 0f;
        public float strength = 1f;          // 用于 MOVE_DIRECTION / APPROACH 的强度 (0.5~2.0)
    }

    [Serializable]
    public class PlanStep
    {
        public string description;
        public string arrival_op;            // 同 op
        public string hand = "";             // 保留双手支持
        public string target_id = "";
        public float arg_x = 0f;
        public float arg_z = 0f;
        public float strength = 1f;
    }

    [Serializable]
    public class AIPhysicsDecision
    {
        public string monologue;
        public string goal = "无"; // 大脑当前认定的宏观战略目标（如：打狼、觅食）
        public List<PlanStep> plan_steps = new List<PlanStep>(); // 为该目标规划的多步原子原子动作序列
        public string interrupt_anchor_type = "None"; // 可选值: "Food", "Enemy", "Weapon", "None"
    }

    // ====================== Gemini API 请求/响应结构 ======================
    [Serializable]
    public class GeminiRequest
    {
        public RequestContent[] contents;

        [Serializable]
        public class RequestContent
        {
            public RequestPart[] parts;
        }

        [Serializable]
        public class RequestPart
        {
            public string text;
        }
    }

    [Serializable]
    public class GeminiResponse
    {
        public Candidate[] candidates;

        [Serializable]
        public class Candidate
        {
            public Content content;
        }

        [Serializable]
        public class Content
        {
            public Part[] parts;
        }

        [Serializable]
        public class Part
        {
            public string text;
        }
    }
}