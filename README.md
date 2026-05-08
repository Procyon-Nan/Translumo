[![License](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](https://opensource.org/licenses/Apache-2.0)
[![Github All Releases](https://img.shields.io/github/downloads/ramjke/Translumo/total.svg)]()

<p align="center">
  <img width="670" src="https://github.com/ramjke/Translumo/assets/29047281/8985049f-ea1c-428e-94be-042ece66cb54">
</p>
<h2 align="center" style="border: 0">Translumo AI 屏幕翻译器</h2>

<p align="center"><strong>中文</strong> | <a href="docs/README-EN.md"><strong>English</strong></a> | <a href="docs/README-RU.md"><strong>Русский</strong></a></p>

## 二次开发说明

本仓库是基于原作者 **ramjke** 的 **[Translumo](https://github.com/ramjke/Translumo)** 项目进行的二次开发版本。原项目的所有权、版权、许可证、贡献记录与项目归属仍属于原作者和原项目贡献者。

本分支的修改重点是将屏幕文字识别和翻译流程改造为 OpenAI 兼容的 AI 工作流，并补充中文本地化、运行日志、截图诊断和更适合单次选区翻译的交互方式。

当前英文 README 已移动到 [docs/README-EN.md](docs/README-EN.md)，根目录 README 优先展示中文内容。

## 本分支主要变化

- 使用 AI 图片文字识别替代原有本地 OCR 识别流程。
- 将 AI OCR 识图配置与 AI 文本翻译配置完全分离，分别拥有 Base URL、API Key、模型、超时和提示词。
- 保留 DeepL、Google、Yandex、Papago 等原有翻译器的手动选择能力。
- 新增 OpenAI 兼容 AI 翻译器，文本翻译请求会使用 `<text>...</text>` 标签严格限定待翻译内容。
- 移除连续翻译模式，只保留 `Shift+F` 单次选区并翻译。
- `Shift+F` 会先冻结鼠标所在屏幕，再在冻结截图上选区，避免悬浮文本在鼠标移开后消失。
- 每次截图会归档到系统临时目录，便于检查 OCR 输入图片。
- 设置页新增运行日志页面，可查看截图、OCR、翻译等关键步骤的诊断信息。
- 补充中文本地化，并为 OCR 提示词和翻译提示词增加恢复默认按钮。

## 使用方式

1. 启动 Translumo 后，按 `Alt+G` 打开设置窗口。
2. 在语言与翻译设置中选择源语言和目标语言。
3. 在 `AI text recognition` 区域配置 OCR 用的 OpenAI 兼容 Base URL、API Key 和模型。
4. 如果翻译器选择 `AI`，在 `AI translation` 区域配置文本翻译用的 Base URL、API Key 和模型。
5. 当屏幕上出现需要翻译的文本时，按 `Shift+F`。
6. 程序会先冻结鼠标所在屏幕，在冻结画面上框选区域后，只执行一次 OCR 与翻译。
7. 识别后的文字会送入当前选择的翻译器，翻译结果显示在翻译窗口中。

## AI 接口说明

AI OCR 与 AI 翻译都使用 OpenAI 兼容接口：

- `GET {baseUrl}/models` 用于获取模型列表。
- `POST {baseUrl}/chat/completions` 用于 OCR 识图或文本翻译。
- `baseUrl` 需要包含版本前缀，例如 `https://api.example.com/v1`。
- API Key 通过 `Authorization: Bearer {apiKey}` 发送。
- OCR 请求会发送 PNG data URL 图片，不会在日志中记录完整 base64 内容。
- 翻译请求会把待翻译文本包裹在 `<text>...</text>` 中，避免远端提示词后处理误翻译无关内容。

## 日志与截图诊断

设置页左侧的“日志”页面会显示当前进程内最近的运行日志，包括截图、图片尺寸、模型、请求耗时、HTTP 状态码、OCR 文本和翻译文本等信息。

截图诊断文件保存在系统临时目录：

```text
%TEMP%\Translumo\Screenshots
```

程序最多保留最近 5 次触发的冻结全屏图与 OCR 裁剪图，方便确认实际发送给 OCR 的图片内容。

## 构建

推荐使用 Visual Studio 2022，并安装 .NET 桌面开发工作负载和 .NET 8 SDK。

也可以在仓库根目录运行：

```powershell
dotnet build Translumo.sln -c Debug
```

## 系统要求

- Windows 10 version 2004 build 19041 或更高版本，或 Windows 11。
- 支持 DirectX 11 的显卡。
- 建议至少 2 GB RAM。
- 使用 AI OCR 和 AI 翻译时，需要可访问的 OpenAI 兼容 API 服务。

## 原项目能力

Translumo 原项目面向 PC 游戏和屏幕内容翻译场景，提供屏幕取词、翻译窗口覆盖显示、多翻译器支持和多语言界面等能力。

原项目支持的翻译器包括 DeepL、Google Translate、Yandex Translate、Naver Papago。本分支在此基础上增加 AI 翻译，并改造 OCR 流程为 AI 图片文字识别。

## 原项目与致谢

- 原项目：[ramjke/Translumo](https://github.com/ramjke/Translumo)
- 原作者：**ramjke** 与 Translumo 贡献者
- 许可证：Apache License 2.0

感谢原作者和贡献者提供 Translumo 的基础项目。本仓库的所有二次开发均建立在该项目之上，并继续遵守原项目许可证。
