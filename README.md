# 🎁 Lethe Gift Injector

---

### 🛠️ 1. Installation & Execution
* **Installation**: Place the `.dll` file into the `BepInEx\plugins` folder within your game directory.
* **Toggle UI**: Press **`F5`** in-game to open or close the control panel.

---

### 🔑 2. How to Obtain Your Unique Token
1. **Access the Website**: Go to [[https://lethelc.site/auth](https://lethelc.site/auth)] and navigate to the **'Save Info'** menu.
2. **Open Developer Tools**: Press **`F12`** to open the inspection window.
3. **Trigger Data Fetch**: Click the **[Fetch current State]** button at the top (under the Normal Dungeon section).
4. **Locate the Request**: In the Developer Tools (Network tab), find the item with the **`{;}`** icon (Type: fetch) and click it.
5. **View Payload**: Go to the **[Payload]** tab and click **[View source]** to see the raw text.
6. **Copy Token**: Find the string `{"token":"YOUR_TOKEN_HERE"}`.
7. **Important**: Copy **ONLY** the text inside the double quotes (`""`). Do not include the quotes themselves.
    * *Example: If it shows `{"token":"abc123xyz"}`, copy only `abc123xyz`.*
8. **Save in Injector**: Paste it into the **Token** field in the Injector UI and click **[Save]**.

---

### 💉 3. Injection Steps (Follow in Order)

**Step 1: Retrieve Data (① Fetch State)**
* Click **[① Fetch State]** to sync with the server.
* Wait for the `Fetch Success` message and check your current gift count to confirm connection.

**Step 2: Select Gifts**
* **From List**: Use the keyword filters and click **[+]** to add gifts to the queue.
* **Manual Entry**: Enter a specific Gift ID and Tier (0–2), then click **[Add]**.
* **Manage**: Use the **[X]** button in the 'Pending List' to remove any unwanted items.

**Step 3: Sync to Server (② Update State)**
* Click **[② Update State]** to finalize the process.
* The injection is successful once you see the `Update Success` message.

---

### ⚠️ 4. Important Notes & Tips
* **Timing**: You must complete the update **BEFORE** entering a Mirror Dungeon or starting a battle for changes to apply.
* **Duplicates**: The injector automatically ignores gifts that are already in your inventory.
* **Troubleshooting**: If you encounter a `422 Error`, double-check your token or report the issue with logs to the developer.
