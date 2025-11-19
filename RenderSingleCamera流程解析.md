# RenderSingleCamera 方法详细流程解析

本文档详细解析 URP 中 `RenderSingleCamera` 方法的执行流程，这是渲染单个相机的核心方法。

## 方法概述

```csharp
/// <summary>
/// Renders a single camera. This method will do culling, setup and execution of the renderer.
/// 渲染单个相机。此方法将执行剔除、设置和执行渲染器。
/// </summary>
/// <param name="context">Render context used to record commands during execution.</param>
/// <param name="cameraData">Camera rendering data. This might contain data inherited from a base camera.</param>
static void RenderSingleCamera(ScriptableRenderContext context, ref CameraData cameraData)
```

## 完整代码流程（带中文注释）

```597:710:Assets/com.unity.render-pipelines.universal@14.0.11/Runtime/UniversalRenderPipeline.cs
        /// <summary>
        /// Renders a single camera. This method will do culling, setup and execution of the renderer.
        /// </summary>
        /// <param name="context">Render context used to record commands during execution.</param>
        /// <param name="cameraData">Camera rendering data. This might contain data inherited from a base camera.</param>
        static void RenderSingleCamera(ScriptableRenderContext context, ref CameraData cameraData)
        {
            Camera camera = cameraData.camera;
            var renderer = cameraData.renderer;
            if (renderer == null)
            {
                Debug.LogWarning(string.Format("Trying to render {0} with an invalid renderer. Camera rendering will be skipped.", camera.name));
                return;
            }

            if (!TryGetCullingParameters(cameraData, out var cullingParameters))
                return;

            ScriptableRenderer.current = renderer;
            bool isSceneViewCamera = cameraData.isSceneViewCamera;

            // NOTE: Do NOT mix ProfilingScope with named CommandBuffers i.e. CommandBufferPool.Get("name").
            // Currently there's an issue which results in mismatched markers.
            // The named CommandBuffer will close its "profiling scope" on execution.
            // That will orphan ProfilingScope markers as the named CommandBuffer markers are their parents.
            // Resulting in following pattern:
            // exec(cmd.start, scope.start, cmd.end) and exec(cmd.start, scope.end, cmd.end)
            CommandBuffer cmd = CommandBufferPool.Get();

            // TODO: move skybox code from C++ to URP in order to remove the call to context.Submit() inside DrawSkyboxPass
            // Until then, we can't use nested profiling scopes with XR multipass
            CommandBuffer cmdScope = cameraData.xr.enabled ? null : cmd;

            ProfilingSampler sampler = Profiling.TryGetOrAddCameraSampler(camera);
            using (new ProfilingScope(cmdScope, sampler)) // Enqueues a "BeginSample" command into the CommandBuffer cmd
            {
                renderer.Clear(cameraData.renderType);

                using (new ProfilingScope(null, Profiling.Pipeline.Renderer.setupCullingParameters))
                {
                    renderer.OnPreCullRenderPasses(in cameraData);
                    renderer.SetupCullingParameters(ref cullingParameters, ref cameraData);
                }

                context.ExecuteCommandBuffer(cmd); // Send all the commands enqueued so far in the CommandBuffer cmd, to the ScriptableRenderContext context
                cmd.Clear();

                SetupPerCameraShaderConstants(cmd);

                // Emit scene/game view UI. The main game camera UI is always rendered, so this needs to be handled only for different camera types
                if (camera.cameraType == CameraType.Reflection || camera.cameraType == CameraType.Preview)
                    ScriptableRenderContext.EmitGeometryForCamera(camera);
#if UNITY_EDITOR
                else if (isSceneViewCamera)
                    ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
#endif

                // Update camera motion tracking (prev matrices) from cameraData.
                // Called and updated only once, as the same camera can be rendered multiple times.
                // NOTE: Tracks only the current (this) camera, not shadow views or any other offscreen views.
                // NOTE: Shared between both Execute and Render (RG) paths.
                if (camera.TryGetComponent<UniversalAdditionalCameraData>(out var additionalCameraData))
                    additionalCameraData.motionVectorsPersistentData.Update(ref cameraData);

                // Update TAA persistent data based on cameraData. Most importantly resize the history render targets.
                // NOTE: Persistent data is kept over multiple frames. Its life-time differs from typical resources.
                // NOTE: Shared between both Execute and Render (RG) paths.
                if (cameraData.taaPersistentData != null)
                    UpdateTemporalAATargets(ref cameraData);

                RTHandles.SetReferenceSize(cameraData.cameraTargetDescriptor.width, cameraData.cameraTargetDescriptor.height);

                // Do NOT use cameraData after 'InitializeRenderingData'. CameraData state may diverge otherwise.
                // RenderingData takes a copy of the CameraData.
                var cullResults = context.Cull(ref cullingParameters);
                InitializeRenderingData(asset, ref cameraData, ref cullResults, cmd, out var renderingData);
#if ADAPTIVE_PERFORMANCE_2_0_0_OR_NEWER
                if (asset.useAdaptivePerformance)
                    ApplyAdaptivePerformance(ref renderingData);
#endif

                renderer.AddRenderPasses(ref renderingData);

                if (useRenderGraph)
                {
                    RecordAndExecuteRenderGraph(s_RenderGraph, context, ref renderingData);
                    renderer.FinishRenderGraphRendering(context, ref renderingData);
                }
                else
                {
                    using (new ProfilingScope(null, Profiling.Pipeline.Renderer.setup))
                        renderer.Setup(context, ref renderingData);

                    // Timing scope inside
                    renderer.Execute(context, ref renderingData);
                }
            } // When ProfilingSample goes out of scope, an "EndSample" command is enqueued into CommandBuffer cmd

            context.ExecuteCommandBuffer(cmd); // Sends to ScriptableRenderContext all the commands enqueued since cmd.Clear, i.e the "EndSample" command
            CommandBufferPool.Release(cmd);

            using (new ProfilingScope(null, Profiling.Pipeline.Context.submit))
            {
                if (renderer.useRenderPassEnabled && !context.SubmitForRenderPassValidation())
                {
                    renderer.useRenderPassEnabled = false;
                    CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.RenderPassEnabled, false);
                    Debug.LogWarning("Rendering command not supported inside a native RenderPass found. Falling back to non-RenderPass rendering path");
                }
                context.Submit(); // Actually execute the commands that we previously sent to the ScriptableRenderContext context
            }

            ScriptableRenderer.current = null;
        }
```

