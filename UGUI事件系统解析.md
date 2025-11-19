# UGUI 事件系统解析

## 一、pointerPress 的作用

### 1.1 定义和用途

```csharp
/// <summary>
/// The GameObject that received the OnPointerDown.
/// </summary>
public GameObject pointerPress
{
    get { return m_PointerPress; }
    set
    {
        if (m_PointerPress == value)
            return;

        lastPress = m_PointerPress;
        m_PointerPress = value;
    }
}
```

**作用**：
- **记录按下时接收事件的 GameObject**：存储了在 `OnPointerDown` 时接收事件的 GameObject
- **保存上一次的按下对象**：通过 `lastPress` 保存上一次按下的对象，用于双击检测
- **事件链的起点**：作为后续事件（OnPointerUp、OnPointerClick）的参考点

### 1.2 设置时机

在按下时（PointerDown）设置：

```csharp
// 在 TouchInputModule 或 StandaloneInputModule 中
var newPressed = ExecuteEvents.ExecuteHierarchy(currentOverGo, pointerEvent, ExecuteEvents.pointerDownHandler);

// 如果没找到 press handler，使用 click handler
if (newPressed == null)
    newPressed = ExecuteEvents.GetEventHandler<IPointerClickHandler>(currentOverGo);

pointerEvent.pointerPress = newPressed;
pointerEvent.rawPointerPress = currentOverGo; // 原始对象（即使不能处理事件）
```

**关键点**：
- `pointerPress`：实际能处理事件的 GameObject（可能通过冒泡找到）
- `rawPointerPress`：原始被按下的 GameObject（即使不能处理事件）

## 二、ReleaseMouse 方法解析

### 2.1 完整代码流程

```csharp
private void ReleaseMouse(PointerEventData pointerEvent, GameObject currentOverGo)
{
    // 步骤 1：对按下时的对象执行 OnPointerUp
    ExecuteEvents.Execute(pointerEvent.pointerPress, pointerEvent, ExecuteEvents.pointerUpHandler);

    // 步骤 2：从当前悬停的对象向上冒泡查找能处理点击的对象
    var pointerClickHandler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(currentOverGo);

    // 步骤 3：如果按下和释放都在同一个对象上，执行点击事件
    if (pointerEvent.pointerClick == pointerClickHandler && pointerEvent.eligibleForClick)
    {
        ExecuteEvents.Execute(pointerEvent.pointerClick, pointerEvent, ExecuteEvents.pointerClickHandler);
    }
    
    // 步骤 4：处理拖拽结束和拖放
    if (pointerEvent.pointerDrag != null && pointerEvent.dragging)
    {
        ExecuteEvents.ExecuteHierarchy(currentOverGo, pointerEvent, ExecuteEvents.dropHandler);
    }

    // 步骤 5：清理状态
    pointerEvent.eligibleForClick = false;
    pointerEvent.pointerPress = null;
    pointerEvent.rawPointerPress = null;
    pointerEvent.pointerClick = null;

    // 步骤 6：结束拖拽
    if (pointerEvent.pointerDrag != null && pointerEvent.dragging)
        ExecuteEvents.Execute(pointerEvent.pointerDrag, pointerEvent, ExecuteEvents.endDragHandler);

    pointerEvent.dragging = false;
    pointerEvent.pointerDrag = null;

    // 步骤 7：重新处理 Enter/Exit 事件
    if (currentOverGo != pointerEvent.pointerEnter)
    {
        HandlePointerExitAndEnter(pointerEvent, null);
        HandlePointerExitAndEnter(pointerEvent, currentOverGo);
    }

    m_InputPointerEvent = pointerEvent;
}
```

### 2.2 为什么先 Execute 再 GetEventHandler？

#### 问题分析

**为什么先执行 `ExecuteEvents.Execute(pointerEvent.pointerPress, ...)`？**

```csharp
// 第一步：直接在按下时的对象上执行 OnPointerUp
ExecuteEvents.Execute(pointerEvent.pointerPress, pointerEvent, ExecuteEvents.pointerUpHandler);
```

**原因**：
1. **OnPointerUp 应该在按下时的对象上触发**
   - 即使鼠标/手指已经移开，`OnPointerUp` 仍然应该在按下时的对象上触发
   - 这是事件系统的设计：按下和抬起应该配对

2. **确保事件链的完整性**
   - `OnPointerDown` 在对象 A 上触发
   - `OnPointerUp` 也应该在对象 A 上触发（即使当前悬停在对象 B 上）

**为什么后面又用 `GetEventHandler` 进行冒泡？**

```csharp
// 第二步：从当前悬停的对象向上冒泡查找能处理点击的对象
var pointerClickHandler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(currentOverGo);
```

