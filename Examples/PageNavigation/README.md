# Page Navigation Demo

This project demonstrates that `info.json`'s `start` file is the only page shown when the app starts. `test.txl` is compiled as a project route, but it opens only after clicking the button in `main.txl`.

## Check and run

From this folder, with the TXL CLI and .NET 9 SDK installed:

```powershell
txl --check
txl --preview
txl --build
```

On Windows, click **Open test.txl** to navigate. On that page, click **Return to main** or press Backspace to return. The compiler scans other project `.txl` files and includes them as routes; scanning them does not display them at startup.
