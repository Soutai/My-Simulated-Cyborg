// DataTransferObjects.cs
// 这是一个纯粹的数据结构文件，放在项目的 Scripts/DTO 目录下

using System.Collections.Generic;

namespace EmbodiedAI.DTO
{
    [System.Serializable]
    public class PrimitiveCommand
    {
        public string op;        // 物理原语操作码: APPLY_FORCE, GRAB, RELEASE, USE_ITEM
        public float arg_x;      // 专为 APPLY_FORCE 准备的水平X分量力
        public float arg_z;      // 专为 APPLY_FORCE 准备的水平Z分量力
        public string target_id; // 专为 GRAB 准备的目标物体唯一标识
    }

    [System.Serializable]
    public class AIPhysicsDecision
    {
        public string monologue;
        public List<PrimitiveCommand> primitive_commands;
    }

    // Google API 规范需要的底层请求/响应外壳
    [System.Serializable]
    public class GeminiRequest
    {
        public RequestContent[] contents;
        [System.Serializable] public class RequestContent { public RequestPart[] parts; }
        [System.Serializable] public class RequestPart { public string text; }
    }

    [System.Serializable]
    public class GeminiResponse
    {
        public Candidate[] candidates;
        [System.Serializable] public class Candidate { public Content content; }
        [System.Serializable] public class Content { public Part[] parts; }
        [System.Serializable] public class Part { public string text; }
    }
}