## 详细步骤解析

### 步骤 1：参数验证和初始化

```csharp
Camera camera = cameraData.camera;
var renderer = cameraData.renderer;
if (renderer == null)
{
    Debug.LogWarning(string.Format("Trying to render {0} with an invalid renderer. Camera rendering will be skipped.", camera.name));
    return;
}
```

**作用**：
- 从 `cameraData` 中提取相机组件和渲染器引用
- **验证渲染器是否有效**：如果渲染器为 null，输出警告并跳过渲染
- 这是第一道安全检查，确保后续流程有有效的渲染器可用

### 步骤 2：获取剔除参数

```csharp
if (!TryGetCullingParameters(cameraData, out var cullingParameters))
    return;
```

**作用**：
- 调用 `TryGetCullingParameters` 获取剔除参数
- 剔除参数用于确定哪些物体在相机视野内，需要被渲染
- **如果获取失败**（例如 XR 相机配置错误），直接返回，不进行渲染

**TryGetCullingParameters 方法**：

```579:595:Assets/com.unity.render-pipelines.universal@14.0.11/Runtime/UniversalRenderPipeline.cs
        static bool TryGetCullingParameters(CameraData cameraData, out ScriptableCullingParameters cullingParams)
        {
#if ENABLE_VR && ENABLE_XR_MODULE
            if (cameraData.xr.enabled)
            {
                cullingParams = cameraData.xr.cullingParams;

                // Sync the FOV on the camera to match the projection from the XR device
                if (!cameraData.camera.usePhysicalProperties && !XRGraphicsAutomatedTests.enabled)
                    cameraData.camera.fieldOfView = Mathf.Rad2Deg * Mathf.Atan(1.0f / cullingParams.stereoProjectionMatrix.m11) * 2.0f;

                return true;
            }
#endif

            return cameraData.camera.TryGetCullingParameters(false, out cullingParams);
        }
```

**说明**：
- 对于 XR 相机，使用 XR 系统提供的剔除参数
- 对于普通相机，使用 Unity 标准方法获取剔除参数
- 返回 false 表示无法获取有效的剔除参数

### 步骤 3：设置当前渲染器

**代码位置**：`UniversalRenderPipeline.cs` 第 **615** 行

