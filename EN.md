
# 🌟 Star Resonance Toolbox

[![License: AGPL v3](https://img.shields.io/badge/License-AGPL%20v3-brightgreen.svg)](https://www.gnu.org/licenses/agpl-3.0.txt)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-blue)](https://dotnet.microsoft.com/)
[![GitHub Releases](https://img.shields.io/github/v/release/yourusername/StarResonanceToolBox?label=Release)](../../releases)
[![GitHub Stars](https://img.shields.io/github/stars/yourusername/StarResonanceToolBox?style=social)](../../stargazers)

**Star Resonance Toolbox** is an open-source analytical and utility suite for the game *Star Resonance*, focused on **combat performance tracking**, **data visualization**, and **player improvement tools** — without modifying or interfering with the game client.

It is built by and for the community, with transparency, performance, and education in mind.

---

## 🧠 What It Does

* ✅ **Real-time damage and healing tracking**
* 📊 **Detailed performance visualization** (charts, trends, skill distribution)
* 🔍 **Class and skill breakdowns** for deeper understanding
* 💾 **Local data history** for long-term progress tracking
* ⚙️ **Modular architecture** — easy to extend and maintain
* 🧩 **Integration-ready API** for third-party tools and web dashboards
* 🕹️ **No client modification or injection required**

> The tool only reads publicly available data streams and does **not** hook or alter the game’s memory, ensuring compliance with *Star Resonance’s* Terms of Service.

---

## 🔧 Architecture Overview

The project consists of several modular components:

| Module                               | Description                                             |
| ------------------------------------ | ------------------------------------------------------- |
| **StarResonanceDpsAnalysis.Core**    | Core logic, data models, and analysis algorithms        |
| **StarResonanceDpsAnalysis.WinForm** | Classic Windows Forms UI for performance tracking       |
| **StarResonanceDpsAnalysis.WPF**     | Modern WPF interface with charts and statistics         |
| **StarResonanceDpsAnalysis.Assets**  | Shared resources, configuration, and UI assets          |
| **StarResonanceDpsAnalysis.Tests**   | Automated test suite ensuring stability and correctness |

The data capture and analysis backend is **adapted from** [StarResonanceDamageCounter](https://github.com/dmlgzs/StarResonanceDamageCounter).
Huge thanks to the original developers for making this possible.

---

## 🚀 Quick Start

### Prerequisites

* Windows 10 or newer
* [.NET 8.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
* Basic familiarity with running .NET applications

### Building from Source

```bash
git clone https://github.com/yourusername/StarResonanceToolBox.git
cd StarResonanceToolBox
dotnet restore
dotnet build -c Release
```

### Running

```bash
dotnet run --project StarResonanceDpsAnalysis.WPF -c Release
```

Or simply open the solution file (`StarResonanceDpsAnalysis.sln`) in **Visual Studio 2022**, set the WPF project as startup, and run.

---

## 🖼️ Screenshots

*(Add screenshots or GIFs of your UI here)*

| Dashboard                                    | Damage Charts                         | Skill Breakdown                      |
| -------------------------------------------- | ------------------------------------- | ------------------------------------ |
| ![Dashboard](docs/screenshots/dashboard.png) | ![Charts](docs/screenshots/chart.png) | ![Skill](docs/screenshots/skill.png) |

---

## 🧩 Roadmap

| Status | Feature                        | Description            |
| :----: | ------------------------------ | ---------------------- |
|    ✅   | Real-time combat parsing       | Already available      |
|   🚧   | Team DPS comparison            | Planned                |
|   🚧   | Upload to bptimer.com API      | Under testing          |
|   🕓   | Global leaderboard integration | Planned                |
|   🕓   | Plugin-based extension support | Planned future feature |

You can track progress and upcoming milestones in the [Projects](../../projects) section.

---

## 👥 Contributing

We welcome community contributions of all kinds!
Ways you can help:

* Report bugs or suggest ideas via [Issues](../../issues)
* Submit pull requests for fixes or enhancements
* Improve translations and documentation
* Share performance feedback and test results

Before submitting, please ensure your PR:

* Builds without warnings or errors
* Follows the existing project structure
* Respects the [AGPLv3 License](LICENSE.txt)

---

## 🛠️ Technical Stack

* **Language:** C# (.NET 8.0)
* **UI Frameworks:** WPF, WinForms
* **Testing:** xUnit
* **Data Visualization:** LiveCharts, ScottPlot (configurable)
* **Automation:** GitHub Actions for CI/CD
* **Packaging:** Automatic release builds via Actions

---

## 📄 License

[![AGPLv3](https://www.gnu.org/graphics/agplv3-with-text-162x68.png)](LICENSE.txt)

Licensed under the **GNU Affero General Public License v3**.
By using or distributing this project, you agree to comply with its terms.

> Open source should stay open.
> Redistribution of modified code in closed-source or commercial form **violates** this license.

---

## 💬 Community & Support

* 💡 Join discussions via GitHub Issues
* 🌐 Optional integration with [bpsr-logs](https://github.com/Chase-Simmons/BPSR-PSO) and [bptimer.com](https://bptimer.com) for web-based analysis
* 📧 Contact: [Your preferred contact or discussion link]

If you find the project useful, please give it a ⭐ on GitHub — it means a lot!

---

## ⚠️ Disclaimer

This tool is intended **solely for data analysis, learning, and performance improvement**.
It is **not a cheat, exploit, or automation system**, and it does **not modify** any part of the game’s executable or data.

Use responsibly:

* Do not share logs or data to harass or discriminate against other players
* Respect the game’s EULA and community guidelines
* Understand that use is **at your own risk**

The developers are **not responsible** for misuse, bans, or data abuse by third parties.

---

## ❤️ Acknowledgements

* [StarResonanceDamageCounter](https://github.com/dmlgzs/StarResonanceDamageCounter) – Original data collection core
* The *Star Resonance* player community – for feedback, testing, and support

