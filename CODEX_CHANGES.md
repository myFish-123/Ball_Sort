## 2026-09-17 水柱抬起与下落改为帧动画

**原因**
水柱转移需要通过 water-up / water-down 呈现，并在落位后从底部展开。

**修改**
- 选中时隐藏 Body/Up，改用 water-up 从水柱底部随位置升起。
- Animator 先完整播放 water-up，再无混合切换到原时间轴 20～30 帧的循环片段。
- 到目标管口后切换 water-down，下落动画速度匹配实际下落时长；落位后隐藏特效，Body 用底部支点展开，Up 保持自身尺寸并跟随。
- WaterTransferVisual 管理视觉切换，特效与普通视觉为同级，颜色及排序沿用水柱。
- 取消选择、溢出返回也使用下落/展开流程；中断、重建关卡和对象销毁时清理对应 Tween。

**主要文件**
- `Assets/Scripts/Game/WaterTransferVisual.cs`
- `Assets/Scripts/Game/BallView.cs`
- `Assets/Scripts/Game/GameController.cs`
- `Assets/prefabs/Water/body.prefab`
- `Assets/Controller/water_00.controller`
- `Assets/Controller/water-up.anim`
- `Assets/Controller/water-up-loop.anim`
- `Assets/Controller/water-down.anim`

**Unity 编辑器操作**
等待资源导入及编译后运行。body 预制体 WaterTransferVisual 的 Reveal Duration 控制展开时长，默认 0.25 秒；帧动画子对象默认隐藏。

**注意**
独立编译、帧序列与循环配置、资源引用检查通过；现有关卡模型测试通过。当前无法连接编辑器，尚未进行实际播放验收。未提交 Git。

## 2026-09-17 上层水柱遮挡下层 Up

**原因**
所有水柱共用 Body 层级，Up 统一高一层，导致下层 Up 显示在上层 Body 前面。

**修改**
- TubeView 按槽位自下向上分配显示层级，每格间隔 3 层，保留 glow、body、up 的顺序。
- 选中水柱仍保留组内层次，并为最上层 Up 预留位置，避免盖过试管前景。
- 取消选择、转移落入目标管及溢出放回时，按实际槽位恢复排序。

**主要文件**
- `Assets/Scripts/Game/TubeView.cs`
- `Assets/Scripts/Game/GameController.cs`

**Unity 编辑器操作**
无额外配置，等待编译后运行查看。

**注意**
相关脚本使用项目 Unity、DOTween 与 Playworks 程序集编译通过；尚未在 Unity 中实际播放验证。未提交 Git。

## 2026-09-17 小球替换为七格水柱

**原因**
按水柱示例显示每个颜色单元，每根试管容量为 7，相邻单元位置间距为 0.8。

**修改**
- 将 Water/body.prefab 接入 BallView，保留示例 body/up 的外观比例，增加独立 visualRoot 以沿用选中、移动、淡出与发光效果。
- 6 根试管各保留 7 个居中槽位，从世界 Y=-5.2 开始，每格增加 0.8；删除场景中的 7 个静态示例以免与生成对象重叠。
- 初始配置改为五种颜色各 7 个，共 35 个单元，保留一根空管；以实际 TubeModel 验证可解。
- 颜色表统一使用 body Sprite，Tint 控制主体颜色；Up 自动向白色提亮，透明度和排序跟随主体。

**主要文件**
- `Assets/Scenes/main.unity`
- `Assets/prefabs/Water/body.prefab`
- `Assets/prefabs/BallVisualPalette.asset`
- `Assets/Scripts/Game/BallView.cs`
- `Assets/Scripts/Game/BallVisualPalette.cs`
- `Assets/Scripts/Game/WaterBodyTopAnchor.cs`

**Unity 编辑器操作**
- 重新加载 main 场景并运行查看生成的水柱。
- 在 BallVisualPalette 的各颜色条目中调整 Tint。
- 在 body 预制体内的 WaterBodyTopAnchor 调整 Up Lighten Amount（默认 0.4）和 Top Offset。

**注意**
独立编译、资源引用、槽位间距和实际 TubeModel 关卡测试通过；当前编辑器连接不可用，尚未进行 Unity 播放和画面验收。未提交 Git。

## 2026-09-17 Body 顶部对齐 Up

**原因**
Body 从底部向上伸长时，需要 Up 跟随顶部且保持自身尺寸。

**修改**
- 添加 WaterBodyTopAnchor，挂在 Body 上，自动获取自身 SpriteRenderer。
- 使用实际渲染边界将 Up 底边对齐 Body 顶边，只修改位置；编辑模式及运行时生效。
- 已配置 main 场景 Body 的 Up 引用，Top Offset 可微调接缝。

**主要文件**
- `Assets/Scripts/Game/WaterBodyTopAnchor.cs`
- `Assets/Scenes/main.unity`

**Unity 编辑器操作**
重新加载 main 场景；Body 和 Up 保持同级，组件与引用已配置。

**注意**
已用 Unity 2022.3 程序集进行独立编译检查，尚未在 Unity 中实际预览。

## 2026-09-17 water Body 启用九宫格拉伸

**原因**
Body 使用 Simple 绘制模式，Sprite Border 未参与尺寸调整。

**修改**
- Body 的 SpriteRenderer 改为 Sliced，图片 Mesh Type 改为 Full Rect。
- 保留已有 Border、Transform 和 Size 设置。

**主要文件**
- `Assets/Scenes/main.unity`
- `Assets/prefabs/Water/body.png.meta`

**Unity 编辑器操作**
重新加载场景；后续通过 SpriteRenderer Size Y 调整高度，保持 Transform Scale 固定。

**注意**
未在编辑器预览验证。Size Y 小于上下边界总高度 4.61 时，首尾仍会被压缩。

## 2026-09-17 试管小球改为居中单列

**原因**
试管内小球改为单列，槽位只保留一侧并居中。

**修改**
- 6 根试管各保留 16 个槽位，删除右侧槽位，将保留槽位对齐试管 Sprite 中心。
- 初始小球按原左右配对缩减为单列，5 种颜色各 16 个，共 80 个，保持颜色顺序和纵向间距。

**主要文件**
- `Assets/Scenes/main.unity`

**Unity 编辑器操作**
- 已直接调整场景配置；在 Unity 重新加载 main 场景后查看。

**注意**
未进行 Unity 运行验证；未提交 Git。