```615:616:Assets/com.unity.render-pipelines.universal@14.0.11/Runtime/UniversalRenderPipeline.cs
            ScriptableRenderer.current = renderer;
            bool isSceneViewCamera = cameraData.isSceneViewCamera;
```

**作用**：
- **设置静态当前渲染器**：使其他代码可以通过 `ScriptableRenderer.current` 访问当前正在使用的渲染器
- 判断是否为场景视图相机（用于后续的特殊处理）

**详细说明**：

1. **定义位置**：`ScriptableRenderer.current` 在 `ScriptableRenderer.cs` 第 126 行定义为静态字段：

```126:126:Assets/com.unity.render-pipelines.universal@14.0.11/Runtime/ScriptableRenderer.cs
        internal static ScriptableRenderer current = null;
```

2. **设置时机**：在 `RenderSingleCamera` 方法开始渲染时设置（第 615 行）

3. **清除时机**：在渲染完成后清除（第 709 行）：

```709:709:Assets/com.unity.render-pipelines.universal@14.0.11/Runtime/UniversalRenderPipeline.cs
            ScriptableRenderer.current = null;
```

4. **使用场景**：在渲染过程中，其他代码可以通过 `ScriptableRenderer.current` 访问当前渲染器，例如：

```562:567:Assets/com.unity.render-pipelines.universal@14.0.11/Runtime/UniversalRenderPipelineCore.cs
            var renderer = ScriptableRenderer.current;
            Debug.Assert(renderer != null, "IsCameraProjectionMatrixFlipped is being called outside camera rendering scope.");

            if (renderer != null)
            {
                var handle = renderer.cameraColorTargetHandle;
```

**设计目的**：
- 类似于 `Camera.current`，提供全局访问当前渲染器的机制
- 只在渲染过程中有效（渲染开始设置为非 null，渲染结束设置为 null）
- 用于在渲染过程中访问渲染器相关的资源（如渲染目标句柄）

### 步骤 4：获取命令缓冲区

```csharp
CommandBuffer cmd = CommandBufferPool.Get();
CommandBuffer cmdScope = cameraData.xr.enabled ? null : cmd;
```

**作用**：
- **从对象池获取命令缓冲区**：避免每帧分配新对象，减少 GC 压力
- **设置性能分析范围**：对于 XR 多通道渲染，不使用命令缓冲区进行性能分析（因为存在嵌套问题）

### 步骤 5：开始性能分析

```csharp
ProfilingSampler sampler = Profiling.TryGetOrAddCameraSampler(camera);
using (new ProfilingScope(cmdScope, sampler))
{
    // 渲染逻辑
}
```

**作用**：
- **获取或创建性能采样器**：用于在 Unity Profiler 中记录此相机的渲染时间
- **创建性能分析作用域**：自动记录整个渲染过程的开始和结束时间

### 步骤 6：清除渲染状态

```csharp
renderer.Clear(cameraData.renderType);
```

**作用**：
- **清除渲染器的内部状态**：为新的渲染帧做准备
- 根据相机渲染类型（Base/Overlay）执行相应的清除操作

### 步骤 7：设置剔除参数

```csharp
using (new ProfilingScope(null, Profiling.Pipeline.Renderer.setupCullingParameters))
{
    renderer.OnPreCullRenderPasses(in cameraData);
    renderer.SetupCullingParameters(ref cullingParameters, ref cameraData);
}
```

**作用**：
- **OnPreCullRenderPasses**：在剔除前调用，允许渲染器特性修改相机数据或添加预剔除的渲染通道
- **SetupCullingParameters**：设置剔除参数，包括：
  - 视锥体参数
  - 剔除标志（如阴影剔除、遮挡剔除等）
  - 层级遮罩
  - 其他剔除相关的设置

### 步骤 8：执行命令缓冲区并清除

```csharp
context.ExecuteCommandBuffer(cmd); // 将 CommandBuffer 中的命令发送到 ScriptableRenderContext
cmd.Clear(); // 清空命令缓冲区，准备记录新的命令
```

**作用**：
- **执行已记录的命令**：将之前记录的所有命令提交到渲染上下文
- **清空命令缓冲区**：为后续命令做准备，避免命令累积

### 步骤 9：设置每相机着色器常量

```csharp
SetupPerCameraShaderConstants(cmd);
```

**作用**：
- **设置每个相机特有的着色器全局变量**，例如：
  - 相机位置
  - 相机方向
  - 投影参数
  - 时间相关参数
  - 其他相机特定的着色器常量

