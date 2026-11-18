# 游戏与关卡结构

## 当前结构（2026-11-19）

- `Assets/MineSweeper/Scripts/Desktop`：桌面、窗口、快捷方式、拖动、时钟和笑脸桌宠。目前位于扫雷目录，但职责属于整个合集。
- `Assets/Collection/Scripts`：弹球接入桌面的窗口、入口和渲染适配。弹球使用独立物理场景，把画面显示到桌面窗口内。
- `Assets/MineSweeper/Scripts/Minesweeper/Core`：基础扫雷棋盘、格子、游戏状态、揭开/插旗/连开规则，以及棋盘配置。基础规则不依赖 Boss 或平台跳跃。
- `Assets/MineSweeper/Scripts/Minesweeper/Campaign`：扫雷战役配置，引用棋盘与关卡参数。
- `Assets/MineSweeper/Scripts/Minesweeper/Levels/Level01Sweep`：第一关的初始化和更新。
- `Assets/MineSweeper/Scripts/Minesweeper/Levels/Level02HiddenBoard`：第二关的隐藏棋盘、窗口观察、字符与 emoji 显示，以及右下角悖论的编辑修复。
- `Assets/MineSweeper/Scripts/Minesweeper/Levels/FinalLevel`：最终 Boss 的回合、血量、倒计时、攻击及特殊扫雷规则。
- `Assets/MineSweeper/Scripts/Minesweeper/Levels/ArchivedPlatform`：保留的跳跃模拟和关卡适配代码。当前流程没有进入它的调用。
- `Assets/MineSweeper/Scripts/Minesweeper/UI`：棋盘与格子显示、特效、关卡 HUD，以及仍然共享的控制器和流程分发。
- `Assets/SpaceCadet`：弹球本体，包括物理、计分、目标、挡板和镜头。

现有脚本都属于默认的 Assembly-CSharp 程序集，没有按模块拆分程序集。

## 当前关卡流程

第一关普通扫雷 → 第二关隐藏棋盘与悖论 → Final Level（Boss，始终排在最后）→ 完成后可重新开始。

第一关使用固定棋盘和标准扫雷规则，保留揭格、插旗、连开与普通胜负判定。Boss 关标题显示为 Minesweeper [Final Level]。后续增加中间关卡时，Boss 仍放在战役末尾。

沿用原有第一关的推进条件：固定棋盘处于 Playing 或 Won 时，可以通过笑脸或 Next level 按钮进入第二关。

第二关为 30×16、20 颗雷；左上起始观察区域不布雷。右下角保留固定的局部谜题，两个已揭开的线索共享同一组未揭开邻格：一颗已插旗的雷与一格安全格。正确线索为 1，其中另一条被故意写成 2。其他数字保持与真实布雷一致。

记事本里的观察棋盘使用字符与 emoji，每个字符对齐真实格子的桌面位置。特殊 2 完整进入记事本的文本区域且没有被上层窗口遮住时，创建在该格位置的临时输入框自动获得焦点，光标定位到 2 后面。删除 2，再输入 1 后同步修复真实棋盘并直接完成第二关，无须扫完其余安全格。错误输入、只删除、普通扫雷均不会完成本关。完成后可点击 Final Level 进入 Boss。

字符观察、emoji 资源、特殊输入框、临时焦点与规则全部由第二关持有；退出或重开时移除并恢复记事本原有输入状态与正文。通用记事本本身不包含扫雷机制。

跳跃的移动、碰撞、重试、目标检测、显示辅助代码和起终点配置均保留。相关代码仍参与编译，但不会在当前战役中启动。

## 笑脸桌宠

扫雷窗口「New Game」方块中的笑脸由 `DesktopPet` 在运行时移到桌面根节点，作为常驻桌宠（层级最高，不参与图标拖动）。窗口按钮保留灰底，仍可点击。两者共用同一张 Image，因此 ready/won/lost、Boss 受击闪光等状态自动同步；Boss 的飞雷也以桌宠为目标。

进入 Boss 时输入暂停，桌宠飞回窗口按钮后 Boss 才开始；Boss 结束回到第一关时桌宠飞回桌面。

