# Unity多语言文本翻译系统使用指南

## 概述

这是一个为Unity开发的多语言文本翻译系统，可以在Unity编辑器中一键将Text组件的文本翻译成多种语言。支持使用OpenRouter AI API（需要API Key）或免费的MyMemory翻译API。

## 功能特点

- ✅ **编辑器内翻译**：无需运行游戏即可翻译文本
- ✅ **批量翻译**：支持批量翻译场景中所有Text组件
- ✅ **AI智能翻译**：支持OpenRouter AI（Claude、GPT等模型）
- ✅ **免费翻译选项**：内置MyMemory免费翻译API
- ✅ **游戏语境支持**：可添加游戏场景描述提高翻译质量
- ✅ **配置管理**：集中管理API Key、目标语言等设置
- ✅ **灵活的语言选择**：支持30+种语言

## 文件说明

### 核心脚本
- `MLT_TextSwitch.cs` - 多语言文本切换组件（挂载到Text组件上）

### 编辑器脚本
- `MLT_TextSwitchEditor.cs` - MLT_TextSwitch组件的自定义编辑器
- `MLT_FreeTranslationEditor.cs` - 免费翻译版本的编辑器（备选方案）
- `MLT_BatchTranslatorWindow.cs` - 批量翻译窗口
- `MLT_TranslationConfig.cs` - 翻译配置管理

## 使用步骤

### 1. 配置翻译设置

打开菜单：`Tools > MLT Translation > Configuration`

#### 基础配置：
- **API Key**：如果有OpenRouter API Key，填入此处（可选）
- **首选AI模型**：选择AI模型（默认claude-3.5-sonnet）
- **游戏语境**：描述游戏场景，如"手机休闲电子游戏"

#### 目标语言：
- 勾选需要翻译的目标语言
- 提供快速选择按钮：常用语言、亚洲语言、欧洲语言等

### 2. 单个文本组件翻译

1. 选中带有Text组件的GameObject
2. 添加`MLT_TextSwitch`组件
3. 在Inspector中会看到自定义编辑器界面
4. 选择源语言（默认自动检测）
5. 点击"Translate to All Languages"按钮
6. 等待翻译完成

### 3. 批量翻译

打开菜单：`Tools > MLT Translation > Batch Translator`

1. **扫描场景**：窗口会自动扫描当前场景的所有Text组件
2. **筛选选项**：
   - Include Inactive：是否包含非活跃对象
   - Only Missing Translations：仅显示缺少翻译的文本
3. **选择文本**：勾选需要翻译的文本
4. **添加MLT_TextSwitch**：为选中的Text组件添加多语言支持
5. **批量翻译**：点击"Translate Selected"开始批量翻译

### 4. 快捷菜单

- `GameObject > UI > Create MLT Text`：创建带有MLT_TextSwitch的Text对象
- `Component > UI > Add MLT_TextSwitch`：为选中的对象批量添加MLT_TextSwitch组件

## API说明

### OpenRouter AI API（推荐）

- 需要API Key：https://openrouter.ai/
- 支持多种AI模型：Claude、GPT、Gemini等
- 翻译质量高，支持游戏语境
- 按使用量付费

### MyMemory API（免费）

- 无需API Key
- 每日限额：1000个请求
- 翻译质量一般
- 适合小项目或测试使用

## 注意事项

1. **API限制**：
   - OpenRouter需要付费API Key
   - MyMemory有每日请求限制
   - 建议设置合理的API延迟避免请求过快

2. **翻译质量**：
   - 使用AI翻译时，提供详细的游戏语境描述可提高质量
   - 翻译后建议人工审核，特别是专业术语

3. **性能优化**：
   - 批量翻译时会显示进度条
   - 大量文本翻译可能需要较长时间
   - 支持断点续传（翻译失败的会重试）

4. **最佳实践**：
   - 先在少量文本上测试翻译效果
   - 保存场景前确保翻译完成
   - 定期备份项目

## 故障排除

### 翻译失败
- 检查网络连接
- 验证API Key是否正确
- 查看Console中的错误信息
- 尝试减少同时翻译的数量

### 翻译质量差
- 添加更详细的游戏语境描述
- 尝试不同的AI模型
- 对特殊术语进行手动调整

### API限制
- OpenRouter：检查账户余额
- MyMemory：等待24小时后重试或使用OpenRouter

## 扩展开发

如需添加其他翻译API支持，可以：

1. 在`MLT_TextSwitchEditor.cs`或`MLT_BatchTranslatorWindow.cs`中添加新的翻译方法
2. 在`MLT_TranslationConfig.cs`中添加相应的配置选项
3. 实现相应的API调用逻辑

## 更新日志

- v1.0：初始版本
  - 支持OpenRouter AI API
  - 支持MyMemory免费API
  - 批量翻译功能
  - 配置管理系统