### 步骤 10：发射几何体（特殊相机类型）

```csharp
// 发射场景/游戏视图 UI。主游戏相机 UI 总是被渲染，所以这只需要处理不同的相机类型
if (camera.cameraType == CameraType.Reflection || camera.cameraType == CameraType.Preview)
    ScriptableRenderContext.EmitGeometryForCamera(camera);
#if UNITY_EDITOR
else if (isSceneViewCamera)
    ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
#endif
```

**作用**：
- **反射相机**：发射用于反射的几何体
- **预览相机**：发射用于预览的几何体
- **场景视图相机**：发射场景视图中的几何体（编辑器模式）
- **主游戏相机**：不需要特殊处理，UI 会自动渲染

### 步骤 11：更新运动向量数据

```csharp
// 从 cameraData 更新相机运动跟踪（上一帧的矩阵）
// 只调用和更新一次，因为同一个相机可能被渲染多次
// 注意：只跟踪当前（这个）相机，不包括阴影视图或任何其他离屏视图
// 注意：在执行和渲染（RG）路径之间共享
if (camera.TryGetComponent<UniversalAdditionalCameraData>(out var additionalCameraData))
    additionalCameraData.motionVectorsPersistentData.Update(ref cameraData);
```

**作用**：
- **更新运动向量持久数据**：保存上一帧的相机矩阵，用于计算运动向量
- **运动向量用途**：用于运动模糊、TAA（时间抗锯齿）等效果
- **只更新一次**：即使同一相机被多次渲染，也只更新一次

### 步骤 12：更新 TAA（时间抗锯齿）数据

```csharp
// 基于 cameraData 更新 TAA 持久数据。最重要的是调整历史渲染目标的大小
// 注意：持久数据在多帧之间保持。其生命周期与典型资源不同
// 注意：在执行和渲染（RG）路径之间共享
if (cameraData.taaPersistentData != null)
    UpdateTemporalAATargets(ref cameraData);
```

**作用**：
- **调整 TAA 历史缓冲区大小**：根据当前相机分辨率调整历史帧缓冲区
- **管理 TAA 资源**：确保历史缓冲区与当前渲染分辨率匹配
- **TAA 原理**：通过复用历史帧信息来减少锯齿

### 步骤 13：设置渲染目标引用大小

```csharp
RTHandles.SetReferenceSize(cameraData.cameraTargetDescriptor.width, cameraData.cameraTargetDescriptor.height);
```

**作用**：
- **设置 RTHandle 系统的引用大小**：RTHandle 系统用于管理渲染目标，避免频繁分配和释放
- **动态调整**：根据相机分辨率动态调整渲染目标池的大小
- **性能优化**：减少内存分配和碎片化

### 步骤 14：执行剔除操作

```csharp
// 注意：在 'InitializeRenderingData' 之后不要使用 cameraData
// 否则 CameraData 状态可能会发散
// RenderingData 会复制 CameraData 的副本
var cullResults = context.Cull(ref cullingParameters);
```

**作用**：
- **执行剔除**：根据剔除参数确定哪些物体在相机视野内
- **返回剔除结果**：包含所有可见物体的信息，包括：
  - 可见的渲染器
  - 可见的光源
  - 可见的反射探针
  - 其他可见对象
- **性能关键**：剔除操作可以减少需要渲染的物体数量，提高性能

### 步骤 15：初始化渲染数据

```csharp
InitializeRenderingData(asset, ref cameraData, ref cullResults, cmd, out var renderingData);
#if ADAPTIVE_PERFORMANCE_2_0_0_OR_NEWER
if (asset.useAdaptivePerformance)
    ApplyAdaptivePerformance(ref renderingData);
#endif
```

**作用**：
- **初始化 RenderingData**：将 CameraData 和 CullingResults 组合成 RenderingData
- **包含的信息**：
  - 相机数据
  - 剔除结果
  - 命令缓冲区
  - 光照数据
  - 阴影数据
- **自适应性能**：如果启用了自适应性能，会根据设备性能调整渲染设置

### 步骤 16：添加渲染通道

```csharp
renderer.AddRenderPasses(ref renderingData);
```

**作用**：
- **收集所有渲染通道**：从渲染器特性（Renderer Features）中收集需要执行的渲染通道
- **添加到队列**：将渲染通道添加到 `activeRenderPassQueue` 中
- **渲染通道来源**：
  - 内置渲染通道（如阴影、深度、不透明物体等）
  - 自定义渲染器特性添加的通道

