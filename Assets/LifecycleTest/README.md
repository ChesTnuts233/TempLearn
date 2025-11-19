# Unity 生命周期测试系统

这是一个完整的 Unity 生命周期测试系统，用于研究和学习 Unity 中所有生命周期的实际执行顺序。

## 功能特性

- ✅ 覆盖所有主要生命周期方法
- ✅ 详细的注释说明每个方法的作用
- ✅ 精确的时间戳和帧数记录
- ✅ 支持与 Animator 系统集成测试
- ✅ 包含物理系统生命周期测试
- ✅ 协程执行顺序测试

## 快速开始

### 方法1：创建基础测试单元

1. 在 Unity 编辑器中，点击菜单：`Tools > LifecycleTest > 创建生命周期测试单元`
2. 这会创建：
   - `LifecycleTestObject` - 主测试对象
   - `LifecycleTestCamera` - 带相机的测试对象（用于测试渲染相关生命周期）

### 方法2：创建完整测试场景（包含 Animator）

1. 首先运行：`Tools > AnimatorTest > 创建测试单元`（如果还没有）
2. 然后运行：`Tools > LifecycleTest > 创建完整测试场景（包含 Animator）`
3. 这会同时测试生命周期和 Animator 事件的执行顺序

## 测试脚本说明

### UnityLifecycleTester.cs

主要的生命周期测试脚本，包含以下生命周期方法：

#### 初始化阶段
- `Awake()` - 对象创建时立即调用
- `OnEnable()` - 对象激活时调用
- `Start()` - 首次激活后第一帧调用

#### 更新阶段
- `FixedUpdate()` - 固定物理更新
- `Update()` - 每帧更新
- `LateUpdate()` - 延迟更新

#### 动画系统
- `OnAnimatorMove()` - 动画根运动处理
- `OnAnimatorIK()` - 动画 IK 处理

#### 渲染阶段
- `OnGUI()` - GUI 渲染
- `OnPreRender()` - 相机预渲染
- `OnRenderObject()` - 对象渲染
- `OnPostRender()` - 相机后渲染

#### 协程
- `TestCoroutine()` - 测试协程的执行顺序

#### 清理阶段
- `OnDisable()` - 对象禁用时调用
- `OnDestroy()` - 对象销毁时调用
- `OnApplicationPause()` - 应用暂停
- `OnApplicationFocus()` - 应用焦点变化
- `OnApplicationQuit()` - 应用退出

### PhysicsLifecycleTester.cs

物理系统生命周期测试脚本，包含：
- `OnTriggerEnter/Stay/Exit()` - 触发器事件
- `OnCollisionEnter/Stay/Exit()` - 碰撞事件
- `OnControllerColliderHit()` - CharacterController 碰撞

## 查看测试结果

1. 运行场景（点击 Play 按钮）
2. 打开 Console 窗口（`Window > General > Console`）
3. 观察日志输出，每个生命周期方法都会打印：
   - 实例 ID（区分多个对象）
   - 方法名称
   - 精确时间戳（6位小数）
   - 帧数
   - 时间增量

## 日志格式

```
[1] [Awake] 对象创建时立即调用，时间: 0.000000, 帧: 0, deltaTime: 0.000000
[1] [OnEnable] 对象激活时调用，时间: 0.000000, 帧: 0, deltaTime: 0.000000
[1] [Start] 首次激活后第一帧调用，时间: 0.000000, 帧: 1, deltaTime: 0.016667
```

## 执行顺序总结

### 单帧完整执行顺序

```
Awake
  ↓
OnEnable (首次激活)
  ↓
Start
  ↓
FixedUpdate (如果该帧需要)
  ↓
Update
  ↓
LateUpdate
  ↓
OnAnimatorMove (如果有 Animator)
  ↓
OnAnimatorIK (如果有 Animator 且启用 IK)
  ↓
OnGUI (渲染阶段，可能多次)
  ↓
OnPreRender (如果有 Camera)
  ↓
OnRenderObject
  ↓
OnPostRender (如果有 Camera)
  ↓
Coroutine (yield return new WaitForEndOfFrame)
```

### 与 Animator 集成时的执行顺序

当同时使用生命周期测试和 Animator 测试时，可以观察到：

```
MonoBehaviour.Update
  ↓
AnimationEvent (第一帧事件) - 仅默认状态
  ↓
MonoBehaviour.LateUpdate
  ↓
OnStateEnter - 状态机回调
  ↓
OnStateUpdate - 状态机回调
  ↓
OnStateMove - 状态机回调
  ↓
OnAnimatorMove - MonoBehaviour 回调
```

## 注意事项

1. **默认状态的特殊性**：
   - 默认状态的 AnimationEvent 可能在 OnStateEnter 之前触发
   - 这是因为默认状态在场景启动时立即开始播放动画

2. **FixedUpdate 与 Update**：
   - FixedUpdate 的时间步长固定（默认 0.02 秒）
   - 一帧内可能调用多次 FixedUpdate，也可能不调用
   - 取决于物理时间步长和帧率

3. **协程执行时机**：
   - `yield return null`: 在 Update 之后
   - `yield return new WaitForFixedUpdate()`: 在 FixedUpdate 之后
   - `yield return new WaitForEndOfFrame()`: 在帧结束后

4. **渲染相关方法**：
   - `OnPreRender` 和 `OnPostRender` 需要挂载到有 Camera 组件的对象上
   - `OnGUI` 可能在同一帧内调用多次

## 文件结构

```
Assets/LifecycleTest/
├── UnityLifecycleTester.cs          # 主生命周期测试脚本
├── PhysicsLifecycleTester.cs        # 物理系统生命周期测试脚本
├── Editor/
│   └── LifecycleTestSetup.cs        # 编辑器工具脚本
├── 生命周期执行顺序说明.md          # 详细说明文档
└── README.md                         # 本文件
```

## 扩展使用

### 添加自定义生命周期测试

可以在 `UnityLifecycleTester.cs` 中添加更多生命周期方法，例如：
- `OnBecameVisible()` / `OnBecameInvisible()` - 可见性变化
- `OnMouseEnter()` / `OnMouseExit()` - 鼠标事件
- `OnParticleCollision()` - 粒子碰撞

### 与其他系统集成

可以结合以下系统进行测试：
- Animator 系统（已支持）
- 物理系统（PhysicsLifecycleTester）
- UI 系统
- 网络系统

## 参考文档

- [Unity 官方文档 - MonoBehaviour 生命周期](https://docs.unity3d.com/Manual/ExecutionOrder.html)
- [Unity 官方文档 - 执行顺序](https://docs.unity3d.com/Manual/ExecutionOrder.html)

## 版本历史

- v1.0 - 初始版本，包含所有主要生命周期方法
- 支持与 Animator 系统集成测试


