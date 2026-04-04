# 🏠 SimpleHomes

A **lightweight, high-performance teleport system** for Rust servers.

Built to replace heavy multi-system plugins like NTeleportation with a **clean, optimized, single-purpose solution** focused on:

* Home teleports
* Outpost & Bandit teleports
* Daily limits
* Performance-first design

---

## ✨ Features

### 🏡 Homes

* `/home add <name>` — set a home
* `/home <name>` — teleport to a home
* `/home list` — list your homes
* `/home remove <name>` — remove a home

### 🏙 Town Teleports

* `/outpost` — teleport to Outpost
* `/bandit` — teleport to Bandit Camp
* `/cancelteleport` — cancel any active teleport

---

## ⚡ Performance Focus

SimpleHomes is designed to be **extremely lightweight**:

* ✔ No unnecessary hooks
* ✔ No polling or loops
* ✔ Timer-based teleport system
* ✔ Cached VIP permissions
* ✔ Batched data saves (no disk spam)
* ✔ Minimal allocations

👉 This makes it ideal for **high-pop servers**

---

## 🔄 Migration Support

Automatically migrates homes from:

* NTeleportation

Supported formats:

* `"x y z"` string positions
* object vector positions `{ x, y, z }`

---

## ⚙️ Configuration

```json
{
  "Chat": {
    "Prefix": "<color=#00BFFF>SimpleHomes</color>: ",
    "Chat Steam64ID": 76561198721088475
  },

  "Migration": {
    "Auto Migrate NTeleportation Homes": true,
    "Mark Migration Complete Even If Source Missing": false
  },

  "Home": {
    "Enabled": true,
    "Homes Limit": 2,
    "VIP Homes Limit": 5,

    "Teleport Cooldown Seconds": 300,
    "VIP Teleport Cooldown Seconds": 120,

    "Teleport Countdown Seconds": 15,
    "VIP Teleport Countdown Seconds": 5,

    "Daily Limit": 10,
    "VIP Daily Limit": 50,

    "Require Building Privilege To Set Home": true,
    "Require Building Privilege To Teleport Home": false,

    "Cancel Teleport On Any Damage": true,
    "Cancel Teleport On Player Damage": true,
    "Cancel Teleport On Fall Damage": true
  },

  "Town Teleports": {
    "Enable Outpost": true,
    "Enable Bandit": true,

    "Outpost Command": "outpost",
    "Bandit Command": "bandit",
    "Cancel Command": "cancelteleport",

    "Outpost Countdown Seconds": 15,
    "Bandit Countdown Seconds": 15,

    "Outpost Daily Limit": 5,
    "Bandit Daily Limit": 5,

    "VIP Outpost Daily Limit": 20,
    "VIP Bandit Daily Limit": 20
  },

  "Safety": {
    "Use Global Cooldown": true,
    "Global Cooldown Seconds": 0,
    "VIP Global Cooldown Seconds": 0,
    "Respect NoEscape": true
  },

  "Wipe Reset": {
    "Reset Cooldowns On Wipe": true,
    "Reset Daily Limits On Wipe": true
  },

  "Debug": false
}
```

---

## 🔐 Permissions

| Permission            | Description                      |
| --------------------- | -------------------------------- |
| `simplehomes.use`     | Use home commands                |
| `simplehomes.outpost` | Use `/outpost`                   |
| `simplehomes.bandit`  | Use `/bandit`                    |
| `simplehomes.vip`     | VIP benefits (limits, cooldowns) |

---

## 📊 Daily Limits

Supports per-feature limits:

* Homes
* Outpost
* Bandit

Players will see:

```text
⏳ Teleporting in 15s
📊 Remaining today: 3/5
```

---

## 🧠 Behavior Rules

* Teleports cancel on damage (configurable)
* Respects combat/raid block via NoEscape
* Optional building privilege checks
* Optional global cooldown system
* Fully wipe-aware (resets limits automatically)

---

## 🧹 Installation

1. Place file in:

   ```
   /oxide/plugins/
   ```

2. Rename to:

   ```
   SimpleHomes.cs
   ```

3. Load plugin:

   ```
   oxide.load SimpleHomes
   ```

---

## 🚀 Migration Guide

1. Install SimpleHomes
2. Keep:

   ```
   oxide/data/NTeleportationHome.json
   ```
3. Run:

   ```
   /shmigrate
   ```
4. Verify:

   ```
   /home list
   ```

---

## ⚠️ Notes

* Migration runs once unless forced
* Boat/moving homes are not supported (static only)
* VIP status is cached for performance (updates on reconnect)

---

## 💎 Why SimpleHomes?

Compared to large plugins:

| Feature             | SimpleHomes | NTeleportation |
| ------------------- | ----------- | -------------- |
| Lightweight         | ✔           | ❌              |
| Single purpose      | ✔           | ❌              |
| Performance focused | ✔           | ❌              |
| Easy config         | ✔           | ❌              |

---

## ❤️ Credits

* Author ***Milestorme***

---

## 🛠 Support

If you encounter issues:

* Check console logs
* Enable `"Debug": true`
* Report with exact error output

---

## 📦 Version

**v2.2.2**
Production-ready release