**AddRenderPasses 实现**：

```1366:1396:Assets/com.unity.render-pipelines.universal@14.0.11/Runtime/ScriptableRenderer.cs
        internal void AddRenderPasses(ref RenderingData renderingData)
        {
            // 清空之前的渲染通道队列
            activeRenderPassQueue.Clear();

            // 从自定义渲染器特性添加渲染通道
            for (int i = 0; i < rendererFeatures.Count; ++i)
            {
                if (!rendererFeatures[i].isActive)
                {
                    continue;
                }

                if (!rendererFeatures[i].SupportsNativeRenderPass())
                    disableNativeRenderPassInFeatures = true;

                rendererFeatures[i].AddRenderPasses(this, ref renderingData);
                disableNativeRenderPassInFeatures = false;
            }

            // 移除任何可能被用户错误添加的空渲染通道
            int count = activeRenderPassQueue.Count;
            for (int i = count - 1; i >= 0; i--)
            {
                if (activeRenderPassQueue[i] == null)
                    activeRenderPassQueue.RemoveAt(i);
            }

            // 如果注入任何通道，"自动"存储优化策略将禁用优化的加载操作
            if (count > 0 && m_StoreActionsOptimizationSetting == StoreActionsOptimization.Auto)
                m_UseOptimizedStoreActions = false;
        }
```

### 步骤 17：执行渲染（两种路径）

#### 路径 A：使用 Render Graph（如果启用）

```csharp
if (useRenderGraph)
{
    RecordAndExecuteRenderGraph(s_RenderGraph, context, ref renderingData);
    renderer.FinishRenderGraphRendering(context, ref renderingData);
}
```

**作用**：
- **Render Graph**：Unity 的现代渲染图系统，用于更高效的资源管理和调度
- **RecordAndExecuteRenderGraph**：记录渲染图并执行
- **FinishRenderGraphRendering**：完成渲染图的清理工作

#### 路径 B：传统渲染路径（默认）

```csharp
else
{
    using (new ProfilingScope(null, Profiling.Pipeline.Renderer.setup))
        renderer.Setup(context, ref renderingData);

    // 内部有性能分析作用域
    renderer.Execute(context, ref renderingData);
}
```

**作用**：
- **Setup**：设置渲染器，准备渲染环境
  - 创建渲染目标
  - 设置渲染状态
  - 配置渲染通道
- **Execute**：执行所有渲染通道
  - 按顺序执行所有渲染通道
  - 包括阴影、深度、不透明物体、透明物体、后处理等

### 步骤 18：执行最终命令并释放资源

```csharp
} // 当 ProfilingSample 超出作用域时，会将 "EndSample" 命令加入 CommandBuffer cmd

context.ExecuteCommandBuffer(cmd); // 将自 cmd.Clear 以来加入的所有命令发送到 ScriptableRenderContext，即 "EndSample" 命令
CommandBufferPool.Release(cmd); // 将命令缓冲区释放回对象池
```

**作用**：
- **执行最终命令**：执行性能分析的结束标记
- **释放命令缓冲区**：将命令缓冲区返回到对象池，供后续使用

### 步骤 19：提交渲染上下文

```csharp
using (new ProfilingScope(null, Profiling.Pipeline.Context.submit))
{
    if (renderer.useRenderPassEnabled && !context.SubmitForRenderPassValidation())
    {
        renderer.useRenderPassEnabled = false;
        CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.RenderPassEnabled, false);
        Debug.LogWarning("Rendering command not supported inside a native RenderPass found. Falling back to non-RenderPass rendering path");
    }
    context.Submit(); // 实际执行我们之前发送到 ScriptableRenderContext 的所有命令
}
```

**作用**：
- **验证 Render Pass**：如果使用了原生 Render Pass，验证其兼容性
- **降级处理**：如果检测到不兼容的命令，自动降级到非 Render Pass 路径
- **提交渲染**：将所有命令提交到 GPU 执行
- **关键步骤**：这是实际开始 GPU 渲染的节点

### 步骤 20：清理当前渲染器引用

```csharp
ScriptableRenderer.current = null;
```

**作用**：
- **清除静态引用**：防止其他代码访问到已结束的渲染器
- **避免错误引用**：确保 `ScriptableRenderer.current` 只在渲染过程中有效

## 完整流程图

