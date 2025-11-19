# Timeline 核心流程速查

## 快速参考流程图

### 1. 初始化流程

```
PlayableDirector.Play()
    ↓
TimelineAsset.CreatePlayable(graph, owner)
    ↓
TimelinePlayable.Create(graph, tracks, go, ...)
    ↓
TimelinePlayable.Compile(...)
    ├── 遍历 TrackAsset[]
    │   ├── TrackAsset.CreatePlayableGraph()
    │   │   ├── 创建 Track Mixer (CreateTrackMixer)
    │   │   ├── 为每个 Clip 创建 Playable
    │   │   ├── 连接 Clip Playables → Mixer
    │   │   └── 创建 RuntimeClip → 添加到 IntervalTree
    │   └── 创建 PlayableOutput
    └── IntervalTree 构建完成
```

### 2. 每帧评估流程

```
Unity PlayableGraph.Evaluate()
    ↓
TimelinePlayable.PrepareFrame(playable, info)
    ↓
TimelinePlayable.Evaluate(playable, frameData)
    ├── 获取当前时间: localTime = playable.GetTime()
    ├── 查询 IntervalTree: IntersectsWith(localTime, results)
    │   └── 返回所有包含 localTime 的 RuntimeClip[]
    ├── 处理新激活的 Clips:
    │   └── RuntimeClip.EvaluateAt(localTime, frameData)
    │       ├── enable = true (Playable.Play())
    │       ├── 计算 Clip 本地时间
    │       ├── 计算混合权重 (MixIn * MixOut)
    │       └── 设置 Playable 时间和权重
    ├── 处理禁用的 Clips:
    │   └── RuntimeClip.DisableAt(...)
    └── 执行评估回调
```

### 3. Clip 评估细节

```
RuntimeClip.EvaluateAt(localTime, frameData)
    ↓
1. 启用 Clip
   enable = true → Playable.Play()
    ↓
2. 计算 Clip 本地时间
   clipLocalTime = clip.ToLocalTime(localTime)
   - 考虑 clipIn (Clip 内部开始时间)
   - 考虑 timeScale (时间缩放)
    ↓
3. 应用外推 (Extrapolation)
   clipLocalTime = clip.EvaluateExtrapolationTime(clipLocalTime)
   - None: 不处理
   - Hold: 保持在边界值
   - Loop: 循环
   - PingPong: 乒乓
   - Continue: 继续
    ↓
4. 计算混合权重
   if (IsPreExtrapolatedTime) → weight = MixIn(start)
   else if (IsPostExtrapolatedTime) → weight = MixOut(end)
   else → weight = MixIn(time) * MixOut(time)
    ↓
5. 应用到 Playable
   SetTime(clipLocalTime)
   mixer.SetInputWeight(playable, weight)
```

## 关键数据结构

### IntervalTree 查询示例

```
时间轴: 0 ────── 5 ────── 10 ────── 15
Clips:
  Clip A: [2, 7]
  Clip B: [5, 12]
  Clip C: [10, 15]

查询时间点 6:
  IntervalTree.IntersectsWith(6, results)
  → 返回: [Clip A, Clip B]  (都在时间点 6 活跃)

查询时间点 8:
  IntervalTree.IntersectsWith(8, results)
  → 返回: [Clip B]  (只有 Clip B 在时间点 8 活跃)
```

## 核心类职责

| 类 | 职责 | 关键方法 |
|---|---|---|
| **TimelineAsset** | Timeline 资源，存储数据 | `CreatePlayable()` |
| **TimelinePlayable** | 运行时核心，编译和评估 | `Compile()`, `Evaluate()` |
| **TrackAsset** | 轨道基类，管理 Clips | `CreatePlayableGraph()`, `CreateTrackMixer()` |
| **TimelineClip** | 片段，时间信息 | `ToLocalTime()`, `EvaluateMixIn/Out()` |
| **RuntimeClip** | 运行时 Clip 包装 | `EvaluateAt()`, `DisableAt()` |
| **IntervalTree** | 区间树，高效查询 | `IntersectsWith()`, `Rebuild()` |

## 时间计算示例

```
全局时间: 10.0 秒
Clip 信息:
  - start: 5.0
  - duration: 8.0
  - clipIn: 2.0
  - timeScale: 2.0

计算 Clip 本地时间:
1. 相对时间: 10.0 - 5.0 = 5.0
2. 应用 timeScale: 5.0 * 2.0 = 10.0
3. 应用 clipIn: 10.0 + 2.0 = 12.0
4. Clip 本地时间 = 12.0
```

## 混合权重计算示例

```
Clip A: [5, 10], MixIn: 0→1 (0.5s), MixOut: 1→0 (0.5s)
Clip B: [8, 13], MixIn: 0→1 (0.5s), MixOut: 1→0 (0.5s)

时间点 9.0:
  Clip A: 
    - 在 Clip 范围内: ✓
    - MixIn: 已完成 (权重 1.0)
    - MixOut: 未开始 (权重 1.0)
    - 最终权重: 1.0 * 1.0 = 1.0
  
  Clip B:
    - 在 Clip 范围内: ✓
    - MixIn: 进行中 (时间 9.0 - 8.0 = 1.0, 混合时间 0.5)
    - MixOut: 未开始 (权重 1.0)
    - MixIn 权重: 1.0 (已完成)
    - 最终权重: 1.0 * 1.0 = 1.0

时间点 9.5 (重叠区域):
  Clip A:
    - MixOut: 进行中 (时间 10.0 - 9.5 = 0.5, 混合时间 0.5)
    - MixOut 权重: 0.5 / 0.5 = 1.0 (线性插值)
    - 最终权重: 1.0 * 1.0 = 1.0
  
  Clip B:
    - MixIn: 已完成
    - 最终权重: 1.0 * 1.0 = 1.0

注意: 实际混合由 Track Mixer 处理，这里只是 Clip 的权重
```

## 性能优化要点

1. **IntervalTree**: O(log n + k) 查询，而不是 O(n) 遍历
2. **Playable 缓存**: 避免重复创建 Playable
3. **列表预分配**: 减少 GC 分配
4. **位标记**: 使用 intervalBit 快速判断 Clip 是否活跃
5. **DiscreteTime**: 使用整数避免浮点精度问题

## 扩展点

### 创建自定义 Track

```csharp
public class MyCustomTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        return ScriptPlayable<MyCustomMixer>.Create(graph, inputCount);
    }
}

public class MyCustomMixer : PlayableBehaviour
{
    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        // 自定义混合逻辑
    }
}
```

### 创建自定义 Clip

```csharp
[Serializable]
public class MyCustomClip : PlayableAsset
{
    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        return ScriptPlayable<MyCustomBehaviour>.Create(graph);
    }
}

public class MyCustomBehaviour : PlayableBehaviour
{
    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        // Clip 逻辑
    }
}
```

## 调试技巧

### 1. 查看编译结果
```csharp
var director = GetComponent<PlayableDirector>();
var graph = director.playableGraph;
Debug.Log($"Graph has {graph.GetOutputCount()} outputs");
```

### 2. 监控评估过程
在 `TimelinePlayable.Evaluate()` 中添加日志：
```csharp
Debug.Log($"[Timeline] Time: {localTime}, Active: {m_CurrentListOfActiveClips.Count}");
```

### 3. 检查 IntervalTree
在 `IntervalTree.IntersectsWith()` 中添加日志：
```csharp
Debug.Log($"[IntervalTree] Query: {value}, Results: {results.Count}");
```

---

**快速参考**: 查看完整文档 `Timeline_Runtime源码分析与学习路线.md` 获取详细信息。


