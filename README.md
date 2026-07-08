# StardewValleyAIMod · 星露谷物语 AI 对话 mod

让《星露谷物语》里的主要 NPC 接入 AI 对话。mod 本身**不做任何模型推理**，只做请求中转：玩家在游戏内填写自己的 AI 网址、API Key 和模型名称，mod 把对话请求原样转发出去，再把 AI 返回的回复显示在游戏里。

每轮对话前，mod 会把对应 NPC 的人设（性格、口癖、人际关系、立场）作为 system 消息发给目标接口，用于塑造角色、避免出戏。

---

## 目录

- [特性](#特性)
- [环境要求](#环境要求)
- [安装](#安装)
- [使用方法](#使用方法)
- [设置窗口字段说明](#设置窗口字段说明)
- [支持人设的 NPC](#支持人设的-npc)
- [settings.json 配置文件](#settingsjson-配置文件)
- [常见问题 FAQ](#常见问题-faq)
- [隐私与安全](#隐私与安全)
- [从源码构建](#从源码构建)
- [架构说明](#架构说明)

---

## 特性

- **安装即用**：玩家无需改代码、无需配置环境，进游戏按 `K` 在弹出的设置窗口里填好网址和 Key 即可。
- **纯请求中转**：mod 不内置任何密钥，Authorization 头现拼现发，所有 AI 能力都来自玩家自己接入的接口。
- **角色塑造**：为 27 位主要 NPC 各写了专属人设 prompt，对话前发送，避免 AI 出戏、跑题或承认自己是模型。
- **人设预热**（可选）：首次正式对话前可单独发一条 system 消息"预热"接口，塑造角色。
- **多轮上下文**：每个 NPC 独立保留对话历史，可配置保留轮数。
- **连接测试**：设置窗口内置"测试连接"按钮，先测后存，避免配置错误。
- **游戏内体验**：回复既显示在自定义菜单里，也推入 NPC 原生对话队列，可用游戏内对话气泡复看。

---

## 环境要求

| 组件 | 版本 / 要求 |
| --- | --- |
| 星露谷物语 Stardew Valley | 1.6 及以上（SMAPI 3.14 兼容版） |
| SMAPI | 3.14.0 或更高 |
| 操作系统 | Windows / macOS / Linux（SMAPI 支持的平台均可） |
| AI 接口 | 任何 **OpenAI 兼容**的 `chat/completions` 端点 |

> 兼容示例：OpenAI 官方 API、Azure OpenAI、Moonshot（Kimi）、DeepSeek、智谱 GLM、零一万物、OpenRouter、本地 Ollama / LM Studio 等，只要支持 `POST /v1/chat/completions`、返回 `choices[0].message.content` 即可。

---

## 安装

### 方式一：直接用已编译版本（推荐普通玩家）

1. 从本仓库 [Releases](../../releases) 页面下载最新版压缩包。
2. 解压后应得到一个文件夹，里面至少包含：
   ```
   StardewValleyAIMod/
   ├── manifest.json
   └── StardewValleyAIMod.dll
   ```
3. 把整个 `StardewValleyAIMod` 文件夹复制到星露谷的 `Mods/` 目录下。最终路径形如：
   ```
   <游戏根目录>/Mods/StardewValleyAIMod/manifest.json
   <游戏根目录>/Mods/StardewValleyAIMod/StardewValleyAIMod.dll
   ```
4. 通过 SMAPI 启动游戏（通常双击 `StardewModdingAPI.exe`，或用启动器）。看到控制台输出类似 `AI 对话 mod 已加载` 即安装成功。

### 方式二：从源码自行编译

见文末 [从源码构建](#从源码构建)。

---

## 使用方法

### 第一步：打开设置窗口填写接口信息

1. 进入游戏（有存档或新建存档均可）。
2. 按下 **`K` 键**，弹出「AI 对话设置」窗口。
3. 依次填写（详见 [设置窗口字段说明](#设置窗口字段说明)）：
   - **您选用的 AI 的网址是**：你的接口地址，例如 `https://api.openai.com/v1/chat/completions`
   - **您的 API 是**：你的 API Key，例如 `sk-...`
   - **模型名称**：例如 `gpt-3.5-turbo`
   - **附加指令（可选）**：对回复风格的要求，如「请用简短中文回复」
4. 点击 **「测试连接」**：mod 会用你填的临时信息发一条最小请求验证连通性。成功会显示 `✓ 连接成功！可以保存后开始对话。`
5. 点击 **「保存」**：信息写入 `Mods/StardewValleyAIMod/settings.json`。下次启动游戏会自动读取，无需重复填写。

> 窗口内可用 `Tab` 切换输入框、`回车`保存、`Esc`取消。

### 第二步：与 NPC 对话

1. 走到任意主要 NPC 附近（约 2.5 格范围内）。
2. 按下 **`L` 键**，弹出「AI 对话」窗口，标题会显示该 NPC 的名字。
3. 在输入框里输入你想说的话，按 **回车** 或点击 **「发送」**。
4. 等待片刻（菜单显示「思考中……」），AI 的回复会显示在回复区，同时推入 NPC 的原生对话队列。

### 快捷键一览

| 快捷键 | 作用 |
| --- | --- |
| `K` | 打开 AI 设置窗口 |
| `L` | 与附近的 NPC 开始 AI 对话 |

> 快捷键可在 `Mods/StardewValleyAIMod/config.json` 中修改（见下文）。该文件由 SMAPI 在首次运行时自动生成，**不是必填**。

### 第一次使用时

如果尚未配置 AI 接口就按了 `L`，mod 会自动弹出设置窗口并提示「尚未配置 AI 接口，已为你打开设置窗口」。按提示填好保存即可。

---

## 设置窗口字段说明

| 字段 | 说明 | 示例 |
| --- | --- | --- |
| 您选用的 AI 的网址是 | OpenAI 兼容的 chat/completions 端点，需带 `https://` | `https://api.openai.com/v1/chat/completions` |
| 您的 API 是 | 接口密钥，mod 以 `Authorization: Bearer <key>` 形式转发 | `sk-xxxxxxxx` |
| 模型名称 | 请求体里的 `model` 字段，按你的服务商填写 | `gpt-3.5-turbo` / `deepseek-chat` / `moonshot-v1-8k` |
| 附加指令 | 追加到 system 消息末尾的通用要求 | `请用简短、口语化的中文回复` |

---

## 支持人设的 NPC

以下 27 位主要 NPC 有专属人设 prompt（见 [Data/CharacterPrompts.cs](Data/CharacterPrompts.cs)）。其他 NPC 会使用通用设定，也能对话，只是角色感稍弱。

**可攻略角色（12）**
Abigail（阿比盖尔）、Penny（潘妮）、Leah（莉亚）、Maru（玛鲁）、Haley（海莉）、Emily（艾米丽）、Alex（亚历克斯）、Elliott（艾利欧特）、Harvey（哈维）、Sam（山姆）、Sebastian（塞巴斯）、Shane（谢恩）

**其他主要村民（15）**
Robin（罗宾）、Demetrius（德米特里乌斯）、Pierre（皮埃尔）、Caroline（卡洛琳）、Lewis（路易斯）、Marnie（玛尼）、Linus（莱纳斯）、Willy（威利）、Wizard（法师）、Krobus（克罗巴斯）、Sandy（珊迪）、Jodi（乔迪）、Kent（肯特）、Vincent（文森特）、Jas（贾斯）

---

## settings.json 配置文件

由设置窗口写入，位于 `Mods/StardewValleyAIMod/settings.json`。普通玩家无需手动编辑；高级玩家可直接改文件。字段含义：

```jsonc
{
  "ApiUrl": "https://api.openai.com/v1/chat/completions", // 必填，接口地址
  "ApiKey": "sk-...",                                     // API Key
  "Model": "gpt-3.5-turbo",                               // 模型名
  "Temperature": 0.8,                                     // 采样温度（0~2）
  "MaxTokens": 220,                                       // 单条回复最大 token
  "RequestTimeoutSeconds": 30,                            // 请求超时（秒）
  "ConversationHistoryLength": 6,                         // 保留的对话历史轮数
  "SendPrimingRequest": false,                            // 是否在首次对话前发人设预热请求
  "ExtraSystemInstruction": "请用简短、口语化的中文回复……",// 追加到 system 的通用指令
  "InteractionRange": 2.5                                 // 与 NPC 互动的最大距离（格）
}
```

### 关于 `SendPrimingRequest`

- `false`（默认）：人设作为 `system` 消息随每条请求一起发送。兼容性最好，推荐大多数情况使用。
- `true`：在首次正式对话前，mod 会单独发一条只含 system 消息的最小请求进行"预热"，塑造角色。某些需要先建立上下文的服务可能需要打开它。

### config.json（可选）

由 SMAPI 自动生成，仅含快捷键，可不改：

```jsonc
{
  "ToggleKey": "L",   // 与 NPC 对话的快捷键
  "SettingsKey": "K"  // 打开设置窗口的快捷键
}
```

---

## 常见问题 FAQ

**Q：mod 是不是自带 AI？需要付费吗？**
A：不是。mod 只做请求中转，所有 AI 能力都来自你自己接入的接口（OpenAI、DeepSeek 等）。是否付费取决于你选的服务商。

**Q：显示「请求出错：HTTP 401 ...」**
A：API Key 无效或过期。回到设置窗口（按 `K`）重新填写并测试连接。

**Q：显示「请求出错：HTTP 404 ...」**
A：网址不对。确认填的是 `chat/completions` 端点，不是根域名。例如 OpenAI 应填 `https://api.openai.com/v1/chat/completions`，不是 `https://api.openai.com`。

**Q：显示「请求超时了」**
A：网络不通或接口响应慢。可在 `settings.json` 里把 `RequestTimeoutSeconds` 调大。

**Q：回复里出现了中文乱码 / AI 一直用英文回复**
A：在设置窗口的「附加指令」里写明 `请用简体中文回复`。

**Q：AI 回复承认自己是 AI，或跳出角色**
A：人设 prompt 已经做了约束，但某些模型仍可能越界。可在「附加指令」里追加 `无论怎样都不得承认自己是 AI 或模型，必须始终以角色身份回答`。

**Q：走到 NPC 旁按 L 提示「附近没有可以对话的 NPC」**
A：站得更近一些（默认 2.5 格内），或在 `settings.json` 里调大 `InteractionRange`。

**Q：换了电脑要重新配置吗？**
A：`settings.json` 保存在本机 mod 目录下，不随存档同步。新电脑上需要重新填写一次。

**Q：会不会泄露我的 API Key？**
A：Key 只保存在你本机的 `settings.json`（已被 `.gitignore` 排除，不会被提交），请求时以 `Authorization: Bearer` 头发出，不会上传到 mod 作者或任何第三方。

---

## 隐私与安全

- mod **不内置任何密钥**，不向 mod 作者回传任何数据。
- 你填写的 API Key 仅保存在本机的 `Mods/StardewValleyAIMod/settings.json`。
- 请求只发生在「你的游戏」与「你填写的接口」之间，mod 仅做中转。
- `settings.json` 已加入 `.gitignore`，若你 fork 本仓库并自行开发，注意不要把它提交上去。
- 请妥善保管你的 API Key，不要截图分享设置窗口内容时连 Key 一起露出。

---

## 从源码构建

适合想自己改 prompt、加功能或排查问题的开发者。

### 依赖

- [.NET 6 SDK](https://dotnet.microsoft.com/download/dotnet/6.0)
- [SMAPI](https://smapi.io/)（开发用，提供游戏 API 引用）
- 已安装的星露谷物语 1.6（提供游戏本体程序集）

### 步骤

```bash
git clone https://github.com/hanshuqimen/StardewValleyAIMod.git
cd StardewValleyAIMod
dotnet build
```

构建产物在 `bin/Debug/net6.0/` 下，包含 `StardewValleyAIMod.dll`。把 `StardewValleyAIMod.dll` 和 `manifest.json` 一起放进游戏的 `Mods/StardewValleyAIMod/` 目录即可。

> 若 `Pathoschild.Stardew.ModBuildConfig` 找不到游戏路径，按提示设置游戏安装目录，或在 `StardewValleyAIMod.csproj` 里用 `<GamePath>` 属性指定。

---

## 架构说明

```
manifest.json              SMAPI mod 清单
StardewValleyAIMod.csproj  .NET 6 工程（ModBuildConfig 自动引用游戏程序集）
ModEntry.cs                入口：加载快捷键/设置、监听按键、打开菜单
ModConfig.cs               仅快捷键（带默认值，可选编辑）
ModSettings.cs             运行时 AI 设置（网址/Key/模型等）
Data/
  CharacterPrompts.cs      27 位主要 NPC 的人设 prompt
Services/
  AiService.cs             纯 HTTP 中转 + 人设预热 + 连接测试
  ConversationStore.cs     每个 NPC 的多轮上下文
  SettingsStore.cs         读写 settings.json
Menus/
  AiDialogueMenu.cs        AI 对话窗口（输入框 + 回复区）
  SettingsMenu.cs          AI 设置窗口（表单 + 测试/保存/取消）
```

**数据流**：

```
玩家输入 ──▶ SettingsMenu ──▶ settings.json（本机保存）
                                       │
玩家按 L ──▶ AiDialogueMenu ──▶ AiService（拼 system+history+user）
                                       │
                       HTTP POST ▶ 玩家填写的 AI 接口
                                       │
                  ◀── choices[0].message.content ──
                       │
            显示在菜单 + 推入 NPC 原生对话队列
```

---

## 许可

本项目按 MIT 协议开源，详见仓库根目录的 LICENSE（如有）。欢迎二次开发，但请勿以此 mod 名义收集他人 API Key。