**原因**：
1. **判断点击是否有效**
   - `OnPointerClick` 只有在**按下和释放都在同一个对象上**时才应该触发
   - 需要检查当前悬停的对象是否是按下时的对象

2. **事件冒泡机制**
   - `GetEventHandler` 会从当前对象向上遍历，找到第一个能处理点击的对象
   - 这允许父对象处理子对象的事件（事件冒泡）

3. **验证点击条件**
   ```csharp
   // 只有当按下时的对象和释放时的对象是同一个时，才触发点击
   if (pointerEvent.pointerClick == pointerClickHandler && pointerEvent.eligibleForClick)
   {
       ExecuteEvents.Execute(pointerEvent.pointerClick, pointerEvent, ExecuteEvents.pointerClickHandler);
   }
   ```

### 2.3 GetEventHandler 的工作原理

```356:372:Library/PackageCache/com.unity.ugui@1.0.0/Runtime/EventSystem/ExecuteEvents.cs
        /// <summary>
        /// Bubble the specified event on the game object, figuring out which object will actually receive the event.
        /// </summary>
        public static GameObject GetEventHandler<T>(GameObject root) where T : IEventSystemHandler
        {
            if (root == null)
                return null;

            Transform t = root.transform;
            while (t != null)
            {
                if (CanHandleEvent<T>(t.gameObject))
                    return t.gameObject;
                t = t.parent;
            }
            return null;
        }
```

**工作流程**：
1. 从当前 GameObject 开始
2. 向上遍历父对象链
3. 返回第一个能处理该事件的 GameObject
4. 如果都没找到，返回 null

**示例场景**：
```
Canvas
  └─ Panel (实现 IPointerClickHandler)
      └─ Button (实现 IPointerClickHandler)
          └─ Text
```

如果点击 Text：
- `GetEventHandler<IPointerClickHandler>(Text)` 会返回 Button
- 因为 Button 是第一个能处理点击的对象

### 2.4 完整的事件流程示例

#### 场景：点击一个按钮

**按下时（OnPointerDown）**：
```
1. 鼠标/手指按下 Button
2. ExecuteHierarchy 查找能处理 OnPointerDown 的对象 → Button
3. pointerEvent.pointerPress = Button
4. pointerEvent.rawPointerPress = Button
5. 执行 Button.OnPointerDown()
```

**释放时（OnPointerUp）**：
```
情况 A：在同一个对象上释放
1. ExecuteEvents.Execute(pointerEvent.pointerPress, ...) 
   → 执行 Button.OnPointerUp()
2. GetEventHandler<IPointerClickHandler>(currentOverGo) 
   → 返回 Button
3. pointerEvent.pointerClick == pointerClickHandler → true
4. 执行 Button.OnPointerClick() ✅

情况 B：移开后再释放
1. ExecuteEvents.Execute(pointerEvent.pointerPress, ...) 
   → 执行 Button.OnPointerUp() (仍然在 Button 上触发)
2. GetEventHandler<IPointerClickHandler>(currentOverGo) 
   → 返回 Panel (当前悬停的对象)
3. pointerEvent.pointerClick != pointerClickHandler → false
4. 不执行 OnPointerClick() ❌
```

## 三、关键设计理念

### 3.1 事件配对原则

| 事件 | 触发对象 | 说明 |
|------|---------|------|
| `OnPointerDown` | 按下时的对象 | 通过 ExecuteHierarchy 或 GetEventHandler 查找 |
| `OnPointerUp` | **按下时的对象** | 始终在 `pointerPress` 上触发 |
| `OnPointerClick` | **按下和释放都在的对象** | 需要验证是否是同一个对象 |

### 3.2 Execute vs GetEventHandler

| 方法 | 作用 | 使用场景 |
|------|------|---------|
| `ExecuteEvents.Execute` | **直接执行**事件处理函数 | 在已知对象上触发事件 |
| `ExecuteEvents.GetEventHandler` | **查找**能处理事件的对象 | 需要找到事件接收者（冒泡） |
| `ExecuteEvents.ExecuteHierarchy` | **向上遍历执行** | 在父对象链上查找并执行 |

### 3.3 为什么需要这种设计？

1. **用户体验一致性**
   - 按下按钮后，即使手指移开，按钮仍然应该收到 `OnPointerUp`
   - 避免用户快速点击时丢失事件

2. **事件语义清晰**
   - `OnPointerDown` 和 `OnPointerUp` 是配对的事件
   - `OnPointerClick` 是完整的点击操作（按下+释放）

3. **支持事件冒泡**
   - 父对象可以处理子对象的事件
   - 通过 `GetEventHandler` 实现冒泡查找

## 四、代码执行顺序解析

### 4.1 ReleaseMouse 的完整执行流程

