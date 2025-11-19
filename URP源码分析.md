# Unity URP 源码分析与渲染管线流程

## 一、URP 架构概述

### 1.1 核心类结构

URP (Universal Render Pipeline) 是 Unity 的可编程渲染管线 (SRP - Scriptable Render Pipeline)，主要包含以下核心类：

- **UniversalRenderPipeline**: 管线主入口类，继承自 `RenderPipeline`
- **ScriptableRenderer**: 抽象渲染器基类，定义渲染策略
- **UniversalRenderer**: URP 的默认渲染器实现，继承自 `ScriptableRenderer`
- **ScriptableRenderPass**: 可编程渲染通道基类
- **RenderingData**: 渲染数据容器，包含相机数据、剔除结果等

### 1.2 版本信息

当前分析版本：**14.0.11** (Unity 2022.3)

## 二、渲染管线流程

### 2.1 主要渲染入口

```19:19:Assets/com.unity.render-pipelines.universal@14.0.11/Runtime/UniversalRenderPipeline.cs
    public sealed partial class UniversalRenderPipeline : RenderPipeline
```

URP 的渲染流程从 `Render()` 方法开始：

```299:412:Assets/com.unity.render-pipelines.universal@14.0.11/Runtime/UniversalRenderPipeline.cs
        protected override void Render(ScriptableRenderContext renderContext, List<Camera> cameras)
#else
        /// <inheritdoc/>
        protected override void Render(ScriptableRenderContext renderContext, Camera[] cameras)
#endif
        {
#if RENDER_GRAPH_ENABLED
            useRenderGraph = asset.enableRenderGraph;
#else
            useRenderGraph = false;
#endif

            SetHDRState(cameras);

            // When HDR is active we render UI overlay per camera as we want all UI to be calibrated to white paper inside a single pass
            // for performance reasons otherwise we render UI overlay after all camera
            SupportedRenderingFeatures.active.rendersUIOverlay = HDROutputForAnyDisplayIsActive();

            // TODO: Would be better to add Profiling name hooks into RenderPipelineManager.
            // C#8 feature, only in >= 2020.2
            using var profScope = new ProfilingScope(null, ProfilingSampler.Get(URPProfileId.UniversalRenderTotal));

#if UNITY_2021_1_OR_NEWER
            using (new ProfilingScope(null, Profiling.Pipeline.beginContextRendering))
            {
                BeginContextRendering(renderContext, cameras);
            }
#else
            using (new ProfilingScope(null, Profiling.Pipeline.beginFrameRendering))
            {
                BeginFrameRendering(renderContext, cameras);
            }
#endif

            GraphicsSettings.lightsUseLinearIntensity = (QualitySettings.activeColorSpace == ColorSpace.Linear);
            GraphicsSettings.lightsUseColorTemperature = true;
            GraphicsSettings.defaultRenderingLayerMask = k_DefaultRenderingLayerMask;
            SetupPerFrameShaderConstants();
            XRSystem.SetDisplayMSAASamples((MSAASamples)asset.msaaSampleCount);

#if UNITY_EDITOR
            // We do not want to start rendering if URP global settings are not ready (m_globalSettings is null)
            // or been deleted/moved (m_globalSettings is not necessarily null)
            if (m_GlobalSettings == null || UniversalRenderPipelineGlobalSettings.instance == null)
            {
                m_GlobalSettings = UniversalRenderPipelineGlobalSettings.Ensure();
                if (m_GlobalSettings == null) return;
            }
#endif

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (DebugManager.instance.isAnyDebugUIActive)
                UniversalRenderPipelineDebugDisplaySettings.Instance.UpdateFrameTiming();
#endif

            // URP uses the camera's allowDynamicResolution flag to decide if useDynamicScale should be enabled for camera render targets.
            // However, the RTHandle system has an additional setting that controls if useDynamicScale will be set for render targets allocated via RTHandles.
            // In order to avoid issues at runtime, we must make the RTHandle system setting consistent with URP's logic. URP already synchronizes the setting
            // during initialization, but unfortunately it's possible for external code to overwrite the setting due to RTHandle state being global.
            // The best we can do to avoid errors in this situation is to ensure the state is set to the correct value every time we perform rendering.
            RTHandles.SetHardwareDynamicResolutionState(true);

            SortCameras(cameras);
#if UNITY_2021_1_OR_NEWER
            for (int i = 0; i < cameras.Count; ++i)
#else
            for (int i = 0; i < cameras.Length; ++i)
#endif
            {
                var camera = cameras[i];
                if (IsGameCamera(camera))
                {
                    RenderCameraStack(renderContext, camera);
                }
                else
                {
                    using (new ProfilingScope(null, Profiling.Pipeline.beginCameraRendering))
                    {
                        BeginCameraRendering(renderContext, camera);
                    }
#if VISUAL_EFFECT_GRAPH_0_0_1_OR_NEWER
                    //It should be called before culling to prepare material. When there isn't any VisualEffect component, this method has no effect.
                    VFX.VFXManager.PrepareCamera(camera);
#endif
                    UpdateVolumeFramework(camera, null);

                    RenderSingleCameraInternal(renderContext, camera);

                    using (new ProfilingScope(null, Profiling.Pipeline.endCameraRendering))
                    {
                        EndCameraRendering(renderContext, camera);
                    }
                }
            }

            s_RenderGraph.EndFrame();
            s_RTHandlePool.PurgeUnusedResources(Time.frameCount);

#if UNITY_2021_1_OR_NEWER
            using (new ProfilingScope(null, Profiling.Pipeline.endContextRendering))
            {
                EndContextRendering(renderContext, cameras);
            }
#else
            using (new ProfilingScope(null, Profiling.Pipeline.endFrameRendering))
            {
                EndFrameRendering(renderContext, cameras);
            }
#endif

#if ENABLE_SHADER_DEBUG_PRINT
            ShaderDebugPrintManager.instance.EndFrame();
#endif
        }
```

### 2.2 单相机渲染流程

核心的单相机渲染方法 `RenderSingleCameraInternal`:

