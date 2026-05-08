[![License](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](https://opensource.org/licenses/Apache-2.0)

<p align="center">
  <img width="670" src="https://github.com/ramjke/Translumo/assets/29047281/8985049f-ea1c-428e-94be-042ece66cb54">
</p>
<h2 align="center" style="border: 0">Translumo AI Screen Translator</h2>

<p align="center"><a href="../README.md"><strong>中文</strong></a> | <strong>English</strong> | <a href="README-RU.md"><strong>Русский</strong></a></p>

## Secondary Development Notice

This repository is a secondary development fork based on the original **[Translumo](https://github.com/ramjke/Translumo)** project by **ramjke** and its contributors. The original project ownership, copyright, license, and contribution history remain with the original author and contributors.

This fork replaces the local OCR workflow with OpenAI-compatible AI image text recognition and adds OpenAI-compatible AI translation, Chinese localization, runtime logs, screenshot diagnostics, prompt reset controls, and a single-shot frozen screenshot selection flow.

## Main Changes

- AI image text recognition replaces the old local OCR engines.
- AI OCR and AI translation use separate Base URL, API key, model, timeout, and prompt settings.
- DeepL, Google, Yandex, and Papago remain available as manual text translator choices.
- AI translation wraps source text in `<text>...</text>` so the remote model only translates the intended content.
- Continuous translation has been removed. Use `Shift+F` to freeze the screen, select an area, and run OCR plus translation once.
- Recent full screenshots and OCR crops are archived under `%TEMP%\Translumo\Screenshots` for diagnostics.
- The Settings window includes a runtime log page for capture, OCR, translation, and API diagnostics.

## Usage

1. Start Translumo and press `Alt+G` to open Settings.
2. Select source and target languages in the language/translation settings.
3. Configure `AI text recognition` with an OpenAI-compatible Base URL, API key, and OCR model.
4. If the translator is set to `AI`, configure `AI translation` with its own Base URL, API key, and translation model.
5. When the text is visible, press `Shift+F`.
6. Translumo freezes the screen under the mouse, lets you select an area on that frozen image, then runs OCR and translation once.

## API Shape

- `GET {baseUrl}/models` loads model lists.
- `POST {baseUrl}/chat/completions` performs AI OCR and AI translation.
- `baseUrl` should include the API version path, for example `https://api.example.com/v1`.
- API keys are sent as `Authorization: Bearer {apiKey}`.
- OCR sends a PNG data URL image. Logs never record the full base64 image body.

## Build

Use Visual Studio 2022 with the .NET desktop development workload and .NET 8 SDK, or run:

```powershell
dotnet build Translumo.sln -c Debug
```

## Requirements

- Windows 10 version 2004 build 19041 or later, or Windows 11.
- DirectX 11 compatible GPU.
- Recommended: at least 2 GB RAM.
- An accessible OpenAI-compatible API service for AI OCR and AI translation.

## Original Project

- Original project: [ramjke/Translumo](https://github.com/ramjke/Translumo)
- License: Apache License 2.0
- Credits remain with the original author and contributors. This fork builds on their work.
