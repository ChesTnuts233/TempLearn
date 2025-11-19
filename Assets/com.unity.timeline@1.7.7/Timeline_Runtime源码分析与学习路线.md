# Unity Timeline Runtime 源码分析与学习路线

## 目录
1. [核心架构概述](#核心架构概述)
2. [关键类和接口](#关键类和接口)
3. [执行流程详解](#执行流程详解)
4. [数据结构与算法](#数据结构与算法)
5. [学习路线](#学习路线)
6. [源码阅读建议](#源码阅读建议)

---

## 核心架构概述

### Timeline 系统架构图

```
PlayableDirector (MonoBehaviour)
    ↓
TimelineAsset (PlayableAsset)
    ↓
TimelinePlayable (PlayableBehaviour) ← 核心运行时类
    ↓
    ├── IntervalTree<RuntimeElement> ← 区间树，用于高效查询活跃的 Clips
    ├── TrackAsset[] ← 轨道列表
    │   ├── TrackAsset.CreatePlayableGraph()
    │   ├── TrackAsset.CreateTrackMixer() ← 创建混合器
    │   └── TimelineClip[] ← 片段列表
    │       └── PlayableAsset.CreatePlayable() ← 创建 Playable
    └── PlayableGraph (Unity Playables 系统)
```

### 核心设计模式

1. **Playable 系统**: Timeline 基于 Unity 的 Playables API 构建
2. **区间树 (Interval Tree)**: 用于高效查询在特定时间点活跃的 Clips
3. **混合器模式 (Mixer Pattern)**: 每个 Track 都有一个 Mixer 来混合多个 Clips
4. **编译时构建 (Compile-time Construction)**: Timeline 在运行时编译成 PlayableGraph

---

## 关键类和接口

### 1. TimelineAsset (TimelineAsset.cs)

**作用**: Timeline 资源的主类，继承自 `PlayableAsset`

**核心职责**:
- 存储 Timeline 的元数据（轨道、标记等）
- 实现 `PlayableAsset.CreatePlayable()` 创建 `TimelinePlayable`
- 管理 Timeline 的持续时间

**关键方法**:
```csharp
public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
{
    return TimelinePlayable.Create(graph, tracks, owner, autoRebalance, createOutputs);
}
```

**学习重点**:
- `DurationMode`: 如何计算 Timeline 持续时间
- `EditorSettings`: 编辑器相关设置（帧率等）

---

### 2. TimelinePlayable (TimelinePlayable.cs)

**作用**: Timeline 的运行时核心类，继承自 `PlayableBehaviour`

**核心职责**:
- 编译 Timeline 为 PlayableGraph
- 每帧评估活跃的 Clips
- 管理 IntervalTree 和 RuntimeElement

**关键数据结构**:
```csharp
private IntervalTree<RuntimeElement> m_IntervalTree;  // 区间树
private List<RuntimeElement> m_ActiveClips;           // 当前活跃的 Clips
private Dictionary<TrackAsset, Playable> m_PlayableCache; // Playable 缓存
```

**关键方法**:

#### Compile() - 编译 Timeline
```csharp
public void Compile(PlayableGraph graph, Playable timelinePlayable, 
                   IEnumerable<TrackAsset> tracks, GameObject go, 
                   bool autoRebalance, bool createOutputs)
```
- 遍历所有 Track，调用 `CreateTrackPlayable()` 创建 Playable
- 构建 IntervalTree，将所有 Clips 添加到树中
- 创建 PlayableOutput 连接

#### PrepareFrame() - 每帧准备
```csharp
public override void PrepareFrame(Playable playable, FrameData info)
```
- 调用 `Evaluate()` 评估当前时间点的活跃 Clips

#### Evaluate() - 评估活跃 Clips
```csharp
private void Evaluate(Playable playable, FrameData frameData)
```
- 使用 IntervalTree 查询当前时间点活跃的 Clips
- 调用每个活跃 Clip 的 `EvaluateAt()`
- 禁用不再活跃的 Clips

**学习重点**:
- 编译流程：如何从 TimelineAsset 构建 PlayableGraph
- 评估流程：如何每帧查询和评估活跃的 Clips
- IntervalTree 的使用

---

### 3. TrackAsset (TrackAsset.cs)

**作用**: 所有 Timeline 轨道的基类

**核心职责**:
- 管理 TimelineClips
- 创建 Track Mixer
- 创建 PlayableGraph 子图

**关键方法**:

#### CreatePlayableGraph() - 创建轨道的 PlayableGraph
```csharp
internal Playable CreatePlayableGraph(PlayableGraph graph, GameObject go, 
                                     IntervalTree<RuntimeElement> tree, 
                                     Playable timelinePlayable)
```
- 为每个 Clip 调用 `CreateClipPlayable()`
- 创建 Track Mixer（通过 `CreateTrackMixer()`）
- 将 Clip Playables 连接到 Mixer
- 创建 RuntimeClip 并添加到 IntervalTree

#### CreateTrackMixer() - 创建混合器（可重写）
```csharp
public virtual Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
```
- 默认返回一个简单的 `Playable.Create(graph, inputCount)`
- 子类可以重写以提供自定义混合逻辑

**学习重点**:
- Track 如何管理多个 Clips
- Mixer 的作用和创建方式
- 如何扩展自定义 Track

---

### 4. TimelineClip (TimelineClip.cs)

**作用**: 表示 Timeline 上的一个片段

**核心属性**:
- `start`: 开始时间
- `duration`: 持续时间
- `clipIn`: Clip 内部开始时间
- `timeScale`: 时间缩放
- `extrapolation`: 外推模式（None, Hold, Loop, PingPong, Continue）

**关键方法**:
- `EvaluateMixIn()`: 计算混合入权重
- `EvaluateMixOut()`: 计算混合出权重
- `IsPreExtrapolatedTime()`: 判断是否在前外推时间
- `IsPostExtrapolatedTime()`: 判断是否在后外推时间

**学习重点**:
- Clip 的时间计算和外推
- 混合曲线的计算
- Clip 与 PlayableAsset 的关系

---

### 5. RuntimeElement 和 RuntimeClip (Evaluation/)

**作用**: 运行时 Clip 的包装类

#### RuntimeElement (抽象基类)
```csharp
abstract class RuntimeElement : IInterval
{
    public abstract Int64 intervalStart { get; }
    public abstract Int64 intervalEnd { get; }
    public abstract bool enable { set; }
    public abstract void EvaluateAt(double localTime, FrameData frameData);
    public abstract void DisableAt(double localTime, double rootDuration, FrameData frameData);
}
```

#### RuntimeClip (具体实现)
```csharp
class RuntimeClip : RuntimeClipBase
{
    TimelineClip m_Clip;
    Playable m_Playable;
    Playable m_ParentMixer;
    
    public override void EvaluateAt(double localTime, FrameData frameData)
    {
        // 启用 Clip
        enable = true;
        
        // 计算混合权重
        float weight = CalculateWeight(localTime);
        
        // 设置 Playable 时间和权重
        SetTime(CalculateLocalTime(localTime));
        mixer.SetInputWeight(playable, weight);
    }
}
```

**学习重点**:
- RuntimeClip 如何包装 TimelineClip 和 Playable
- 时间计算和权重计算
- 启用/禁用逻辑

---

### 6. IntervalTree (Evaluation/IntervalTree.cs)

**作用**: 区间树数据结构，用于高效查询在特定时间点活跃的 Clips

**核心方法**:
```csharp
public void IntersectsWith(Int64 value, List<T> results)
```
- 查询与给定时间点相交的所有区间
- 时间复杂度: O(log n + k)，其中 k 是结果数量

**数据结构**:
```csharp
struct IntervalTreeNode
{
    public Int64 center;  // 中点
    public int first;     // 第一个元素索引
    public int last;      // 最后一个元素索引
    public int left;      // 左子树索引
    public int right;     // 右子树索引
}
```

**学习重点**:
- 区间树的工作原理
- 为什么使用区间树而不是简单遍历
- Rebuild 和 Query 的实现

---

## 执行流程详解

### 阶段1: 初始化与编译

```
1. PlayableDirector.Play()
   ↓
2. PlayableDirector.RebuildGraph()
   ↓
3. TimelineAsset.CreatePlayable(graph, owner)
   ↓
4. TimelinePlayable.Create(graph, tracks, go, ...)
   ↓
5. TimelinePlayable.Compile(...)
   ├── 遍历所有 Track
   │   ├── TrackAsset.CreatePlayableGraph()
   │   │   ├── 为每个 Clip 创建 Playable
   │   │   ├── TrackAsset.CreateTrackMixer()
   │   │   ├── 连接 Clip Playables 到 Mixer
   │   │   └── 创建 RuntimeClip 并添加到 IntervalTree
   │   └── 创建 PlayableOutput
   └── 构建 IntervalTree
```

**关键代码路径**:
1. `TimelinePlayable.Compile()` → `CompileTrackList()`
2. `CompileTrackList()` → `CreateTrackPlayable()`
3. `CreateTrackPlayable()` → `TrackAsset.CreatePlayableGraph()`
4. `TrackAsset.CreatePlayableGraph()` → 创建 Mixer 和 Clips

---

### 阶段2: 运行时评估（每帧）

```
1. PlayableGraph.Evaluate() (Unity 内部)
   ↓
2. TimelinePlayable.PrepareFrame(playable, info)
   ↓
3. TimelinePlayable.Evaluate(playable, frameData)
   ├── 获取当前时间: localTime = playable.GetTime()
   ├── 查询 IntervalTree: IntersectsWith(localTime, results)
   ├── 处理新激活的 Clips:
   │   └── RuntimeClip.EvaluateAt(localTime, frameData)
   │       ├── 计算本地时间
   │       ├── 计算混合权重
   │       └── 设置 Playable 时间和权重
   ├── 处理禁用的 Clips:
   │   └── RuntimeClip.DisableAt(localTime, duration, frameData)
   └── 执行评估回调:
       └── ITimelineEvaluateCallback.Evaluate()
```

**关键代码路径**:
1. `TimelinePlayable.PrepareFrame()` → `Evaluate()`
2. `Evaluate()` → `IntervalTree.IntersectsWith()`
3. `Evaluate()` → `RuntimeClip.EvaluateAt()`

---

### 阶段3: Clip 评估细节

#### RuntimeClip.EvaluateAt() 流程

```csharp
public override void EvaluateAt(double localTime, FrameData frameData)
{
    // 1. 启用 Clip
    enable = true;  // 如果未启用，会调用 Playable.Play()
    
    // 2. 处理循环
    if (frameData.timeLooped)
        SetTime(clip.clipIn);
    
    // 3. 计算混合权重
    float weight = 1.0f;
    if (clip.IsPreExtrapolatedTime(localTime))
        weight = clip.EvaluateMixIn((float)clip.start);
    else if (clip.IsPostExtrapolatedTime(localTime))
        weight = clip.EvaluateMixOut((float)clip.end);
    else
        weight = clip.EvaluateMixIn(localTime) * clip.EvaluateMixOut(localTime);
    
    // 4. 计算 Clip 的本地时间
    double clipLocalTime = clip.ToLocalTime(localTime);
    
    // 5. 应用外推
    clipLocalTime = clip.EvaluateExtrapolationTime(clipLocalTime);
    
    // 6. 设置 Playable 时间和权重
    SetTime(clipLocalTime);
    mixer.SetInputWeight(playable, weight);
}
```

---

## 数据结构与算法

### 1. IntervalTree (区间树)

**用途**: 高效查询在特定时间点活跃的 Clips

**为什么使用区间树**:
- 简单遍历: O(n) 时间复杂度
- 区间树: O(log n + k) 时间复杂度，其中 k 是结果数量
- 当 Timeline 有大量 Clips 时，性能提升明显

**工作原理**:
1. 构建: 将所有 Clips 的 [start, end] 区间添加到树中
2. 查询: 给定时间点 t，查询所有包含 t 的区间
3. 重建: 当区间改变时，标记为 dirty，下次查询时重建

**关键代码**:
```csharp
// 查询
public void IntersectsWith(Int64 value, List<T> results)
{
    if (dirty) Rebuild();
    Query(m_Nodes[0], value, results);
}

// 递归查询
void Query(IntervalTreeNode node, Int64 value, List<T> results)
{
    // 如果值在节点中点左侧，查询左子树
    if (value < node.center && node.left != kInvalidNode)
        Query(m_Nodes[node.left], value, results);
    
    // 检查当前节点的区间
    for (int i = node.first; i <= node.last; i++)
    {
        if (m_Entries[i].intervalStart <= value && 
            m_Entries[i].intervalEnd >= value)
            results.Add(m_Entries[i].item);
    }
    
    // 如果值在节点中点右侧，查询右子树
    if (value > node.center && node.right != kInvalidNode)
        Query(m_Nodes[node.right], value, results);
}
```

---

### 2. DiscreteTime (离散时间)

**用途**: 使用整数表示时间，避免浮点数精度问题

**为什么使用离散时间**:
- 浮点数精度问题可能导致时间比较不准确
- 使用 tick（1/60 秒）作为最小单位
- 所有时间计算都转换为整数进行比较

**关键代码**:
```csharp
public struct DiscreteTime
{
    private Int64 m_DiscreteTime;
    
    public DiscreteTime(double time)
    {
        m_DiscreteTime = (Int64)(time * TimeUtility.kTimeEpsilon);
    }
    
    public static Int64 GetNearestTick(double time)
    {
        return (Int64)Math.Round(time * TimeUtility.kTimeEpsilon);
    }
}
```

---

### 3. Playable 缓存

**用途**: 避免重复创建 Playable

**实现**:
```csharp
private Dictionary<TrackAsset, Playable> m_PlayableCache;

Playable CreateTrackPlayable(...)
{
    if (m_PlayableCache.TryGetValue(track, out playable))
        return playable;  // 使用缓存
    
    // 创建新的 Playable
    playable = track.CreatePlayableGraph(...);
    m_PlayableCache[track] = playable;
    return playable;
}
```

---

## 学习路线

### 阶段1: 基础理解（1-2天）

**目标**: 理解 Timeline 的基本概念和架构

**学习内容**:
1. **Unity Playables 系统基础**
   - 阅读 Unity 官方文档: [Playables API](https://docs.unity3d.com/Manual/Playables.html)
   - 理解 `Playable`, `PlayableGraph`, `PlayableBehaviour` 的概念

2. **Timeline 核心类**
   - `TimelineAsset.cs`: Timeline 资源类
   - `TimelinePlayable.cs`: 运行时核心类（重点）
   - `TrackAsset.cs`: 轨道基类
   - `TimelineClip.cs`: 片段类

**推荐阅读顺序**:
1. `TimelineAsset.cs` (前 200 行) - 了解 Timeline 资源结构
2. `TimelinePlayable.cs` (完整) - 理解编译和评估流程
3. `TrackAsset.cs` (CreatePlayableGraph 方法) - 理解轨道如何创建 PlayableGraph

**实践**:
- 创建一个简单的 Timeline，在代码中访问 `PlayableDirector`
- 观察 `PlayableGraph` 的结构

---

### 阶段2: 编译流程深入（2-3天）

**目标**: 理解 Timeline 如何编译成 PlayableGraph

**学习内容**:
1. **编译流程**
   - `TimelinePlayable.Compile()` 方法
   - `TrackAsset.CreatePlayableGraph()` 方法
   - `TrackAsset.CreateTrackMixer()` 方法
   - Clip Playable 的创建

2. **IntervalTree 构建**
   - `IntervalTree.Add()` 方法
   - `IntervalTree.Rebuild()` 方法

**推荐阅读顺序**:
1. `TimelinePlayable.cs` - `Compile()` 方法（99-138 行）
2. `TimelinePlayable.cs` - `CreateTrackPlayable()` 方法（188-228 行）
3. `TrackAsset.cs` - `CreatePlayableGraph()` 方法（774-850 行）
4. `IntervalTree.cs` - `Add()` 和 `Rebuild()` 方法

**实践**:
- 在 `TimelinePlayable.Compile()` 中添加日志，观察编译过程
- 检查生成的 `PlayableGraph` 结构

---

### 阶段3: 运行时评估（2-3天）

**目标**: 理解 Timeline 如何每帧评估活跃的 Clips

**学习内容**:
1. **评估流程**
   - `TimelinePlayable.PrepareFrame()` 方法
   - `TimelinePlayable.Evaluate()` 方法
   - `IntervalTree.IntersectsWith()` 方法

2. **RuntimeClip 评估**
   - `RuntimeClip.EvaluateAt()` 方法
   - 时间计算和权重计算
   - 外推处理

**推荐阅读顺序**:
1. `TimelinePlayable.cs` - `PrepareFrame()` 方法（235-250 行）
2. `TimelinePlayable.cs` - `Evaluate()` 方法（252-290 行）
3. `IntervalTree.cs` - `IntersectsWith()` 和 `Query()` 方法
4. `RuntimeClip.cs` - `EvaluateAt()` 方法（82-128 行）

**实践**:
- 在 `Evaluate()` 中添加日志，观察每帧评估的 Clips
- 测试不同时间点的 Clip 激活情况

---

### 阶段4: 高级特性（3-5天）

**目标**: 理解 Timeline 的高级特性

**学习内容**:
1. **混合和权重**
   - `TimelineClip.EvaluateMixIn()` 和 `EvaluateMixOut()`
   - Track Mixer 的工作原理
   - 自定义 Mixer 的实现

2. **外推 (Extrapolation)**
   - `TimelineClip.ClipExtrapolation` 枚举
   - `TimelineClip.EvaluateExtrapolationTime()` 方法

3. **通知和标记**
   - `IMarker` 接口
   - `SignalTrack` 和 `SignalEmitter`
   - `TimeNotificationBehaviour`

4. **嵌套 Timeline**
   - `DirectorControlPlayable` 类
   - 如何控制其他 `PlayableDirector`

**推荐阅读顺序**:
1. `TimelineClip.cs` - 混合和外推相关方法
2. `ActivationTrack.cs` - 查看自定义 Mixer 示例
3. `DirectorControlPlayable.cs` - 嵌套 Timeline 控制
4. `Events/Signals/` - 信号系统

**实践**:
- 创建一个自定义 Track 和 Mixer
- 实现一个简单的信号系统

---

### 阶段5: 数据结构与算法（2-3天）

**目标**: 深入理解 Timeline 使用的数据结构和算法

**学习内容**:
1. **IntervalTree 算法**
   - 区间树的原理
   - 构建和查询算法
   - 性能分析

2. **DiscreteTime**
   - 为什么使用离散时间
   - 时间转换和比较

3. **优化技术**
   - Playable 缓存
   - 列表预分配
   - 位标记优化

**推荐阅读顺序**:
1. `IntervalTree.cs` - 完整实现
2. `DiscreteTime.cs` - 时间处理
3. `TimelinePlayable.cs` - 优化技术

**实践**:
- 实现一个简单的区间树
- 分析 Timeline 的性能瓶颈

---

## 源码阅读建议

### 1. 阅读顺序建议

**初学者**:
```
TimelineAsset.cs (基础)
  → TimelinePlayable.cs (核心)
    → TrackAsset.cs (轨道)
      → TimelineClip.cs (片段)
        → RuntimeClip.cs (运行时)
          → IntervalTree.cs (数据结构)
```

**有经验者**:
```
TimelinePlayable.cs (整体架构)
  → Evaluation/ (评估系统)
    → Playables/ (Playable 实现)
      → TrackAsset.cs (扩展点)
        → 具体 Track 实现 (AnimationTrack, AudioTrack 等)
```

---

### 2. 关键方法索引

| 类 | 方法 | 作用 | 行号范围 |
|---|---|---|---|
| TimelinePlayable | `Compile()` | 编译 Timeline | 99-123 |
| TimelinePlayable | `Evaluate()` | 评估活跃 Clips | 252-290 |
| TimelinePlayable | `CreateTrackPlayable()` | 创建轨道 Playable | 188-228 |
| TrackAsset | `CreatePlayableGraph()` | 创建轨道图 | 774-850 |
| TrackAsset | `CreateTrackMixer()` | 创建混合器 | 464-467 |
| RuntimeClip | `EvaluateAt()` | 评估 Clip | 82-128 |
| IntervalTree | `IntersectsWith()` | 查询区间 | 66-79 |
| IntervalTree | `Rebuild()` | 重建树 | 100+ |

---

### 3. 调试技巧

**添加日志**:
```csharp
// 在 TimelinePlayable.Evaluate() 中添加
Debug.Log($"[Timeline] Evaluate at time: {localTime}, Active clips: {m_CurrentListOfActiveClips.Count}");

// 在 RuntimeClip.EvaluateAt() 中添加
Debug.Log($"[RuntimeClip] {clip.displayName} - LocalTime: {clipLocalTime}, Weight: {weight}");
```

**使用 Unity Profiler**:
- 查看 `TimelinePlayable.Evaluate()` 的调用频率和耗时
- 分析 `IntervalTree.IntersectsWith()` 的性能

**断点调试**:
- 在 `TimelinePlayable.Compile()` 设置断点，观察编译过程
- 在 `TimelinePlayable.Evaluate()` 设置断点，观察每帧评估

---

### 4. 常见问题

**Q: 为什么 Timeline 使用 IntervalTree 而不是简单遍历？**
A: 当 Timeline 有大量 Clips 时，区间树可以将查询时间复杂度从 O(n) 降低到 O(log n + k)。

**Q: TimelinePlayable 和 TimelineAsset 的区别？**
A: `TimelineAsset` 是资源类，存储 Timeline 数据；`TimelinePlayable` 是运行时类，负责编译和执行。

**Q: 如何扩展 Timeline 创建自定义 Track？**
A: 继承 `TrackAsset`，重写 `CreateTrackMixer()` 方法，创建自定义的 `PlayableBehaviour`。

**Q: Clip 的时间是如何计算的？**
A: 通过 `TimelineClip.ToLocalTime()` 将全局时间转换为 Clip 本地时间，考虑 `clipIn`、`timeScale` 和外推。

---

### 5. 扩展阅读

**Unity 官方文档**:
- [Timeline Manual](https://docs.unity3d.com/Manual/TimelineSection.html)
- [Playables API](https://docs.unity3d.com/Manual/Playables.html)
- [Creating Custom Tracks](https://docs.unity3d.com/Manual/timeline-custom-tracks.html)

**相关源码**:
- `UnityEngine.Playables` 命名空间
- `UnityEngine.Animations` 命名空间
- `UnityEngine.Audio` 命名空间

**算法参考**:
- 区间树 (Interval Tree) 算法
- 线段树 (Segment Tree) 算法（相关）

---

## 总结

Unity Timeline Runtime 源码的核心在于：

1. **编译时**: 将 TimelineAsset 编译成 PlayableGraph，构建 IntervalTree
2. **运行时**: 每帧使用 IntervalTree 查询活跃的 Clips，评估它们的时间和权重
3. **扩展性**: 通过 TrackAsset 和 PlayableBehaviour 提供丰富的扩展点

理解 Timeline 源码的关键是理解 **Playables 系统** 和 **区间树算法**。建议按照学习路线循序渐进，先理解整体架构，再深入细节实现。

---

**最后更新**: 2024年
**版本**: Unity Timeline 1.7.7