```
ReleaseMouse 开始
    │
    ├─ 1. ExecuteEvents.Execute(pointerEvent.pointerPress, ...)
    │   └─ 执行 OnPointerUp 在按下时的对象上
    │   └─ 目的：确保 Up 事件在 Down 的同一对象上触发
    │
    ├─ 2. ExecuteEvents.GetEventHandler<IPointerClickHandler>(currentOverGo)
    │   └─ 从当前悬停的对象向上冒泡查找
    │   └─ 目的：找到当前能处理点击的对象
    │
    ├─ 3. 判断是否触发 OnPointerClick
    │   ├─ 如果 pointerEvent.pointerClick == pointerClickHandler
    │   │   └─ 说明按下和释放都在同一个对象上
    │   │   └─ 执行 OnPointerClick ✅
    │   └─ 否则不执行 ❌
    │
    ├─ 4. 处理拖放事件
    │   └─ 如果正在拖拽，执行 OnDrop
    │
    ├─ 5. 清理状态
    │   └─ 重置所有事件相关标志
    │
    └─ 6. 重新处理 Enter/Exit
        └─ 确保状态正确
```

### 4.2 设计要点总结

**为什么先用 Execute 再用 GetEventHandler？**

1. **Execute** 用于 `OnPointerUp`：
   - 直接在已知对象（`pointerPress`）上执行
   - 不需要查找，因为已经知道按下时的对象

2. **GetEventHandler** 用于 `OnPointerClick`：
   - 需要查找当前能处理点击的对象
   - 需要验证是否是同一个对象（按下和释放都在）
   - 支持事件冒泡机制

**这种设计的好处**：
- ✅ **事件语义清晰**：Down 和 Up 配对，Click 需要完整操作
- ✅ **用户体验好**：即使移开也能收到 Up 事件
- ✅ **支持冒泡**：父对象可以处理子对象事件
- ✅ **状态正确**：确保事件状态的一致性

## 五、实际应用示例

### 5.1 场景：按钮嵌套

```csharp
// 场景结构
Canvas
  └─ Panel (实现 IPointerClickHandler)
      └─ Button (实现 IPointerClickHandler)
          └─ Text
```

**用户操作**：点击 Text

**按下时**：
- `pointerPress` = Button（通过 GetEventHandler 找到）
- `rawPointerPress` = Text（原始对象）
- 执行 `Button.OnPointerDown()`

**释放时（在 Button 上）**：
- 执行 `Button.OnPointerUp()`（在 pointerPress 上）
- `GetEventHandler<IPointerClickHandler>(Button)` → Button
- `pointerEvent.pointerClick == Button` → true
- 执行 `Button.OnPointerClick()` ✅

**释放时（移开 Panel 上）**：
- 执行 `Button.OnPointerUp()`（仍在 pointerPress 上）
- `GetEventHandler<IPointerClickHandler>(Panel)` → Panel
- `pointerEvent.pointerClick != Panel` → false
- 不执行 OnPointerClick ❌

### 5.2 关键代码位置

**TouchInputModule.cs**（触摸输入）：
```207:217:Library/PackageCache/com.unity.ugui@1.0.0/Runtime/EventSystem/InputModules/TouchInputModule.cs
                ExecuteEvents.Execute(pointerEvent.pointerPress, pointerEvent, ExecuteEvents.pointerUpHandler);

                // see if we mouse up on the same element that we clicked on...
                var pointerUpHandler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(currentOverGo);

                // PointerClick and Drop events
                if (pointerEvent.pointerPress == pointerUpHandler && pointerEvent.eligibleForClick)
                {
                    ExecuteEvents.Execute(pointerEvent.pointerPress, pointerEvent, ExecuteEvents.pointerClickHandler);
                }
```

## 六、总结

### 6.1 pointerPress 的作用

- **记录按下时的对象**：用于后续的 Up 和 Click 事件
- **事件链的起点**：确保事件正确配对
- **支持双击检测**：通过 `lastPress` 保存上一次的对象

### 6.2 ReleaseMouse 的设计

1. **先 Execute**：确保 `OnPointerUp` 在按下时的对象上触发
2. **后 GetEventHandler**：查找当前能处理点击的对象，验证点击是否有效
3. **条件判断**：只有按下和释放都在同一对象上才触发 `OnPointerClick`

### 6.3 设计优势

- ✅ **事件配对正确**：Down 和 Up 在同一对象上
- ✅ **点击验证准确**：只有完整点击才触发 Click
- ✅ **支持事件冒泡**：父对象可以处理子对象事件
- ✅ **用户体验好**：即使移开也能收到正确的事件

这种设计确保了 Unity UI 事件系统的正确性和用户体验的一致性。


