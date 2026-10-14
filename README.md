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
<img width="1596" height="756" alt="image" src="https://github.com/user-attachments/assets/1d680d0c-5382-4cf3-91cd-1b4da7aeae4f" />
<img width="617" height="544" alt="image" src="https://github.com/user-attachments/assets/00f5f6f9-fabf-4ed5-abaa-ac5a05351a7f" />


---

## Instructions:

Option A: Installing via Compiled .dll File (Easiest) If you shared a pre-compiled .dll file, users can install it instantly without editing code:

Download the .dll file.

Open your ATAS custom indicators folder by pasting this path into your Windows File Explorer address bar: %APPDATA%\ATAS\Indicators

Drop the .dll file directly into that folder.

Open or restart ATAS, open any chart, and press Ctrl + I.

Look under the Custom category to find and add Account Info(Custom).

Option B: Visual Studio

Create a New Class Library Project Open Visual Studio and click Create a new project.
Search for and select Class Library (make sure it's the C# version targeting .NET Framework or the appropriate .NET runtime version your ATAS version uses, typically .NET 10 depending on the ATAS build). Click Next.

Name your project (e.g., AccountInfoCustom), choose your saving location, and click Create.

Add ATAS Reference Assemblies To compile ATAS indicators, your project needs references to the core ATAS libraries (ATAS.Indicators.dll and OFT.Rendering.dll).
In the Solution Explorer on the right, right-click on Dependencies (or References) and select Add Reference... (or Manage NuGet Packages if applicable).

Click Browse and navigate to your ATAS installation directory (usually C:\Program Files\ATAS\ or your user path).

Select the following required DLL files:

ATAS.Indicators.dll

OFT.Rendering.dll

Any other dependencies referenced by your project (like data feed cores).

Click OK to add them. (Tip: In the reference properties, set Copy Local to False since ATAS loads these natively at runtime).

Add the Code File Visual Studio automatically creates a default file named Class1.cs. Right-click it, select Rename, and change it to MyCustomIndicator.cs.
Open the file, delete any placeholder code, and paste your indicator C# source code into it.

Save the file (Ctrl + S).

Build the Project Go to the top menu and select Build > Clean Solution (to clear out old build caches).
Select Build > Build Solution (Ctrl + Shift + B).

Check the Output window at the bottom to ensure it says 1 succeeded, 0 failed.

Deploy to ATAS Once built successfully, go to your project folder in Windows Explorer and find the compiled file located in bin\Debug\ or bin\Release.
Copy the generated .dll file.

Drop it directly into your local ATAS indicators folder: %APPDATA%\ATAS\Indicators

Open ATAS, open a chart, press Ctrl + I, and add your custom indicator from the list!

(If you have any trouble during this process, chatgpt, claude or gemini is your friend)

* The HUD will instantly render in the top-left corner of your chart (fully customizable via settings for position and offsets).
* The default ATAS plot line is hidden automatically, leaving you with a sleek, minimalist trading HUD.
* Enjoy an out-of-the-box, reliable news and macro feed!