后续剧情/对话接入点：`DesktopPet.Clicked` 事件（每次点击先触发）、`AllowDefaultClick`（返回 false 可拦截默认的关卡操作）、`Interactable`（暂停交互）；`Face`、`Rect`、`OnDesktop`、`ShowOnDesktop`、`AttachToButton`、`RevealOnButton` 可用于演出控制。

## 对话与开局演出

`DialogueDirector` 持有桌宠的对话气泡：`Say(id, lines)` 按顺序播放，同 id 请求在播放或排队期间不会重复入队，前一段结束后自动接下一段；`Interrupt(id, lines)` 用于需要立刻回应的反应。播放时（以及开局出场期间）会在桌面根节点下启用一层全屏透明射线阻挡层，层级在窗口、任务栏之上、桌宠之下，因此除点击桌宠或气泡推进对话外，其他交互都会被挡住。`Finished` 事件留给后续关卡流程使用（例如播完对话再进入下一关）。目前没有任何由剧情驱动的关卡流程，关卡仍由 HUD 按钮切换。

开局时 `MinesweeperController.Start` 在 `NewGame()` 之后调用 `CloseWindow()`，所以进入游戏时只有桌面。`Minesweeper Shortcut` 图标会周期性晃动，提示玩家双击打开扫雷。第一次打开窗口时 `DesktopWindow.Shown` 触发 `GameIntro`：桌宠先出现在 New Game 方块中，停留片刻后飞到右下角桌面位置，落地后播放三句开场白。图标晃动和出场演出只触发一次。

`GameIntro` 同时监听 `DesktopIconDrag.Dropped`：把 Minesweeper 快捷方式拖到回收站区域内时，桌宠会说 “No, no, no! You can't do that.” 与 “That's my favorite game!”，图标本身仍会吸附回网格，不会被删除。

第二关通过 `MinesweeperController.HiddenBoardStarted` / `HiddenBoardEnded` / `InHiddenBoard` 暴露进入与退出时机。`GameIntro` 在每次进入（含重开）第二关后等待 10 秒，若仍在该关则提示 “Open Notepad.” 与 “You might find something unexpected.”；重开或离开第二关会取消上一轮计时。

第二关的大棋盘在右下角为桌宠预留了 140 px 的右侧净空，悖论格与底部谜题区不会压在桌宠下面。

## 本次隔离的程度

基础扫雷算法与特殊关卡机制已有单独的类。此次把 Boss、跳跃的状态和执行方法移入对应关卡目录，并保留移动脚本的 .meta GUID，避免破坏资源引用。

MinesweeperController 使用 partial 分文件，仍是同一个运行时对象。这是关卡代码的组织边界，还不是运行时隔离。关卡切换、HUD 和输入分发仍存在关卡判断；BoardEffects 和 CellView 也保留部分特殊机制显示。

## 后续扩展应遵守的边界

建议按以下四层继续开发：

1. 合集外壳：只管理桌面、窗口、应用打开关闭与焦点，不直接实现某个游戏的规则。
2. 游戏本体：每个游戏拥有自己的核心模型、规则和显示接口；核心规则不引用具体关卡，也不引用桌面应用。
3. 关卡模块：每关有自己的配置、状态、输入处理、特殊规则和完成条件，通过游戏提供的接口组合或覆盖规则。
4. 战役流程：只决定关卡顺序、启动与结束，不承载 Boss 攻击或跳跃碰撞算法。

目标目录为 Shared/Desktop、Games/<游戏>/Core、Games/<游戏>/Presentation、Games/<游戏>/Levels/<关卡> 和 Games/<游戏>/Campaign。每个关卡目录再按实际需要放 Config、Runtime、Presentation 与资源。可复用的解构机制成熟后再提取到 Mechanics，关卡保存自己的参数与组合方式。

下一步应把 partial 中的关卡逻辑提取为独立关卡对象，统一提供 Enter、Tick、Exit 与完成通知，由战役控制器调用；随后拆分每关配置和显示特效，再迁移共享桌面目录。这样新增游戏或关卡时，不需要持续向通用控制器添加 Stage 分支。此次尚未实现这些接口，也没有调整弹球的模块结构。

## 验证

目前只保留一个场景 `Assets/Scenes/SampleScene.unity`。关卡和弹球需要在编辑器中手动游玩验证；此前的独立回归工程与辅助脚本已删除。