```562:710:Assets/com.unity.render-pipelines.universal@14.0.11/Runtime/UniversalRenderPipeline.cs
        internal static void RenderSingleCameraInternal(ScriptableRenderContext context, Camera camera, ref UniversalAdditionalCameraData additionalCameraData)
        {
            if (additionalCameraData != null && additionalCameraData.renderType != CameraRenderType.Base)
            {
                Debug.LogWarning("Only Base cameras can be rendered with standalone RenderSingleCamera. Camera will be skipped.");
                return;
            }

            InitializeCameraData(camera, additionalCameraData, true, out var cameraData);
            InitializeAdditionalCameraData(camera, additionalCameraData, true, ref cameraData);
#if ADAPTIVE_PERFORMANCE_2_0_0_OR_NEWER
            if (asset.useAdaptivePerformance)
                ApplyAdaptivePerformance(ref cameraData);
#endif
            RenderSingleCamera(context, ref cameraData);
        }

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

### 2.3 渲染器执行流程

ScriptableRenderer 的 Execute 方法是渲染通道执行的核心：

```1098:1191:Assets/com.unity.render-pipelines.universal@14.0.11/Runtime/ScriptableRenderer.cs
        public void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            // Disable Gizmos when using scene overrides. Gizmos break some effects like Overdraw debug.
            bool drawGizmos = UniversalRenderPipelineDebugDisplaySettings.Instance.renderingSettings.sceneOverrideMode == DebugSceneOverrideMode.None;

            hasReleasedRTs = false;
            m_IsPipelineExecuting = true;
            ref CameraData cameraData = ref renderingData.cameraData;
            Camera camera = cameraData.camera;

            // Let renderer features call their own setup functions when targets are valid
            if (rendererFeatures.Count != 0 && !renderingData.cameraData.isPreviewCamera)
                SetupRenderPasses(in renderingData);

            CommandBuffer cmd = CommandBufferPool.Get();

            // TODO: move skybox code from C++ to URP in order to remove the call to context.Submit() inside DrawSkyboxPass
            // Until then, we can't use nested profiling scopes with XR multipass
            CommandBuffer cmdScope = renderingData.cameraData.xr.enabled ? null : cmd;

            using (new ProfilingScope(cmdScope, profilingExecute))
            {
                InternalStartRendering(context, ref renderingData);

                // Cache the time for after the call to `SetupCameraProperties` and set the time variables in shader
                // For now we set the time variables per camera, as we plan to remove `SetupCameraProperties`.
                // Setting the time per frame would take API changes to pass the variable to each camera render.
                // Once `SetupCameraProperties` is gone, the variable should be set higher in the call-stack.
#if UNITY_EDITOR
                float time = Application.isPlaying ? Time.time : Time.realtimeSinceStartup;
#else
                float time = Time.time;
#endif
                float deltaTime = Time.deltaTime;
                float smoothDeltaTime = Time.smoothDeltaTime;

                // Initialize Camera Render State
                ClearRenderingState(cmd);
                SetShaderTimeValues(cmd, time, deltaTime, smoothDeltaTime);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                using (new ProfilingScope(null, Profiling.sortRenderPasses))
                {
                    // Sort the render pass queue
                    SortStable(m_ActiveRenderPassQueue);

                }

                using (new ProfilingScope(null, Profiling.RenderPass.configure))
                {
                    foreach (var pass in activeRenderPassQueue)
                        pass.Configure(cmd, cameraData.cameraTargetDescriptor);

                    context.ExecuteCommandBuffer(cmd);
                    cmd.Clear();
                }

                SetupNativeRenderPassFrameData(ref cameraData, useRenderPassEnabled);

                using var renderBlocks = new RenderBlocks(m_ActiveRenderPassQueue);

                using (new ProfilingScope(null, Profiling.setupLights))
                {
                    SetupLights(context, ref renderingData);
                }

#if VISUAL_EFFECT_GRAPH_0_0_1_OR_NEWER
                using (new ProfilingScope(null, Profiling.setupCamera))
                {
                    //Camera variables need to be setup for the VFXManager.ProcessCameraCommand to work properly.
                    //VFXManager.ProcessCameraCommand needs to be called before any rendering (incl. shadows)
                    SetPerCameraProperties(context, ref cameraData, camera, cmd);

                    //Triggers dispatch per camera, all global parameters should have been setup at this stage.
                    VFX.VFXManager.ProcessCameraCommand(camera, cmd, new VFX.VFXCameraXRSettings(), renderingData.cullResults);

                    // Force execution of the command buffer, to ensure that it is run before "BeforeRendering", (which renders shadow maps)
                    // This is needed in 2022.2 because they are using different command buffers, but not in latest versions.
                    context.ExecuteCommandBuffer(cmd);
                    cmd.Clear();
                }
#endif

                // Before Render Block. This render blocks always execute in mono rendering.
                // Camera is not setup.
                // Used to render input textures like shadowmaps.
                if (renderBlocks.GetLength(RenderPassBlock.BeforeRendering) > 0)
                {
                    // TODO: Separate command buffers per pass break the profiling scope order/hierarchy.
                    // If a single buffer is used and passed as a param to passes,
                    // put all of the "block" scopes back into the command buffer. (null -> cmd)
                    using var profScope = new ProfilingScope(null, Profiling.RenderBlock.beforeRendering);
                    ExecuteBlock(RenderPassBlock.BeforeRendering, in renderBlocks, context, ref renderingData);
                }
```

## 三、渲染通道 (Render Pass) 系统

### 3.1 渲染通道事件顺序

URP 定义了多个渲染通道事件，按顺序执行：

```51:100:Assets/com.unity.render-pipelines.universal@14.0.11/Runtime/Passes/ScriptableRenderPass.cs
    public enum RenderPassEvent
    {
        /// <summary>
        /// Executes a <c>ScriptableRenderPass</c> before rendering any other passes in the pipeline.
        /// Camera matrices and stereo rendering are not setup this point.
        /// You can use this to draw to custom input textures used later in the pipeline, f.ex LUT textures.
        /// </summary>
        BeforeRendering = 0,

        /// <summary>
        /// Executes a <c>ScriptableRenderPass</c> before rendering shadowmaps.
        /// Camera matrices and stereo rendering are not setup this point.
        /// </summary>
        BeforeRenderingShadows = 50,

        /// <summary>
        /// Executes a <c>ScriptableRenderPass</c> after rendering shadowmaps.
        /// Camera matrices and stereo rendering are not setup this point.
        /// </summary>
        AfterRenderingShadows = 100,

        /// <summary>
        /// Executes a <c>ScriptableRenderPass</c> before rendering prepasses, f.ex, depth prepass.
        /// Camera matrices and stereo rendering are already setup at this point.
        /// </summary>
        BeforeRenderingPrePasses = 150,

        /// <summary>
        /// Executes a <c>ScriptableRenderPass</c> after rendering prepasses, f.ex, depth prepass.
        /// Camera matrices and stereo rendering are already setup at this point.
        /// </summary>
        AfterRenderingPrePasses = 200,

        /// <summary>
        /// Executes a <c>ScriptableRenderPass</c> before rendering gbuffer pass.
        /// </summary>
        BeforeRenderingGbuffer = 210,

        /// <summary>
        /// Executes a <c>ScriptableRenderPass</c> after rendering gbuffer pass.
        /// </summary>
        AfterRenderingGbuffer = 220,

        /// <summary>
        /// Executes a <c>ScriptableRenderPass</c> before rendering deferred shading pass.
        /// </summary>
        BeforeRenderingDeferredLights = 230,
```

### 3.2 渲染通道块 (Render Pass Blocks)

渲染通道被组织成四个主要块：

```524:537:Assets/com.unity.render-pipelines.universal@14.0.11/Runtime/ScriptableRenderer.cs
        static class RenderPassBlock
        {
            // Executes render passes that are inputs to the main rendering
            // but don't depend on camera state. They all render in monoscopic mode. f.ex, shadow maps.
            public static readonly int BeforeRendering = 0;

            // Main bulk of render pass execution. They required camera state to be properly set
            // and when enabled they will render in stereo.
            public static readonly int MainRenderingOpaque = 1;
            public static readonly int MainRenderingTransparent = 2;

            // Execute after Post-processing.
            public static readonly int AfterRendering = 3;
        }
```

### 3.3 UniversalRenderer 的渲染通道

UniversalRenderer 初始化时创建了多个内置渲染通道：

```236:324:Assets/com.unity.render-pipelines.universal@14.0.11/Runtime/UniversalRenderer.cs
            // Note: Since all custom render passes inject first and we have stable sort,
            // we inject the builtin passes in the before events.
            m_MainLightShadowCasterPass = new MainLightShadowCasterPass(RenderPassEvent.BeforeRenderingShadows);
            m_AdditionalLightsShadowCasterPass = new AdditionalLightsShadowCasterPass(RenderPassEvent.BeforeRenderingShadows);

#if ENABLE_VR && ENABLE_XR_MODULE
            m_XROcclusionMeshPass = new XROcclusionMeshPass(RenderPassEvent.BeforeRenderingOpaques);
            // Schedule XR copydepth right after m_FinalBlitPass
            m_XRCopyDepthPass = new CopyDepthPass(RenderPassEvent.AfterRendering + k_AfterFinalBlitPassQueueOffset, m_CopyDepthMaterial);
#endif
            m_DepthPrepass = new DepthOnlyPass(RenderPassEvent.BeforeRenderingPrePasses, RenderQueueRange.opaque, data.opaqueLayerMask);
            m_DepthNormalPrepass = new DepthNormalOnlyPass(RenderPassEvent.BeforeRenderingPrePasses, RenderQueueRange.opaque, data.opaqueLayerMask);

            if (renderingModeRequested == RenderingMode.Forward || renderingModeRequested == RenderingMode.ForwardPlus)
            {
                m_PrimedDepthCopyPass = new CopyDepthPass(RenderPassEvent.AfterRenderingPrePasses, m_CopyDepthMaterial, true);
            }

            if (this.renderingModeRequested == RenderingMode.Deferred)
            {
                var deferredInitParams = new DeferredLights.InitParams();
                deferredInitParams.stencilDeferredMaterial = m_StencilDeferredMaterial;
                deferredInitParams.lightCookieManager = m_LightCookieManager;
                m_DeferredLights = new DeferredLights(deferredInitParams, useRenderPassEnabled);
                m_DeferredLights.AccurateGbufferNormals = data.accurateGbufferNormals;

                m_GBufferPass = new GBufferPass(RenderPassEvent.BeforeRenderingGbuffer, RenderQueueRange.opaque, data.opaqueLayerMask, m_DefaultStencilState, stencilData.stencilReference, m_DeferredLights);
                // Forward-only pass only runs if deferred renderer is enabled.
                // It allows specific materials to be rendered in a forward-like pass.
                // We render both gbuffer pass and forward-only pass before the deferred lighting pass so we can minimize copies of depth buffer and
                // benefits from some depth rejection.
                // - If a material can be rendered either forward or deferred, then it should declare a UniversalForward and a UniversalGBuffer pass.
                // - If a material cannot be lit in deferred (unlit, bakedLit, special material such as hair, skin shader), then it should declare UniversalForwardOnly pass
                // - Legacy materials have unamed pass, which is implicitely renamed as SRPDefaultUnlit. In that case, they are considered forward-only too.
                // TO declare a material with unnamed pass and UniversalForward/UniversalForwardOnly pass is an ERROR, as the material will be rendered twice.
                StencilState forwardOnlyStencilState = DeferredLights.OverwriteStencil(m_DefaultStencilState, (int)StencilUsage.MaterialMask);
                ShaderTagId[] forwardOnlyShaderTagIds = new ShaderTagId[]
                {
                    new ShaderTagId("UniversalForwardOnly"),
                    new ShaderTagId("SRPDefaultUnlit"), // Legacy shaders (do not have a gbuffer pass) are considered forward-only for backward compatibility
                    new ShaderTagId("LightweightForward") // Legacy shaders (do not have a gbuffer pass) are considered forward-only for backward compatibility
                };
                int forwardOnlyStencilRef = stencilData.stencilReference | (int)StencilUsage.MaterialUnlit;
                m_GBufferCopyDepthPass = new CopyDepthPass(RenderPassEvent.BeforeRenderingGbuffer + 1, m_CopyDepthMaterial, true);
                m_DeferredPass = new DeferredPass(RenderPassEvent.BeforeRenderingDeferredLights, m_DeferredLights);
                m_RenderOpaqueForwardOnlyPass = new DrawObjectsPass("Render Opaques Forward Only", forwardOnlyShaderTagIds, true, RenderPassEvent.BeforeRenderingOpaques, RenderQueueRange.opaque, data.opaqueLayerMask, forwardOnlyStencilState, forwardOnlyStencilRef);
            }

            // Always create this pass even in deferred because we use it for wireframe rendering in the Editor or offscreen depth texture rendering.
            m_RenderOpaqueForwardPass = new DrawObjectsPass(URPProfileId.DrawOpaqueObjects, true, RenderPassEvent.BeforeRenderingOpaques, RenderQueueRange.opaque, data.opaqueLayerMask, m_DefaultStencilState, stencilData.stencilReference);
            m_RenderOpaqueForwardWithRenderingLayersPass = new DrawObjectsWithRenderingLayersPass(URPProfileId.DrawOpaqueObjects, true, RenderPassEvent.BeforeRenderingOpaques, RenderQueueRange.opaque, data.opaqueLayerMask, m_DefaultStencilState, stencilData.stencilReference);

            bool copyDepthAfterTransparents = m_CopyDepthMode == CopyDepthMode.AfterTransparents;
            RenderPassEvent copyDepthEvent = copyDepthAfterTransparents ? RenderPassEvent.AfterRenderingTransparents : RenderPassEvent.AfterRenderingSkybox;

            m_CopyDepthPass = new CopyDepthPass(
                copyDepthEvent,
                m_CopyDepthMaterial,
                shouldClear: true,
                copyResolvedDepth: RenderingUtils.MultisampleDepthResolveSupported() && SystemInfo.supportsMultisampleAutoResolve && copyDepthAfterTransparents);

            // Motion vectors depend on the (copy) depth texture. Depth is reprojected to calculate motion vectors.
            m_MotionVectorPass = new MotionVectorRenderPass(copyDepthEvent + 1, m_CameraMotionVecMaterial, m_ObjectMotionVecMaterial, data.opaqueLayerMask);

            m_DrawSkyboxPass = new DrawSkyboxPass(RenderPassEvent.BeforeRenderingSkybox);
            m_CopyColorPass = new CopyColorPass(RenderPassEvent.AfterRenderingSkybox, m_SamplingMaterial, m_BlitMaterial);
#if ADAPTIVE_PERFORMANCE_2_1_0_OR_NEWER
            if (needTransparencyPass)
#endif
            {
                m_TransparentSettingsPass = new TransparentSettingsPass(RenderPassEvent.BeforeRenderingTransparents, data.shadowTransparentReceive);
                m_RenderTransparentForwardPass = new DrawObjectsPass(URPProfileId.DrawTransparentObjects, false, RenderPassEvent.BeforeRenderingTransparents, RenderQueueRange.transparent, data.transparentLayerMask, m_DefaultStencilState, stencilData.stencilReference);
            }
            m_OnRenderObjectCallbackPass = new InvokeOnRenderObjectCallbackPass(RenderPassEvent.BeforeRenderingPostProcessing);

            m_DrawOffscreenUIPass = new DrawScreenSpaceUIPass(RenderPassEvent.BeforeRenderingPostProcessing, true);
            m_DrawOverlayUIPass = new DrawScreenSpaceUIPass(RenderPassEvent.AfterRendering + k_AfterFinalBlitPassQueueOffset, false); // after m_FinalBlitPass

            {
                var postProcessParams = PostProcessParams.Create();
                postProcessParams.blitMaterial = m_BlitMaterial;
                postProcessParams.requestHDRFormat = GraphicsFormat.B10G11R11_UFloatPack32;
                var asset = UniversalRenderPipeline.asset;
                if (asset)
                    postProcessParams.requestHDRFormat = UniversalRenderPipeline.MakeRenderTextureGraphicsFormat(asset.supportsHDR, asset.hdrColorBufferPrecision, false);

                m_PostProcessPasses = new PostProcessPasses(data.postProcessData, ref postProcessParams);
            }

            m_CapturePass = new CapturePass(RenderPassEvent.AfterRendering);
            m_FinalBlitPass = new FinalBlitPass(RenderPassEvent.AfterRendering + k_FinalBlitPassQueueOffset, m_BlitMaterial, m_BlitHDRMaterial);
```

## 四、渲染模式

### 4.1 支持的渲染模式

UniversalRenderer 支持三种渲染模式：

```11:20:Assets/com.unity.render-pipelines.universal@14.0.11/Runtime/UniversalRenderer.cs
    public enum RenderingMode
    {
        /// <summary>Render all objects and lighting in one pass, with a hard limit on the number of lights that can be applied on an object.</summary>
        Forward = 0,
        /// <summary>Render all objects and lighting in one pass using a clustered data structure to access lighting data.</summary>
        [InspectorName("Forward+")]
        ForwardPlus = 2,
        /// <summary>Render all objects first in a g-buffer pass, then apply all lighting in a separate pass using deferred shading.</summary>
        Deferred = 1
    };
```

### 4.2 实际渲染模式

实际使用的渲染模式可能因硬件限制或调试模式而改变：

```78:83:Assets/com.unity.render-pipelines.universal@14.0.11/Runtime/UniversalRenderer.cs
        // Actual rendering mode, which may be different (ex: wireframe rendering, hardware not capable of deferred rendering).
        internal RenderingMode renderingModeActual => renderingModeRequested == RenderingMode.Deferred && (GL.wireframe || (DebugHandler != null && DebugHandler.IsActiveModeUnsupportedForDeferred) || m_DeferredLights == null || !m_DeferredLights.IsRuntimeSupportedThisFrame() || m_DeferredLights.IsOverlay)
        ? RenderingMode.Forward
        : this.renderingModeRequested;
```

## 五、核心渲染流程总结

### 5.1 完整渲染流程

1. **初始化阶段**
   - 设置 HDR 状态
   - 初始化全局着色器常量
   - 设置渲染目标系统

2. **相机循环**
   - 对相机进行排序
   - 遍历每个相机进行渲染

3. **单相机渲染**
   - **初始化相机数据** (`InitializeCameraData`)
   - **设置剔除参数** (`SetupCullingParameters`)
   - **执行剔除** (`context.Cull`)
   - **初始化渲染数据** (`InitializeRenderingData`)
   - **添加渲染通道** (`AddRenderPasses`)
   - **执行渲染器** (`Execute`)

4. **渲染通道执行** (Execute 方法)
   - **BeforeRendering Block**: 阴影贴图等
   - **设置相机属性**: 相机矩阵、着色器变量
   - **设置光照**: 主光源和附加光源
   - **MainRenderingOpaque Block**: 不透明物体渲染
   - **MainRenderingTransparent Block**: 透明物体渲染
   - **AfterRendering Block**: 后处理、最终输出

5. **提交渲染**
   - 执行命令缓冲区
   - 提交到 GPU

### 5.2 关键数据结构

- **CameraData**: 包含相机信息、渲染目标描述符等
- **RenderingData**: 包含相机数据、剔除结果、命令缓冲区等
- **CullingResults**: 剔除操作的结果
- **RTHandle**: 渲染目标句柄，用于管理渲染纹理

### 5.3 渲染通道顺序 (Forward 模式示例)

1. BeforeRenderingShadows: 主光源阴影贴图
2. BeforeRenderingShadows: 附加光源阴影贴图
3. BeforeRenderingPrePasses: 深度预通道 (可选)
4. BeforeRenderingOpaques: 不透明物体渲染
5. BeforeRenderingSkybox: 天空盒渲染
6. BeforeRenderingTransparents: 透明物体渲染
7. BeforeRenderingPostProcessing: 后处理
8. AfterRendering: 最终输出

## 六、重要特性

### 6.1 Render Graph 支持

URP 14.0 支持 Render Graph，可以更高效地管理渲染资源：

```680:692:Assets/com.unity.render-pipelines.universal@14.0.11/Runtime/UniversalRenderPipeline.cs
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
```

### 6.2 相机堆叠 (Camera Stack)

支持相机堆叠，可以实现多层次的渲染效果。

### 6.3 光照系统

- **ForwardLights**: 前向渲染光照管理
- **DeferredLights**: 延迟渲染光照管理
- **LightCookieManager**: 光照 Cookie 管理

## 七、性能优化要点

1. **SRP Batcher**: 通过 `useSRPBatcher` 启用
2. **RTHandle 系统**: 动态管理渲染目标，减少内存分配
3. **Render Pass 排序**: 稳定排序确保渲染顺序正确
4. **剔除优化**: 使用 ScriptableCullingParameters 进行高效剔除
5. **资源池化**: CommandBufferPool 等资源池化机制

## 八、扩展性

### 8.1 自定义渲染通道

通过继承 `ScriptableRenderPass` 创建自定义渲染通道：

```12:43:Assets/com.unity.render-pipelines.universal@14.0.11/Runtime/Passes/ScriptableRenderPass.cs
    /// <summary>
    /// Input requirements for <c>ScriptableRenderPass</c>.
    /// </summary>
    /// <seealso cref="ConfigureInput"/>
    [Flags]
    public enum ScriptableRenderPassInput
    {
        /// <summary>
        /// Used when a <c>ScriptableRenderPass</c> does not require any texture.
        /// </summary>
        None = 0,

        /// <summary>
        /// Used when a <c>ScriptableRenderPass</c> requires a depth texture.
        /// </summary>
        Depth = 1 << 0,

        /// <summary>
        /// Used when a <c>ScriptableRenderPass</c> requires a normal texture.
        /// </summary>
        Normal = 1 << 1,

        /// <summary>
        /// Used when a <c>ScriptableRenderPass</c> requires a color texture.
        /// </summary>
        Color = 1 << 2,

        /// <summary>
        /// Used when a <c>ScriptableRenderPass</c> requires a motion vectors texture.
        /// </summary>
        Motion = 1 << 3,
    }
```

### 8.2 渲染器特性

通过 `ScriptableRendererFeature` 扩展渲染器功能。

## 九、相机渲染类型详解 (CameraRenderType)

### 9.1 Base 和 Overlay 的区别

URP 支持两种相机渲染类型，用于实现**相机堆叠 (Camera Stacking)** 功能：

#### Base (基础相机)

```89:99:Assets/com.unity.render-pipelines.universal@14.0.11/Runtime/UniversalAdditionalCameraData.cs
    /// <summary>
    /// Holds information about the render type of a camera. Options are Base or Overlay.
    /// Base rendering type allows the camera to render to either the screen or to a texture.
    /// Overlay rendering type allows the camera to render on top of a previous camera output, thus compositing camera results.
    /// </summary>
    public enum CameraRenderType
    {
        /// <summary>
        /// Use this to select the base camera render type.
        /// Base rendering type allows the camera to render to either the screen or to a texture.
        /// </summary>
        Base,
```

**Base 相机的特点：**

1. **独立渲染**: 可以独立渲染到屏幕或纹理
2. **完整渲染流程**: 执行完整的渲染管线（阴影、深度、后处理等）
3. **相机堆叠基础**: 作为相机堆叠的底层，可以拥有 Overlay 相机堆叠
4. **清除操作**: 会自动清除颜色和深度缓冲区（除非使用相机堆叠）

#### Overlay (叠加相机)

```101:105:Assets/com.unity.render-pipelines.universal@14.0.11/Runtime/UniversalAdditionalCameraData.cs
        /// <summary>
        /// Use this to select the overlay camera render type.
        /// Overlay rendering type allows the camera to render on top of a previous camera output, thus compositing camera results.
        /// </summary>
        Overlay,
    }
```

**Overlay 相机的特点：**

1. **叠加渲染**: 渲染在 Base 相机（或其他 Overlay 相机）的输出之上
2. **合成效果**: 通过 Alpha 混合等方式与之前的渲染结果合成
3. **依赖 Base**: 必须添加到 Base 相机的 `cameraStack` 中才能被渲染
4. **可选清除**: 可以设置是否清除深度缓冲区（`clearDepth`）

### 9.2 相机堆叠的工作原理

相机堆叠的实现流程：

```718:875:Assets/com.unity.render-pipelines.universal@14.0.11/Runtime/UniversalRenderPipeline.cs
        static void RenderCameraStack(ScriptableRenderContext context, Camera baseCamera)
        {
            using var profScope = new ProfilingScope(null, ProfilingSampler.Get(URPProfileId.RenderCameraStack));

            baseCamera.TryGetComponent<UniversalAdditionalCameraData>(out var baseCameraAdditionalData);

            // Overlay cameras will be rendered stacked while rendering base cameras
            if (baseCameraAdditionalData != null && baseCameraAdditionalData.renderType == CameraRenderType.Overlay)
                return;

            // Renderer contains a stack if it has additional data and the renderer supports stacking
            // The renderer is checked if it supports Base camera. Since Base is the only relevant type at this moment.
            var renderer = baseCameraAdditionalData?.scriptableRenderer;
            bool supportsCameraStacking = renderer != null && renderer.SupportsCameraStackingType(CameraRenderType.Base);
            List<Camera> cameraStack = (supportsCameraStacking) ? baseCameraAdditionalData?.cameraStack : null;

            bool anyPostProcessingEnabled = baseCameraAdditionalData != null && baseCameraAdditionalData.renderPostProcessing;
            bool mainHdrDisplayOutputActive = HDROutputForMainDisplayIsActive();

            int rendererCount = asset.m_RendererDataList.Length;

            // We need to know the last active camera in the stack to be able to resolve
            // rendering to screen when rendering it. The last camera in the stack is not
            // necessarily the last active one as it users might disable it.
            int lastActiveOverlayCameraIndex = -1;
            if (cameraStack != null)
            {
                var baseCameraRendererType = baseCameraAdditionalData?.scriptableRenderer.GetType();
                bool shouldUpdateCameraStack = false;

                cameraStackRequiresDepthForPostprocessing = false;

                for (int i = 0; i < cameraStack.Count; ++i)
                {
                    Camera currCamera = cameraStack[i];
                    if (currCamera == null)
                    {
                        shouldUpdateCameraStack = true;
                        continue;
                    }

                    if (currCamera.isActiveAndEnabled)
                    {
                        currCamera.TryGetComponent<UniversalAdditionalCameraData>(out var data);

                        // Checking if the base and the overlay camera is of the same renderer type.
                        var currCameraRendererType = data?.scriptableRenderer.GetType();
                        if (currCameraRendererType != baseCameraRendererType)
                        {
                            Debug.LogWarning("Only cameras with compatible renderer types can be stacked. " +
                                             $"The camera: {currCamera.name} are using the renderer {currCameraRendererType.Name}, " +
                                             $"but the base camera: {baseCamera.name} are using {baseCameraRendererType.Name}. Will skip rendering");
                            continue;
                        }

                        var overlayRenderer = data.scriptableRenderer;
                        // Checking if they are the same renderer type but just not supporting Overlay
                        if ((overlayRenderer.SupportedCameraStackingTypes() & 1 << (int)CameraRenderType.Overlay) == 0)
                        {
                            Debug.LogWarning($"The camera: {currCamera.name} is using a renderer of type {renderer.GetType().Name} which does not support Overlay cameras in it's current state.");
                            continue;
                        }

                        if (data == null || data.renderType != CameraRenderType.Overlay)
                        {
                            Debug.LogWarning($"Stack can only contain Overlay cameras. The camera: {currCamera.name} " +
                                             $"has a type {data.renderType} that is not supported. Will skip rendering.");
                            continue;
                        }

                        cameraStackRequiresDepthForPostprocessing |= CheckPostProcessForDepth();

                        anyPostProcessingEnabled |= data.renderPostProcessing;
                        lastActiveOverlayCameraIndex = i;
                    }
                }
                if (shouldUpdateCameraStack)
                {
                    baseCameraAdditionalData.UpdateCameraStack();
                }
            }

            // Post-processing not supported in GLES2.
            anyPostProcessingEnabled &= SystemInfo.graphicsDeviceType != GraphicsDeviceType.OpenGLES2;

            bool isStackedRendering = lastActiveOverlayCameraIndex != -1;

            // Prepare XR rendering
            var xrActive = false;
            var xrRendering = baseCameraAdditionalData?.allowXRRendering ?? true;
            var xrLayout = XRSystem.NewLayout();
            xrLayout.AddCamera(baseCamera, xrRendering);

            // With XR multi-pass enabled, each camera can be rendered multiple times with different parameters
            foreach ((Camera _, XRPass xrPass) in xrLayout.GetActivePasses())
            {
                if (xrPass.enabled)
                {
                    xrActive = true;
                    UpdateCameraStereoMatrices(baseCamera, xrPass);
                }


                using (new ProfilingScope(null, Profiling.Pipeline.beginCameraRendering))
                {
                    BeginCameraRendering(context, baseCamera);
                }
                // Update volumeframework before initializing additional camera data
                UpdateVolumeFramework(baseCamera, baseCameraAdditionalData);
                InitializeCameraData(baseCamera, baseCameraAdditionalData, !isStackedRendering, out var baseCameraData);
                RenderTextureDescriptor originalTargetDesc = baseCameraData.cameraTargetDescriptor;

#if ENABLE_VR && ENABLE_XR_MODULE
                if (xrPass.enabled)
                {
                    baseCameraData.xr = xrPass;

                    // Helper function for updating cameraData with xrPass Data
                    // Need to update XRSystem using baseCameraData to handle the case where camera position is modified in BeginCameraRendering
                    UpdateCameraData(ref baseCameraData, baseCameraData.xr);

                    // Handle the case where camera position is modified in BeginCameraRendering
                    xrLayout.ReconfigurePass(baseCameraData.xr, baseCamera);
                    XRSystemUniversal.BeginLateLatching(baseCamera, baseCameraData.xrUniversal);
                }
#endif
                // InitializeAdditionalCameraData needs to be initialized after the cameraTargetDescriptor is set because it needs to know the
                // msaa level of cameraTargetDescriptor and XR modifications.
                InitializeAdditionalCameraData(baseCamera, baseCameraAdditionalData, !isStackedRendering, ref baseCameraData);

#if VISUAL_EFFECT_GRAPH_0_0_1_OR_NEWER
                //It should be called before culling to prepare material. When there isn't any VisualEffect component, this method has no effect.
                VFX.VFXManager.PrepareCamera(baseCamera);
#endif
#if ADAPTIVE_PERFORMANCE_2_0_0_OR_NEWER
                if (asset.useAdaptivePerformance)
                    ApplyAdaptivePerformance(ref baseCameraData);
#endif
                // update the base camera flag so that the scene depth is stored if needed by overlay cameras later in the frame
                baseCameraData.postProcessingRequiresDepthTexture |= cameraStackRequiresDepthForPostprocessing;

                // Check whether the camera stack final output is HDR
                // This is equivalent of UniversalCameraData.isHDROutputActive but without necessiting the base camera to be the last camera in the stack.
                bool hdrDisplayOutputActive = mainHdrDisplayOutputActive;
#if ENABLE_VR && ENABLE_XR_MODULE
                // If we are rendering to xr then we need to look at the XR Display rather than the main non-xr display.
                if (xrPass.enabled)
                    hdrDisplayOutputActive = xrPass.isHDRDisplayOutputActive;
#endif
                bool finalOutputHDR = asset.supportsHDR && hdrDisplayOutputActive // Check whether any HDR display is active and the render pipeline asset allows HDR rendering
                    && baseCamera.targetTexture == null && (baseCamera.cameraType == CameraType.Game || baseCamera.cameraType == CameraType.VR) // Check whether the stack outputs to a screen
                    && baseCameraData.allowHDROutput; // Check whether the base camera allows HDR output

                // Update stack-related parameters
                baseCameraData.stackAnyPostProcessingEnabled = anyPostProcessingEnabled;
                baseCameraData.stackLastCameraOutputToHDR = finalOutputHDR;

                RenderSingleCamera(context, ref baseCameraData);
                using (new ProfilingScope(null, Profiling.Pipeline.endCameraRendering))
                {
                    EndCameraRendering(context, baseCamera);
                }
```

**渲染顺序：**

1. **Base 相机先渲染**: 渲染场景的主要内容
2. **Overlay 相机按顺序叠加**: 在 Base 相机渲染完成后，按堆叠顺序依次渲染 Overlay 相机
3. **最终输出**: 最后一个 Overlay 相机（或 Base 相机）的输出作为最终结果

### 9.3 关键限制和规则

1. **相机堆叠只能包含 Overlay 相机**:

```781:786:Assets/com.unity.render-pipelines.universal@14.0.11/Runtime/UniversalRenderPipeline.cs
                        if (data == null || data.renderType != CameraRenderType.Overlay)
                        {
                            Debug.LogWarning($"Stack can only contain Overlay cameras. The camera: {currCamera.name} " +
                                             $"has a type {data.renderType} that is not supported. Will skip rendering.");
                            continue;
                        }
```

2. **只有 Base 相机可以有相机堆叠**:

```425:439:Assets/com.unity.render-pipelines.universal@14.0.11/Runtime/UniversalAdditionalCameraData.cs
        /// <summary>
        /// Returns the camera stack. Only valid for Base cameras.
        /// Will return null if it is not a Base camera.
        /// <seealso cref="CameraRenderType"/>.
        /// </summary>
        public List<Camera> cameraStack
        {
            get
            {
                if (renderType != CameraRenderType.Base)
                {
                    var camera = gameObject.GetComponent<Camera>();
                    Debug.LogWarning(string.Format("{0}: This camera is of {1} type. Only Base cameras can have a camera stack.", camera.name, renderType));
                    return null;
                }
```

3. **Overlay 相机不能单独渲染**:

```564:568:Assets/com.unity.render-pipelines.universal@14.0.11/Runtime/UniversalRenderPipeline.cs
            if (additionalCameraData != null && additionalCameraData.renderType != CameraRenderType.Base)
            {
                Debug.LogWarning("Only Base cameras can be rendered with standalone RenderSingleCamera. Camera will be skipped.");
                return;
            }
```

4. **渲染器类型必须兼容**: Base 和 Overlay 相机必须使用相同的渲染器类型

### 9.4 使用场景示例

**Base 相机**:
- 主游戏相机
- 渲染整个场景
- 可以渲染到屏幕或 RenderTexture

**Overlay 相机**:
- UI 相机（叠加在游戏画面上）
- 特效相机（添加粒子效果、光晕等）
- 小地图相机（叠加在主画面上）
- 后处理效果相机

### 9.5 渲染流程对比

| 特性 | Base 相机 | Overlay 相机 |
|------|-----------|--------------|
| 渲染目标 | 屏幕或纹理 | 必须基于 Base 相机的输出 |
| 清除操作 | 自动清除颜色和深度 | 可选清除深度（clearDepth） |
| 阴影 | 支持 | 通常不支持 |
| 后处理 | 支持 | 支持（但会影响整个堆叠） |
| 相机堆叠 | 可以有 | 不能有 |
| 独立渲染 | 可以 | 不可以 |

## 十、InitializeCameraData 初始化数据详解

### 10.1 方法概述

`InitializeCameraData` 方法负责初始化相机的所有渲染相关数据，为后续的渲染流程做准备。它主要通过调用 `InitializeStackedCameraData` 和 `CreateRenderTextureDescriptor` 来完成初始化。

```1079:1109:Assets/com.unity.render-pipelines.universal@14.0.11/Runtime/UniversalRenderPipeline.cs
        static void InitializeCameraData(Camera camera, UniversalAdditionalCameraData additionalCameraData, bool resolveFinalTarget, out CameraData cameraData)
        {
            using var profScope = new ProfilingScope(null, Profiling.Pipeline.initializeCameraData);

            cameraData = new CameraData();
            InitializeStackedCameraData(camera, additionalCameraData, ref cameraData);

            cameraData.camera = camera;

            ///////////////////////////////////////////////////////////////////
            // Descriptor settings                                            /
            ///////////////////////////////////////////////////////////////////

            var renderer = additionalCameraData?.scriptableRenderer;
            bool rendererSupportsMSAA = renderer != null && renderer.supportedRenderingFeatures.msaa;

            int msaaSamples = 1;
            if (camera.allowMSAA && asset.msaaSampleCount > 1 && rendererSupportsMSAA)
                msaaSamples = (camera.targetTexture != null) ? camera.targetTexture.antiAliasing : asset.msaaSampleCount;

            // Use XR's MSAA if camera is XR camera. XR MSAA needs special handle here because it is not per Camera.
            // Multiple cameras could render into the same XR display and they should share the same MSAA level.
            if (cameraData.xrRendering && rendererSupportsMSAA && camera.targetTexture == null)
                msaaSamples = (int)XRSystem.GetDisplayMSAASamples();

            bool needsAlphaChannel = Graphics.preserveFramebufferAlpha;

            cameraData.hdrColorBufferPrecision = asset ? asset.hdrColorBufferPrecision : HDRColorBufferPrecision._32Bits;
            cameraData.cameraTargetDescriptor = CreateRenderTextureDescriptor(camera, ref cameraData,
                cameraData.isHdrEnabled, cameraData.hdrColorBufferPrecision, msaaSamples, needsAlphaChannel, cameraData.requiresOpaqueTexture);
        }
```

### 10.2 InitializeStackedCameraData 初始化的数据

#### 10.2.1 基础相机属性

```1118:1175:Assets/com.unity.render-pipelines.universal@14.0.11/Runtime/UniversalRenderPipeline.cs
        static void InitializeStackedCameraData(Camera baseCamera, UniversalAdditionalCameraData baseAdditionalCameraData, ref CameraData cameraData)
        {
            using var profScope = new ProfilingScope(null, Profiling.Pipeline.initializeStackedCameraData);

            var settings = asset;
            cameraData.targetTexture = baseCamera.targetTexture;
            cameraData.cameraType = baseCamera.cameraType;
            bool isSceneViewCamera = cameraData.isSceneViewCamera;

            ///////////////////////////////////////////////////////////////////
            // Environment and Post-processing settings                       /
            ///////////////////////////////////////////////////////////////////
            if (isSceneViewCamera)
            {
                cameraData.volumeLayerMask = 1; // "Default"
                cameraData.volumeTrigger = null;
                cameraData.isStopNaNEnabled = false;
                cameraData.isDitheringEnabled = false;
                cameraData.antialiasing = AntialiasingMode.None;
                cameraData.antialiasingQuality = AntialiasingQuality.High;
                cameraData.xrRendering = false;
                cameraData.allowHDROutput = false;
            }
            else if (baseAdditionalCameraData != null)
            {
                cameraData.volumeLayerMask = baseAdditionalCameraData.volumeLayerMask;
                cameraData.volumeTrigger = baseAdditionalCameraData.volumeTrigger == null ? baseCamera.transform : baseAdditionalCameraData.volumeTrigger;
                cameraData.isStopNaNEnabled = baseAdditionalCameraData.stopNaN && SystemInfo.graphicsShaderLevel >= 35;
                cameraData.isDitheringEnabled = baseAdditionalCameraData.dithering;
                cameraData.antialiasing = baseAdditionalCameraData.antialiasing;
                cameraData.antialiasingQuality = baseAdditionalCameraData.antialiasingQuality;
                cameraData.xrRendering = baseAdditionalCameraData.allowXRRendering && XRSystem.displayActive;
                cameraData.allowHDROutput = baseAdditionalCameraData.allowHDROutput;
            }
            else
            {
                cameraData.volumeLayerMask = 1; // "Default"
                cameraData.volumeTrigger = null;
                cameraData.isStopNaNEnabled = false;
                cameraData.isDitheringEnabled = false;
                cameraData.antialiasing = AntialiasingMode.None;
                cameraData.antialiasingQuality = AntialiasingQuality.High;
                cameraData.xrRendering = XRSystem.displayActive;
                cameraData.allowHDROutput = true;
            }

            ///////////////////////////////////////////////////////////////////
            // Settings that control output of the camera                     /
            ///////////////////////////////////////////////////////////////////

            cameraData.isHdrEnabled = baseCamera.allowHDR && settings.supportsHDR;
            cameraData.allowHDROutput &= settings.supportsHDR;

            Rect cameraRect = baseCamera.rect;
            cameraData.pixelRect = baseCamera.pixelRect;
            cameraData.pixelWidth = baseCamera.pixelWidth;
            cameraData.pixelHeight = baseCamera.pixelHeight;
            cameraData.aspectRatio = (float)cameraData.pixelWidth / (float)cameraData.pixelHeight;
            cameraData.isDefaultViewport = (!(Math.Abs(cameraRect.x) > 0.0f || Math.Abs(cameraRect.y) > 0.0f ||
                Math.Abs(cameraRect.width) < 1.0f || Math.Abs(cameraRect.height) < 1.0f));
```

**初始化的基础属性：**
- `targetTexture`: 渲染目标纹理（从 Camera 获取）
- `cameraType`: 相机类型（Game、SceneView、Preview 等）

#### 10.2.2 环境与后处理设置

**Volume 系统设置：**
- `volumeLayerMask`: Volume 层级遮罩（用于 Volume 系统）
- `volumeTrigger`: Volume 触发器（Transform，用于本地 Volume）

**后处理特性：**
- `isStopNaNEnabled`: 是否启用 NaN 停止（防止 NaN 值传播）
- `isDitheringEnabled`: 是否启用抖动（颜色抖动）
- `antialiasing`: 抗锯齿模式（None、FXAA、SMAA、TAA）
- `antialiasingQuality`: 抗锯齿质量（Low、Medium、High）

**XR 和 HDR：**
- `xrRendering`: 是否启用 XR 渲染
- `allowHDROutput`: 是否允许 HDR 输出

#### 10.2.3 渲染输出控制设置

**像素和视口信息：**
- `pixelRect`: 像素矩形（视口在屏幕像素坐标中的位置和大小）
- `pixelWidth`: 像素宽度
- `pixelHeight`: 像素高度
- `aspectRatio`: 宽高比
- `isDefaultViewport`: 是否为默认视口（全屏）

**HDR 设置：**
- `isHdrEnabled`: 是否启用 HDR（基于相机设置和管线资产支持）

#### 10.2.4 渲染缩放和上采样设置

```1181:1208:Assets/com.unity.render-pipelines.universal@14.0.11/Runtime/UniversalRenderPipeline.cs
            // Discard variations lesser than kRenderScaleThreshold.
            // Scale is only enabled for gameview.
            const float kRenderScaleThreshold = 0.05f;
            bool disableRenderScale = ((Mathf.Abs(1.0f - settings.renderScale) < kRenderScaleThreshold) || isScenePreviewOrReflectionCamera);
            cameraData.renderScale = disableRenderScale ? 1.0f : settings.renderScale;

            // Convert the upscaling filter selection from the pipeline asset into an image upscaling filter
            cameraData.upscalingFilter = ResolveUpscalingFilterSelection(new Vector2(cameraData.pixelWidth, cameraData.pixelHeight), cameraData.renderScale, settings.upscalingFilter);

            if (cameraData.renderScale > 1.0f)
            {
                cameraData.imageScalingMode = ImageScalingMode.Downscaling;
            }
            else if ((cameraData.renderScale < 1.0f) || (!isScenePreviewOrReflectionCamera && (cameraData.upscalingFilter == ImageUpscalingFilter.FSR)))
            {
                // When FSR is enabled, we still consider 100% render scale an upscaling operation. (This behavior is only intended for game view cameras)
                // This allows us to run the FSR shader passes all the time since they improve visual quality even at 100% scale.

                cameraData.imageScalingMode = ImageScalingMode.Upscaling;
            }
            else
            {
                cameraData.imageScalingMode = ImageScalingMode.None;
            }

            cameraData.fsrOverrideSharpness = settings.fsrOverrideSharpness;
            cameraData.fsrSharpness = settings.fsrSharpness;
```

**渲染缩放相关：**
- `renderScale`: 渲染缩放比例（< 1.0 为超采样，> 1.0 为降采样）
- `imageScalingMode`: 图像缩放模式（None、Upscaling、Downscaling）
- `upscalingFilter`: 上采样滤镜（用于 FSR、DLSS 等）
- `fsrOverrideSharpness`: FSR 是否覆盖锐度设置
- `fsrSharpness`: FSR 锐度值

#### 10.2.5 排序和捕获设置

```1212:1219:Assets/com.unity.render-pipelines.universal@14.0.11/Runtime/UniversalRenderPipeline.cs
            var commonOpaqueFlags = SortingCriteria.CommonOpaque;
            var noFrontToBackOpaqueFlags = SortingCriteria.SortingLayer | SortingCriteria.RenderQueue | SortingCriteria.OptimizeStateChanges | SortingCriteria.CanvasOrder;
            bool hasHSRGPU = SystemInfo.hasHiddenSurfaceRemovalOnGPU;
            bool canSkipFrontToBackSorting = (baseCamera.opaqueSortMode == OpaqueSortMode.Default && hasHSRGPU) || baseCamera.opaqueSortMode == OpaqueSortMode.NoDistanceSort;

            cameraData.defaultOpaqueSortFlags = canSkipFrontToBackSorting ? noFrontToBackOpaqueFlags : commonOpaqueFlags;
            cameraData.captureActions = CameraCaptureBridge.GetCaptureActions(baseCamera);
```

**排序相关：**
- `defaultOpaqueSortFlags`: 默认不透明物体排序标志（根据 GPU HSR 支持情况优化）

**捕获相关：**
- `captureActions`: 相机捕获动作（用于相机捕获功能）

### 10.3 InitializeCameraData 自身初始化的数据

#### 10.3.1 MSAA 设置

```1092:1102:Assets/com.unity.render-pipelines.universal@14.0.11/Runtime/UniversalRenderPipeline.cs
            var renderer = additionalCameraData?.scriptableRenderer;
            bool rendererSupportsMSAA = renderer != null && renderer.supportedRenderingFeatures.msaa;

            int msaaSamples = 1;
            if (camera.allowMSAA && asset.msaaSampleCount > 1 && rendererSupportsMSAA)
                msaaSamples = (camera.targetTexture != null) ? camera.targetTexture.antiAliasing : asset.msaaSampleCount;

            // Use XR's MSAA if camera is XR camera. XR MSAA needs special handle here because it is not per Camera.
            // Multiple cameras could render into the same XR display and they should share the same MSAA level.
            if (cameraData.xrRendering && rendererSupportsMSAA && camera.targetTexture == null)
                msaaSamples = (int)XRSystem.GetDisplayMSAASamples();
```

**MSAA 相关：**
- `msaaSamples`: 多采样抗锯齿采样数（根据相机设置、渲染器支持、XR 设置确定）

#### 10.3.2 HDR 和渲染目标描述符

```1104:1108:Assets/com.unity.render-pipelines.universal@14.0.11/Runtime/UniversalRenderPipeline.cs
            bool needsAlphaChannel = Graphics.preserveFramebufferAlpha;

            cameraData.hdrColorBufferPrecision = asset ? asset.hdrColorBufferPrecision : HDRColorBufferPrecision._32Bits;
            cameraData.cameraTargetDescriptor = CreateRenderTextureDescriptor(camera, ref cameraData,
                cameraData.isHdrEnabled, cameraData.hdrColorBufferPrecision, msaaSamples, needsAlphaChannel, cameraData.requiresOpaqueTexture);
```

**HDR 和描述符相关：**
- `hdrColorBufferPrecision`: HDR 颜色缓冲区精度（16 位或 32 位）
- `cameraTargetDescriptor`: 相机渲染目标描述符（包含格式、大小、MSAA 等所有渲染纹理参数）

### 10.4 InitializeAdditionalCameraData 初始化的数据

该方法在 `InitializeCameraData` 之后调用，初始化相机特定的设置：

```1228:1334:Assets/com.unity.render-pipelines.universal@14.0.11/Runtime/UniversalRenderPipeline.cs
        static void InitializeAdditionalCameraData(Camera camera, UniversalAdditionalCameraData additionalCameraData, bool resolveFinalTarget, ref CameraData cameraData)
        {
            using var profScope = new ProfilingScope(null, Profiling.Pipeline.initializeAdditionalCameraData);

            var settings = asset;

            bool anyShadowsEnabled = settings.supportsMainLightShadows || settings.supportsAdditionalLightShadows;
            cameraData.maxShadowDistance = Mathf.Min(settings.shadowDistance, camera.farClipPlane);
            cameraData.maxShadowDistance = (anyShadowsEnabled && cameraData.maxShadowDistance >= camera.nearClipPlane) ? cameraData.maxShadowDistance : 0.0f;

            bool isSceneViewCamera = cameraData.isSceneViewCamera;
            if (isSceneViewCamera)
            {
                cameraData.renderType = CameraRenderType.Base;
                cameraData.clearDepth = true;
                cameraData.postProcessEnabled = CoreUtils.ArePostProcessesEnabled(camera);
                cameraData.requiresDepthTexture = settings.supportsCameraDepthTexture;
                cameraData.requiresOpaqueTexture = settings.supportsCameraOpaqueTexture;
                cameraData.renderer = asset.scriptableRenderer;
                cameraData.useScreenCoordOverride = false;
                cameraData.screenSizeOverride = cameraData.pixelRect.size;
                cameraData.screenCoordScaleBias = Vector2.one;
            }
            else if (additionalCameraData != null)
            {
                cameraData.renderType = additionalCameraData.renderType;
                cameraData.clearDepth = (additionalCameraData.renderType != CameraRenderType.Base) ? additionalCameraData.clearDepth : true;
                cameraData.postProcessEnabled = additionalCameraData.renderPostProcessing;
                cameraData.maxShadowDistance = (additionalCameraData.renderShadows) ? cameraData.maxShadowDistance : 0.0f;
                cameraData.requiresDepthTexture = additionalCameraData.requiresDepthTexture;
                cameraData.requiresOpaqueTexture = additionalCameraData.requiresColorTexture;
                cameraData.renderer = additionalCameraData.scriptableRenderer;
                cameraData.useScreenCoordOverride = additionalCameraData.useScreenCoordOverride;
                cameraData.screenSizeOverride = additionalCameraData.screenSizeOverride;
                cameraData.screenCoordScaleBias = additionalCameraData.screenCoordScaleBias;
            }
            else
            {
                cameraData.renderType = CameraRenderType.Base;
                cameraData.clearDepth = true;
                cameraData.postProcessEnabled = false;
                cameraData.requiresDepthTexture = settings.supportsCameraDepthTexture;
                cameraData.requiresOpaqueTexture = settings.supportsCameraOpaqueTexture;
                cameraData.renderer = asset.scriptableRenderer;
                cameraData.useScreenCoordOverride = false;
                cameraData.screenSizeOverride = cameraData.pixelRect.size;
                cameraData.screenCoordScaleBias = Vector2.one;
            }

            // Disables post if GLes2
            cameraData.postProcessEnabled &= SystemInfo.graphicsDeviceType != GraphicsDeviceType.OpenGLES2;

            cameraData.requiresDepthTexture |= isSceneViewCamera;
            cameraData.postProcessingRequiresDepthTexture = CheckPostProcessForDepth(ref cameraData);
            cameraData.resolveFinalTarget = resolveFinalTarget;

            // Disable depth and color copy. We should add it in the renderer instead to avoid performance pitfalls
            // of camera stacking breaking render pass execution implicitly.
            bool isOverlayCamera = (cameraData.renderType == CameraRenderType.Overlay);
            if (isOverlayCamera)
            {
                cameraData.requiresOpaqueTexture = false;
            }

            // NOTE: TAA depends on XR modifications of cameraTargetDescriptor.
            if (additionalCameraData != null)
                UpdateTemporalAAData(ref cameraData, additionalCameraData);

            Matrix4x4 projectionMatrix = camera.projectionMatrix;

            // Overlay cameras inherit viewport from base.
            // If the viewport is different between them we might need to patch the projection to adjust aspect ratio
            // matrix to prevent squishing when rendering objects in overlay cameras.
            if (isOverlayCamera && !camera.orthographic && cameraData.pixelRect != camera.pixelRect)
            {
                // m00 = (cotangent / aspect), therefore m00 * aspect gives us cotangent.
                float cotangent = camera.projectionMatrix.m00 * camera.aspect;

                // Get new m00 by dividing by base camera aspectRatio.
                float newCotangent = cotangent / cameraData.aspectRatio;
                projectionMatrix.m00 = newCotangent;
            }

            // TAA debug settings
            // Affects the jitter set just below. Do not move.
            ApplyTaaRenderingDebugOverrides(ref cameraData.taaSettings);

            // Depends on the cameraTargetDesc, size and MSAA also XR modifications of those.
            Matrix4x4 jitterMat = TemporalAA.CalculateJitterMatrix(ref cameraData);
            cameraData.SetViewProjectionAndJitterMatrix(camera.worldToCameraMatrix, projectionMatrix, jitterMat);

            cameraData.worldSpaceCameraPos = camera.transform.position;

            var backgroundColorSRGB = camera.backgroundColor;
            // Get the background color from preferences if preview camera
#if UNITY_EDITOR
            if (camera.cameraType == CameraType.Preview && camera.clearFlags != CameraClearFlags.SolidColor)
            {
                backgroundColorSRGB = CoreRenderPipelinePreferences.previewBackgroundColor;
            }
#endif

            cameraData.backgroundColor = CoreUtils.ConvertSRGBToActiveColorSpace(backgroundColorSRGB);

            cameraData.stackAnyPostProcessingEnabled = cameraData.postProcessEnabled;
            cameraData.stackLastCameraOutputToHDR = cameraData.isHDROutputActive;
        }
```

**相机特定设置：**
- `renderType`: 相机渲染类型（Base 或 Overlay）
- `clearDepth`: 是否清除深度缓冲区
- `postProcessEnabled`: 是否启用后处理
- `maxShadowDistance`: 最大阴影距离
- `requiresDepthTexture`: 是否需要深度纹理
- `requiresOpaqueTexture`: 是否需要不透明纹理
- `renderer`: 使用的渲染器
- `useScreenCoordOverride`: 是否使用屏幕坐标覆盖
- `screenSizeOverride`: 屏幕大小覆盖
- `screenCoordScaleBias`: 屏幕坐标缩放和偏移

**矩阵和位置：**
- `SetViewProjectionAndJitterMatrix`: 设置视图矩阵、投影矩阵和抖动矩阵（用于 TAA）
- `worldSpaceCameraPos`: 世界空间相机位置

**其他：**
- `backgroundColor`: 背景颜色（转换为正确的颜色空间）
- `resolveFinalTarget`: 是否解析到最终目标

### 10.5 初始化数据总结表

| 类别 | 数据项 | 说明 |
|------|--------|------|
| **基础属性** | `camera` | 相机组件引用 |
| | `targetTexture` | 渲染目标纹理 |
| | `cameraType` | 相机类型 |
| **像素信息** | `pixelRect` | 像素矩形 |
| | `pixelWidth` | 像素宽度 |
| | `pixelHeight` | 像素高度 |
| | `aspectRatio` | 宽高比 |
| | `isDefaultViewport` | 是否为默认视口 |
| **Volume 系统** | `volumeLayerMask` | Volume 层级遮罩 |
| | `volumeTrigger` | Volume 触发器 |
| **后处理** | `isStopNaNEnabled` | NaN 停止 |
| | `isDitheringEnabled` | 抖动 |
| | `antialiasing` | 抗锯齿模式 |
| | `antialiasingQuality` | 抗锯齿质量 |
| | `postProcessEnabled` | 后处理启用 |
| **HDR** | `isHdrEnabled` | HDR 启用 |
| | `allowHDROutput` | HDR 输出允许 |
| | `hdrColorBufferPrecision` | HDR 颜色缓冲区精度 |
| **XR** | `xrRendering` | XR 渲染 |
| | `xr` | XR 通道数据 |
| **渲染缩放** | `renderScale` | 渲染缩放 |
| | `imageScalingMode` | 图像缩放模式 |
| | `upscalingFilter` | 上采样滤镜 |
| | `fsrSharpness` | FSR 锐度 |
| **MSAA** | `msaaSamples` | MSAA 采样数 |
| **渲染目标** | `cameraTargetDescriptor` | 渲染目标描述符 |
| **渲染类型** | `renderType` | 相机渲染类型 |
| | `clearDepth` | 清除深度 |
| **纹理需求** | `requiresDepthTexture` | 需要深度纹理 |
| | `requiresOpaqueTexture` | 需要不透明纹理 |
| **矩阵** | 视图矩阵 | 相机视图矩阵 |
| | 投影矩阵 | 相机投影矩阵 |
| | 抖动矩阵 | TAA 抖动矩阵 |
| **位置** | `worldSpaceCameraPos` | 世界空间相机位置 |
| **颜色** | `backgroundColor` | 背景颜色 |
| **排序** | `defaultOpaqueSortFlags` | 不透明物体排序标志 |
| **捕获** | `captureActions` | 相机捕获动作 |

## 十一、ProfilingScope 性能分析工具

### 11.1 ProfilingScope 的作用

`ProfilingScope` 是 Unity 提供的**性能分析工具**，用于在 Unity Profiler 中标记代码段的执行时间，帮助开发者识别性能瓶颈。

```1230:1230:Assets/com.unity.render-pipelines.universal@14.0.11/Runtime/UniversalRenderPipeline.cs
            using var profScope = new ProfilingScope(null, Profiling.Pipeline.initializeAdditionalCameraData);
```

### 11.2 工作原理

`ProfilingScope` 是一个实现了 `IDisposable` 接口的结构体，使用 `using` 语句自动管理生命周期：

```227:277:Library/PackageCache/com.unity.render-pipelines.core@14.0.11/Runtime/Debugging/ProfilingScope.cs
    public struct ProfilingScope : IDisposable
    {
        CommandBuffer       m_Cmd;
        bool                m_Disposed;
        ProfilingSampler    m_Sampler;

        /// <summary>
        /// Profiling Scope constructor
        /// </summary>
        /// <param name="cmd">Command buffer used to add markers and compute execution timings.</param>
        /// <param name="sampler">Profiling Sampler to be used for this scope.</param>
        public ProfilingScope(CommandBuffer cmd, ProfilingSampler sampler)
        {
            // NOTE: Do not mix with named CommandBuffers.
            // Currently there's an issue which results in mismatched markers.
            // The named CommandBuffer will close its "profiling scope" on execution.
            // That will orphan ProfilingScope markers as the named CommandBuffer marker
            // is their "parent".
            // Resulting in following pattern:
            // exec(cmd.start, scope.start, cmd.end) and exec(cmd.start, scope.end, cmd.end)
            m_Cmd = cmd;
            m_Disposed = false;
            m_Sampler = sampler;
            m_Sampler?.Begin(m_Cmd);
        }

        /// <summary>
        ///  Dispose pattern implementation
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        // Protected implementation of Dispose pattern.
        void Dispose(bool disposing)
        {
            if (m_Disposed)
                return;

            // As this is a struct, it could have been initialized using an empty constructor so we
            // need to make sure `cmd` isn't null to avoid a crash. Switching to a class would fix
            // this but will generate garbage on every frame (and this struct is used quite a lot).
            if (disposing)
            {
                m_Sampler?.End(m_Cmd);
            }

            m_Disposed = true;
        }
    }
```

**工作流程：**

1. **构造时**：调用 `m_Sampler.Begin(m_Cmd)` 开始性能采样
2. **作用域结束时**：由于 `using` 语句，自动调用 `Dispose()`，执行 `m_Sampler.End(m_Cmd)` 结束采样

### 11.3 参数说明

```csharp
new ProfilingScope(null, Profiling.Pipeline.initializeAdditionalCameraData)
```

**第一个参数 `cmd` (CommandBuffer)**：
- **作用**：用于记录 GPU 性能标记
- **为 `null` 时**：只记录 CPU 性能，不记录 GPU 性能
- **不为 `null` 时**：同时记录 CPU 和 GPU 性能（在 CommandBuffer 中插入标记）

**第二个参数 `sampler` (ProfilingSampler)**：
- **作用**：定义性能采样的名称和标识符
- **示例**：`Profiling.Pipeline.initializeAdditionalCameraData` 表示 "初始化附加相机数据" 这个采样点

### 11.4 在 Unity Profiler 中的显示

使用 `ProfilingScope` 后，在 Unity Profiler 的 **Hierarchy** 视图中会显示：

```
UniversalRenderPipeline.Render
  └─ UniversalRenderPipeline.RenderSingleCameraInternal
       └─ InitializeCameraData
            └─ InitializeAdditionalCameraData  ← 这里会显示执行时间
```

### 11.5 使用场景和优势

**主要用途：**

1. **性能分析**：识别代码执行时间，找出性能瓶颈
2. **调试**：追踪渲染流程中各个步骤的执行情况
3. **优化**：对比优化前后的性能差异

**优势：**

1. **自动管理**：使用 `using` 语句自动开始和结束采样
2. **零开销（Release 模式）**：在非开发模式下，可能被编译为空操作
3. **层次化**：支持嵌套的采样点，形成层次结构
4. **CPU/GPU 分离**：可以分别记录 CPU 和 GPU 性能

### 11.6 使用注意事项

**重要提示（来自源码注释）：**

```618:623:Assets/com.unity.render-pipelines.universal@14.0.11/Runtime/UniversalRenderPipeline.cs
            // NOTE: Do NOT mix ProfilingScope with named CommandBuffers i.e. CommandBufferPool.Get("name").
            // Currently there's an issue which results in mismatched markers.
            // The named CommandBuffer will close its "profiling scope" on execution.
            // That will orphan ProfilingScope markers as the named CommandBuffer markers are their parents.
            // Resulting in following pattern:
            // exec(cmd.start, scope.start, cmd.end) and exec(cmd.start, scope.end, cmd.end)
```

**注意事项：**
- ❌ **不要**将 `ProfilingScope` 与命名的 `CommandBuffer` 混合使用
- ✅ 在方法开始时创建 `ProfilingScope`，结束时自动销毁
- ✅ 使用 `null` 作为 `cmd` 参数时只记录 CPU 性能

### 11.7 实际示例

在 URP 源码中，`ProfilingScope` 被广泛使用来标记各个渲染步骤：

```319:319:Assets/com.unity.render-pipelines.universal@14.0.11/Runtime/UniversalRenderPipeline.cs
            using var profScope = new ProfilingScope(null, ProfilingSampler.Get(URPProfileId.UniversalRenderTotal));
```

```375:389:Assets/com.unity.render-pipelines.universal@14.0.11/Runtime/UniversalRenderPipeline.cs
                    using (new ProfilingScope(null, Profiling.Pipeline.beginCameraRendering))
                    {
                        BeginCameraRendering(renderContext, camera);
                    }
#if VISUAL_EFFECT_GRAPH_0_0_1_OR_NEWER
                    //It should be called before culling to prepare material. When there isn't any VisualEffect component, this method has no effect.
                    VFX.VFXManager.PrepareCamera(camera);
#endif
                    UpdateVolumeFramework(camera, null);

                    RenderSingleCameraInternal(renderContext, camera);

                    using (new ProfilingScope(null, Profiling.Pipeline.endCameraRendering))
                    {
                        EndCameraRendering(renderContext, camera);
                    }
```

### 11.8 总结

`ProfilingScope` 是 Unity 渲染管线中用于性能分析的关键工具：

- ✅ **自动记录**代码执行时间
- ✅ **零开销**在发布版本中
- ✅ **层次化**显示性能数据
- ✅ **简化**性能分析工作流

通过使用 `ProfilingScope`，开发者可以轻松识别渲染管线中各个步骤的性能开销，为优化提供数据支持。

## 十二、总结

URP 是一个高度模块化和可扩展的渲染管线，核心特点：

1. **可编程性**: 通过 ScriptableRenderer 和 ScriptableRenderPass 实现完全可编程
2. **模块化**: 渲染通道系统允许灵活组合
3. **性能优化**: 多种优化技术确保高性能
4. **跨平台**: 支持多种渲染模式和硬件平台
5. **扩展性**: 支持自定义渲染通道和特性
6. **相机堆叠**: 通过 Base 和 Overlay 相机实现多层次渲染合成

通过深入理解 URP 的源码，可以更好地：
- 优化渲染性能
- 创建自定义渲染效果
- 调试渲染问题
- 扩展渲染管线功能
- 合理使用相机堆叠实现复杂渲染需求

