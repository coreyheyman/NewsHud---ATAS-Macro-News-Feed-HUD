# NewsHud - ATAS Macro & News Feed HUD

**NewsHud** is a custom, production-grade visual indicator built for **ATAS (Advanced Trading Analytical Software)**. It renders a clean, interactive Heads-Up Display (HUD) directly on your trading charts, aggregating real-time US macroeconomic calendar events and live financial news tailored for US Equity futures traders (specifically Nasdaq/MNQ).

---

## Key Features

* **Zero-Configuration Plug-and-Play**: Feed URLs (Forex Factory JSON and CNBC RSS) are hardcoded and automatically managed under the hood. No manual setup required.
* **Smart Event Filtering**: Automatically filters for **USD-only** macro events and highlights high-impact catalysts (e.g., FOMC, CPI, NFP, Geopolitics).
* **Autonomous Error Recovery**: Features a built-in **Circuit Breaker** pattern and proxy fallbacks to seamlessly handle network drops or server rate-limiting (HTTP 429) without crashing or freezing your chart.
* **Interactive UI & Auto-Expiry**:
* Click any headline in the HUD to expand it and read full summary details or actual/forecast figures.
* Past calendar events automatically purge from the display **15 minutes** after they pass to keep your screen uncluttered.


* **Resource Optimization**: Implements strict `static` memory caching and proper `OnDispose` cleanup to prevent background memory leaks or "zombie" timers.

<img width="360" height="374" alt="image" src="https://github.com/user-attachments/assets/91f9cc00-d930-437d-b72a-09e1f5fc4f0b" />
<img width="617" height="544" alt="image" src="https://github.com/user-attachments/assets/00f5f6f9-fabf-4ed5-abaa-ac5a05351a7f" />


---

## Installation & Setup Guide

Getting NewsHud running on your ATAS platform takes just a few minutes. Follow these steps:

### Step 1: Open Your ATAS Custom Indicators Folder

1. Open **ATAS**.
2. Navigate to the main menu and open the **Indicator Editor** (or press `Ctrl + I` to open indicators, click **Add**, and select **Custom / Visual Indicator** or open your workspace source folder).
3. Place the NewsHud source files (`TradeHudVisualIndicator.cs`, `MarketDataManager.cs`, and the service files) into your ATAS custom indicators directory (typically located under your Documents folder or ATAS installation path under `CustomIndicators`).

### Step 2: Compile in Visual Studio

1. Open the project solution containing your NewsHud files in **Visual Studio**.
2. Ensure you have the required dependencies referenced (`ATAS.Indicators`, `OFT.Rendering`, etc.).
3. Press **`Ctrl + Shift + B`** to build/compile the solution. Verify that there are zero compilation errors.

### Step 3: Add to Your Chart in ATAS

1. Return to your active ATAS chart window.
2. Right-click the chart and select **Indicators** (or press `Ctrl + I`).
3. Scroll through your indicator list and find **`NewsHud - Macro & News Feed`**.
4. Click **Add** and apply it to your chart.

### Step 4: Final Verification

* The HUD will instantly render in the top-left corner of your chart (fully customizable via settings for position and offsets).
* The default ATAS plot line is hidden automatically, leaving you with a sleek, minimalist trading HUD.
* Enjoy an out-of-the-box, reliable news and macro feed!