```
开始 RenderSingleCamera
    │
    ├─ 1. 验证渲染器有效性
    │   └─ 如果无效，返回
    │
    ├─ 2. 获取剔除参数
    │   └─ 如果失败，返回
    │
    ├─ 3. 设置当前渲染器
    │
    ├─ 4. 获取命令缓冲区
    │
    ├─ 5. 开始性能分析
    │   │
    │   ├─ 6. 清除渲染状态
    │   │
    │   ├─ 7. 设置剔除参数
    │   │   ├─ OnPreCullRenderPasses
    │   │   └─ SetupCullingParameters
    │   │
    │   ├─ 8. 执行命令缓冲区
    │   │
    │   ├─ 9. 设置每相机着色器常量
    │   │
    │   ├─ 10. 发射几何体（特殊相机）
    │   │
    │   ├─ 11. 更新运动向量数据
    │   │
    │   ├─ 12. 更新 TAA 数据
    │   │
    │   ├─ 13. 设置渲染目标引用大小
    │   │
    │   ├─ 14. 执行剔除操作
    │   │   └─ context.Cull()
    │   │
    │   ├─ 15. 初始化渲染数据
    │   │   └─ InitializeRenderingData()
    │   │
    │   ├─ 16. 添加渲染通道
    │   │   └─ renderer.AddRenderPasses()
    │   │
    │   └─ 17. 执行渲染
    │       ├─ [Render Graph 路径]
    │       │   ├─ RecordAndExecuteRenderGraph
    │       │   └─ FinishRenderGraphRendering
    │       │
    │       └─ [传统路径]
    │           ├─ renderer.Setup()
    │           └─ renderer.Execute()
    │               └─ 执行所有渲染通道
    │
    ├─ 18. 执行最终命令并释放资源
    │
    ├─ 19. 提交渲染上下文
    │   └─ context.Submit() → GPU 开始渲染
    │
    └─ 20. 清除当前渲染器引用
        └─ ScriptableRenderer.current = null
```

## 关键概念说明

### CommandBuffer（命令缓冲区）

- **作用**：记录渲染命令，稍后批量执行
- **优势**：减少 CPU-GPU 通信开销
- **生命周期**：从对象池获取，使用后释放回对象池

### ScriptableRenderContext（可编程渲染上下文）

- **作用**：Unity 提供的渲染上下文，用于记录和执行渲染命令
- **特点**：命令是延迟执行的，直到调用 `Submit()` 才真正提交到 GPU

### 剔除（Culling）

- **作用**：确定哪些物体在相机视野内，需要被渲染
- **类型**：
  - 视锥体剔除（Frustum Culling）
  - 遮挡剔除（Occlusion Culling）
  - 层级剔除（Layer Culling）
- **性能影响**：有效的剔除可以大幅减少渲染工作量

### 渲染通道（Render Pass）

- **作用**：渲染管线的基本执行单元
- **类型**：
  - 阴影通道（Shadow Pass）
  - 深度通道（Depth Pass）
  - 不透明物体通道（Opaque Pass）
  - 透明物体通道（Transparent Pass）
  - 后处理通道（Post-processing Pass）
- **执行顺序**：按照 `RenderPassEvent` 定义的顺序执行

### Render Graph

- **作用**：Unity 的现代渲染图系统
- **优势**：
  - 自动管理资源生命周期
  - 优化资源分配和释放
  - 更好的并行执行支持
  - 减少内存使用

## 性能优化要点

1. **命令缓冲区池化**：使用对象池避免分配开销
2. **剔除优化**：有效的剔除可以大幅减少渲染工作量
3. **延迟执行**：命令先记录，后批量执行，减少 CPU-GPU 通信
4. **资源复用**：RTHandle 系统管理渲染目标，避免频繁分配
5. **性能分析**：使用 ProfilingScope 识别性能瓶颈

## 总结

`RenderSingleCamera` 是 URP 渲染单个相机的核心方法，它负责：

1. ✅ **准备阶段**：验证参数、获取剔除参数、设置渲染环境
2. ✅ **剔除阶段**：确定需要渲染的物体
3. ✅ **收集阶段**：收集所有需要执行的渲染通道
4. ✅ **执行阶段**：按顺序执行所有渲染通道
5. ✅ **提交阶段**：将命令提交到 GPU 执行
6. ✅ **清理阶段**：释放资源，清理状态

整个流程设计合理，既保证了功能的完整性，又考虑了性能优化，是学习 URP 渲染管线的核心入口点。

