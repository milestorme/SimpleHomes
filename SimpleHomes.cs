
using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Rust;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("SimpleHomes", "Milestorme", "2.2.3")]
    [Description("Lightweight home, outpost, and bandit teleports with migration and daily limit support.")]
    public class SimpleHomes : RustPlugin
    {
        [PluginReference]
        private Plugin NoEscape;

        private const string PermUse = "simplehomes.use";
        private const string PermVip = "simplehomes.vip";
        private const string PermOutpost = "simplehomes.outpost";
        private const string PermBandit = "simplehomes.bandit";
        private const string PermTownNoCooldown = "simplehomes.town.nocooldown";

        private ConfigData config;
        private StoredData storedData;

        private readonly Dictionary<ulong, PendingTeleport> pendingTeleports = new Dictionary<ulong, PendingTeleport>();
        private readonly List<Vector3> outpostSpawns = new List<Vector3>();
        private readonly List<Vector3> banditSpawns = new List<Vector3>();
        private readonly Dictionary<ulong, bool> vipCache = new Dictionary<ulong, bool>();
        private Timer saveTimer;
        private bool isDirty;
        private System.Random random;

        #region Configuration

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Chat")]
            public ChatSettings Chat = new ChatSettings();

            [JsonProperty(PropertyName = "Migration")]
            public MigrationSettings Migration = new MigrationSettings();

            [JsonProperty(PropertyName = "Home")]
            public HomeSettings Home = new HomeSettings();

            [JsonProperty(PropertyName = "Town Teleports")]
            public TownSettings Town = new TownSettings();

            [JsonProperty(PropertyName = "Safety")]
            public SafetySettings Safety = new SafetySettings();

            [JsonProperty(PropertyName = "Wipe Reset")]
            public WipeResetSettings WipeReset = new WipeResetSettings();

            [JsonProperty(PropertyName = "Debug")]
            public bool Debug = false;
        }

        private class ChatSettings
        {
            [JsonProperty(PropertyName = "Prefix")]
            public string Prefix = "<color=#00BFFF>SimpleHomes</color>: ";

            [JsonProperty(PropertyName = "Chat Steam64ID")]
            public ulong SteamId = 76561199056025689;
        }

        private class MigrationSettings
        {
            [JsonProperty(PropertyName = "Auto Migrate NTeleportation Homes")]
            public bool AutoMigrateNteleportationHomes = true;

            [JsonProperty(PropertyName = "Mark Migration Complete Even If Source Missing")]
            public bool MarkCompleteIfSourceMissing = false;
        }

        private class HomeSettings
        {
            [JsonProperty(PropertyName = "Enabled")]
            public bool Enabled = true;

            [JsonProperty(PropertyName = "Homes Limit")]
            public int Limit = 2;

            [JsonProperty(PropertyName = "VIP Homes Limit")]
            public int VipLimit = 5;

            [JsonProperty(PropertyName = "Teleport Cooldown Seconds")]
            public int Cooldown = 600;

            [JsonProperty(PropertyName = "VIP Teleport Cooldown Seconds")]
            public int VipCooldown = 300;

            [JsonProperty(PropertyName = "Teleport Countdown Seconds")]
            public int Countdown = 15;

            [JsonProperty(PropertyName = "VIP Teleport Countdown Seconds")]
            public int VipCountdown = 5;

            [JsonProperty(PropertyName = "Daily Limit")]
            public int DailyLimit = 0;

            [JsonProperty(PropertyName = "VIP Daily Limit")]
            public int VipDailyLimit = 0;

            [JsonProperty(PropertyName = "Require Building Privilege To Set Home")]
            public bool RequirePrivilegeToSet = true;

            [JsonProperty(PropertyName = "Require Building Privilege To Teleport Home")]
            public bool RequirePrivilegeToTeleport = false;

            [JsonProperty(PropertyName = "Block Set Home While Building Blocked")]
            public bool BlockSetHomeWhileBuildingBlocked = false;

            [JsonProperty(PropertyName = "Block Set Home Without Cupboard")]
            public bool BlockSetHomeWithoutCupboard = false;

            [JsonProperty(PropertyName = "Cancel Teleport On Any Damage")]
            public bool CancelOnAnyDamage = true;

            [JsonProperty(PropertyName = "Cancel Teleport On Player Damage")]
            public bool CancelOnPlayerDamage = true;

            [JsonProperty(PropertyName = "Cancel Teleport On Fall Damage")]
            public bool CancelOnFallDamage = true;

            [JsonProperty(PropertyName = "Block While Crafting")]
            public bool BlockWhileCrafting = false;

            [JsonProperty(PropertyName = "Block While Swimming")]
            public bool BlockWhileSwimming = false;

            [JsonProperty(PropertyName = "Block While Wounded")]
            public bool BlockWhileWounded = true;
        }

        private class TownSettings
        {
            [JsonProperty(PropertyName = "Enable Outpost")]
            public bool EnableOutpost = true;

            [JsonProperty(PropertyName = "Enable Bandit")]
            public bool EnableBandit = true;

            [JsonProperty(PropertyName = "Outpost Command")]
            public string OutpostCommand = "outpost";

            [JsonProperty(PropertyName = "Bandit Command")]
            public string BanditCommand = "bandit";

            [JsonProperty(PropertyName = "Cancel Command")]
            public string CancelCommand = "cancelteleport";

            [JsonProperty(PropertyName = "Outpost Cooldown Seconds")]
            public int OutpostCooldown = 0;

            [JsonProperty(PropertyName = "Outpost Countdown Seconds")]
            public int OutpostCountdown = 15;

            [JsonProperty(PropertyName = "Outpost Daily Limit")]
            public int OutpostDailyLimit = 0;

            [JsonProperty(PropertyName = "Bandit Cooldown Seconds")]
            public int BanditCooldown = 0;

            [JsonProperty(PropertyName = "Bandit Countdown Seconds")]
            public int BanditCountdown = 15;

            [JsonProperty(PropertyName = "Bandit Daily Limit")]
            public int BanditDailyLimit = 0;

            [JsonProperty(PropertyName = "VIP Cooldown Seconds")]
            public int VipCooldown = 0;

            [JsonProperty(PropertyName = "VIP Countdown Seconds")]
            public int VipCountdown = 5;

            [JsonProperty(PropertyName = "VIP Outpost Daily Limit")]
            public int VipOutpostDailyLimit = 0;

            [JsonProperty(PropertyName = "VIP Bandit Daily Limit")]
            public int VipBanditDailyLimit = 0;

            [JsonProperty(PropertyName = "Block Teleport When Mounted")]
            public bool BlockWhenMounted = false;

            [JsonProperty(PropertyName = "Block Teleport From Cargo")]
            public bool BlockFromCargo = false;

            [JsonProperty(PropertyName = "Force Reset Hostile Timer On Teleport")]
            public bool ResetHostileOnSuccess = true;
        }

        private class SafetySettings
        {
            [JsonProperty(PropertyName = "Use Global Cooldown")]
            public bool UseGlobalCooldown = true;

            [JsonProperty(PropertyName = "Global Cooldown Seconds")]
            public int GlobalCooldown = 0;

            [JsonProperty(PropertyName = "VIP Global Cooldown Seconds")]
            public int VipGlobalCooldown = 0;

            [JsonProperty(PropertyName = "Respect NoEscape")]
            public bool RespectNoEscape = true;
        }

        private class WipeResetSettings
        {
            [JsonProperty(PropertyName = "Reset Cooldowns On Wipe")]
            public bool ResetCooldownsOnWipe = true;

            [JsonProperty(PropertyName = "Reset Daily Limits On Wipe")]
            public bool ResetDailyLimitsOnWipe = true;

            [JsonProperty(PropertyName = "Reset Homes On Wipe")]
            public bool ResetHomesOnWipe = true;
        }

        protected override void LoadDefaultConfig()
        {
            config = new ConfigData();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<ConfigData>();
                if (config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                PrintWarning("Config error, generating new config.");
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config, true);
        }

        #endregion

        #region Data

        private class StoredData
        {
            [JsonProperty(PropertyName = "Players")]
            public Dictionary<ulong, PlayerData> Players = new Dictionary<ulong, PlayerData>();

            [JsonProperty(PropertyName = "Migration Completed")]
            public bool MigrationCompleted = false;

            [JsonProperty(PropertyName = "Last Map Seed")]
            public int LastMapSeed = 0;

            [JsonProperty(PropertyName = "Last Map Size")]
            public int LastMapSize = 0;

            [JsonProperty(PropertyName = "Last Save Created Time")]
            public string LastSaveCreatedTime = string.Empty;
        }

        private class PlayerData
        {
            [JsonProperty(PropertyName = "Homes")]
            public Dictionary<string, SerializableVector3> Homes = new Dictionary<string, SerializableVector3>(StringComparer.OrdinalIgnoreCase);

            [JsonProperty(PropertyName = "Home Cooldown Until")]
            public int HomeCooldownUntil = 0;

            [JsonProperty(PropertyName = "Town Cooldown Until")]
            public int TownCooldownUntil = 0;

            [JsonProperty(PropertyName = "Global Cooldown Until")]
            public int GlobalCooldownUntil = 0;

            [JsonProperty(PropertyName = "Home Daily Date")]
            public string HomeDailyDate = string.Empty;

            [JsonProperty(PropertyName = "Home Daily Count")]
            public int HomeDailyCount = 0;

            [JsonProperty(PropertyName = "Town Daily Date")]
            public string TownDailyDate = string.Empty;

            [JsonProperty(PropertyName = "Outpost Daily Count")]
            public int OutpostDailyCount = 0;

            [JsonProperty(PropertyName = "Bandit Daily Count")]
            public int BanditDailyCount = 0;
        }

        private class SerializableVector3
        {
            [JsonProperty(PropertyName = "x")]
            public float x;

            [JsonProperty(PropertyName = "y")]
            public float y;

            [JsonProperty(PropertyName = "z")]
            public float z;

            public SerializableVector3()
            {
            }

            public SerializableVector3(Vector3 v)
            {
                x = v.x;
                y = v.y;
                z = v.z;
            }

            public Vector3 ToVector3()
            {
                return new Vector3(x, y, z);
            }
        }

        private class PendingTeleport
        {
            public Timer Timer;
            public string Type;
            public string HomeName;
            public Vector3 Destination;
        }

        private class NTeleportationHomeRecord
        {
            [JsonProperty(PropertyName = "l")]
            public Dictionary<string, object> Buildings = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            [JsonProperty(PropertyName = "b")]
            public Dictionary<string, object> Boats = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            [JsonProperty(PropertyName = "t")]
            public object Teleports;
        }

        private class NTeleportationBoatHome
        {
            [JsonProperty(PropertyName = "Value")]
            public ulong Value;

            [JsonProperty(PropertyName = "Offset")]
            public SerializableVector3 Offset = new SerializableVector3();
        }

        private void LoadData()
        {
            try
            {
                storedData = Interface.GetMod().DataFileSystem.ReadObject<StoredData>(Name);
            }
            catch
            {
                storedData = new StoredData();
            }

            if (storedData == null)
            {
                storedData = new StoredData();
            }
        }

        private void SaveData()
        {
            Interface.GetMod().DataFileSystem.WriteObject(Name, storedData);
            isDirty = false;
            if (saveTimer != null && !saveTimer.Destroyed)
            {
                saveTimer.Destroy();
                saveTimer = null;
            }
        }

        private void MarkDirty()
        {
            isDirty = true;

            if (saveTimer != null && !saveTimer.Destroyed)
            {
                return;
            }

            saveTimer = timer.Once(10f, delegate()
            {
                saveTimer = null;
                if (isDirty)
                {
                    SaveData();
                }
            });
        }

        #endregion

        #region Oxide Hooks

        private void Init()
        {
            random = new System.Random();
            vipCache.Clear();

            permission.RegisterPermission(PermUse, this);
            permission.RegisterPermission(PermVip, this);
            permission.RegisterPermission(PermOutpost, this);
            permission.RegisterPermission(PermBandit, this);
            permission.RegisterPermission(PermTownNoCooldown, this);

            LoadData();
            RegisterCommands();
        }

        private void OnServerInitialized()
        {
            FindTownSpawns();
            HandleWipeReset();
            TryMigrateNteleportationHomes();
        }

        private void Unload()
        {
            foreach (KeyValuePair<ulong, PendingTeleport> pair in pendingTeleports)
            {
                if (pair.Value != null && pair.Value.Timer != null && !pair.Value.Timer.Destroyed)
                {
                    pair.Value.Timer.Destroy();
                }
            }
            pendingTeleports.Clear();

            if (saveTimer != null && !saveTimer.Destroyed)
            {
                saveTimer.Destroy();
                saveTimer = null;
            }

            if (isDirty)
            {
                SaveData();
            }
        }

        private void OnServerSave()
        {
            if (isDirty)
            {
                SaveData();
            }
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player != null)
            {
                vipCache[player.userID] = IsVip(player);
            }
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            CancelTeleport(player, null);
            if (player != null)
            {
                vipCache.Remove(player.userID);
            }
        }

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            BasePlayer player = entity as BasePlayer;
            if (player == null || info == null)
            {
                return;
            }

            PendingTeleport pending;
            if (!pendingTeleports.TryGetValue(player.userID, out pending))
            {
                return;
            }

            NextTick(delegate()
            {
                if (info == null || info.damageTypes == null || info.damageTypes.Total() <= 0f)
                {
                    return;
                }

                if (pending.Type == "home")
                {
                    if (config.Home.CancelOnAnyDamage)
                    {
                        CancelTeleport(player, Lang("CancelDamage", player.UserIDString));
                        return;
                    }
                    if (config.Home.CancelOnPlayerDamage && info.Initiator is BasePlayer)
                    {
                        CancelTeleport(player, Lang("CancelPlayerDamage", player.UserIDString));
                        return;
                    }
                    if (config.Home.CancelOnFallDamage && info.damageTypes.Has(DamageType.Fall))
                    {
                        CancelTeleport(player, Lang("CancelFallDamage", player.UserIDString));
                        return;
                    }
                }
                else
                {
                    if (config.Home.CancelOnAnyDamage)
                    {
                        CancelTeleport(player, Lang("CancelDamage", player.UserIDString));
                        return;
                    }
                    if (config.Home.CancelOnPlayerDamage && info.Initiator is BasePlayer)
                    {
                        CancelTeleport(player, Lang("CancelPlayerDamage", player.UserIDString));
                        return;
                    }
                    if (config.Home.CancelOnFallDamage && info.damageTypes.Has(DamageType.Fall))
                    {
                        CancelTeleport(player, Lang("CancelFallDamage", player.UserIDString));
                        return;
                    }
                }
            });
        }

        #endregion

        #region Commands

        private void RegisterCommands()
        {
            cmd.AddChatCommand("home", this, "CmdHome");
            cmd.AddChatCommand(config.Town.OutpostCommand, this, "CmdOutpost");
            cmd.AddChatCommand(config.Town.BanditCommand, this, "CmdBandit");
            cmd.AddChatCommand(config.Town.CancelCommand, this, "CmdCancelTeleport");
            cmd.AddChatCommand("shmigrate", this, "CmdShMigrate");
        }

        private void CmdHome(BasePlayer player, string command, string[] args)
        {
            if (player == null)
            {
                return;
            }

            if (!config.Home.Enabled)
            {
                ReplyKey(player, "HomeDisabled");
                return;
            }

            if (!permission.UserHasPermission(player.UserIDString, PermUse))
            {
                ReplyKey(player, "NoPermHome");
                return;
            }

            if (args == null || args.Length == 0)
            {
                ReplyKey(player, "HomeUsage");
                return;
            }

            string sub = args[0].ToLowerInvariant();

            if (sub == "add")
            {
                if (args.Length != 2)
                {
                    ReplyKey(player, "HomeUsageAdd");
                    return;
                }
                TryAddHome(player, args[1]);
                return;
            }

            if (sub == "list")
            {
                if (args.Length != 1)
                {
                    ReplyKey(player, "HomeUsageList");
                    return;
                }
                ListHomes(player);
                return;
            }

            if (sub == "remove")
            {
                if (args.Length != 2)
                {
                    ReplyKey(player, "HomeUsageRemove");
                    return;
                }
                RemoveHome(player, args[1]);
                return;
            }

            if (args.Length == 1)
            {
                TeleportHome(player, args[0]);
                return;
            }

            ReplyKey(player, "HomeUsage");
        }

        private void CmdOutpost(BasePlayer player, string command, string[] args)
        {
            if (player == null)
            {
                return;
            }

            if (!config.Town.EnableOutpost)
            {
                ReplyKey(player, "OutpostDisabled");
                return;
            }

            if (!permission.UserHasPermission(player.UserIDString, PermOutpost))
            {
                ReplyKey(player, "NoPermOutpost");
                return;
            }

            if (outpostSpawns.Count == 0)
            {
                ReplyKey(player, "OutpostNotFound");
                return;
            }

            string error = CanUseTownTeleport(player, "outpost");
            if (!string.IsNullOrEmpty(error))
            {
                Reply(player, error);
                return;
            }

            int countdown = GetTownCountdown(player, GetTownBaseCountdown("outpost"));
            Vector3 destination = outpostSpawns[random.Next(outpostSpawns.Count)];
            BeginTeleport(player, "town", "outpost", destination, countdown, "Teleporting to Outpost in {0} seconds. Type /" + config.Town.CancelCommand + " to cancel.");
        }

        private void CmdBandit(BasePlayer player, string command, string[] args)
        {
            if (player == null)
            {
                return;
            }

            if (!config.Town.EnableBandit)
            {
                ReplyKey(player, "BanditDisabled");
                return;
            }

            if (!permission.UserHasPermission(player.UserIDString, PermBandit))
            {
                ReplyKey(player, "NoPermBandit");
                return;
            }

            if (banditSpawns.Count == 0)
            {
                ReplyKey(player, "BanditNotFound");
                return;
            }

            string error = CanUseTownTeleport(player, "bandit");
            if (!string.IsNullOrEmpty(error))
            {
                Reply(player, error);
                return;
            }

            int countdown = GetTownCountdown(player, GetTownBaseCountdown("bandit"));
            Vector3 destination = banditSpawns[random.Next(banditSpawns.Count)];
            BeginTeleport(player, "town", "bandit", destination, countdown, "Teleporting to Bandit in {0} seconds. Type /" + config.Town.CancelCommand + " to cancel.");
        }

        private void CmdCancelTeleport(BasePlayer player, string command, string[] args)
        {
            if (player == null)
            {
                return;
            }

            PendingTeleport pending;
            if (!pendingTeleports.TryGetValue(player.userID, out pending))
            {
                ReplyKey(player, "NoActiveTeleport");
                return;
            }

            CancelTeleport(player, Lang("CancelGeneric", player.UserIDString));
        }

        #endregion

        #region Home Logic

        private void TryAddHome(BasePlayer player, string name)
        {
            if (!IsValidHomeName(name))
            {
                ReplyKey(player, "InvalidHomeName");
                return;
            }

            PlayerData pdata = GetPlayerData(player.userID);

            if (pdata.Homes.ContainsKey(name))
            {
                ReplyKey(player, "HomeExists");
                return;
            }

            if (pdata.Homes.Count >= GetHomeLimit(player))
            {
                ReplyKey(player, "HomeLimitReached", GetHomeLimit(player));
                return;
            }

            string setError = CanSetHome(player);
            if (!string.IsNullOrEmpty(setError))
            {
                Reply(player, setError);
                return;
            }

            pdata.Homes[name] = new SerializableVector3(player.transform.position);
            MarkDirty();
            ReplyKey(player, "HomeSaved", name);
        }

        private void ListHomes(BasePlayer player)
        {
            PlayerData pdata = GetPlayerData(player.userID);
            ResetHomeDailyIfNeeded(pdata);

            if (pdata.Homes.Count == 0)
            {
                ReplyKey(player, "NoHomesSaved", FormatDailyRemaining(player.UserIDString, Math.Max(0, GetHomeDailyLimit(player) - pdata.HomeDailyCount), GetHomeDailyLimit(player)));
                return;
            }

            ReplyKey(player, "YourHomes");
            foreach (KeyValuePair<string, SerializableVector3> entry in pdata.Homes)
            {
                Vector3 pos = entry.Value.ToVector3();
                ReplyKey(player, "HomeListEntry", entry.Key, pos.x, pos.y, pos.z);
            }

            Reply(player, FormatDailyRemaining(player.UserIDString, Math.Max(0, GetHomeDailyLimit(player) - pdata.HomeDailyCount), GetHomeDailyLimit(player)));
        }

        private void RemoveHome(BasePlayer player, string name)
        {
            PlayerData pdata = GetPlayerData(player.userID);
            if (!pdata.Homes.Remove(name))
            {
                ReplyKey(player, "HomeNotFound");
                return;
            }

            MarkDirty();
            ReplyKey(player, "HomeRemoved", name);
        }

        private void TeleportHome(BasePlayer player, string name)
        {
            PlayerData pdata = GetPlayerData(player.userID);
            SerializableVector3 home;
            if (!pdata.Homes.TryGetValue(name, out home))
            {
                ReplyKey(player, "HomeNotFound");
                return;
            }

            string error = CanUseHomeTeleport(player);
            if (!string.IsNullOrEmpty(error))
            {
                Reply(player, error);
                return;
            }

            int countdown = GetHomeCountdown(player);
            BeginTeleport(player, "home", name, home.ToVector3(), countdown, "Teleporting to home '{0}' in {1} seconds.");
        }

        private string CanSetHome(BasePlayer player)
        {
            if (config.Home.BlockWhileWounded && player.IsWounded())
            {
                return Lang("SetHomeWounded", player.UserIDString);
            }

            if (config.Home.BlockWhileSwimming && player.IsSwimming())
            {
                return Lang("SetHomeSwimming", player.UserIDString);
            }

            if (config.Home.BlockWhileCrafting && IsCrafting(player))
            {
                return Lang("SetHomeCrafting", player.UserIDString);
            }

            BuildingPrivlidge priv = player.GetBuildingPrivilege();
            if (config.Home.BlockSetHomeWithoutCupboard && priv == null)
            {
                return Lang("SetHomeNoCupboard", player.UserIDString);
            }

            if (config.Home.BlockSetHomeWhileBuildingBlocked && !player.CanBuild())
            {
                return Lang("SetHomeBlocked", player.UserIDString);
            }

            if (config.Home.RequirePrivilegeToSet)
            {
                if (priv == null || !priv.IsAuthed(player))
                {
                    return Lang("SetHomeNotAuthed", player.UserIDString);
                }
            }

            return null;
        }

        private string CanUseHomeTeleport(BasePlayer player, bool ignorePending = false)
        {
            if (player.IsDead())
            {
                return Lang("TpDead", player.UserIDString);
            }

            if (config.Home.BlockWhileWounded && player.IsWounded())
            {
                return Lang("TpWounded", player.UserIDString);
            }

            if (config.Home.BlockWhileSwimming && player.IsSwimming())
            {
                return Lang("TpSwimming", player.UserIDString);
            }

            if (config.Home.BlockWhileCrafting && IsCrafting(player))
            {
                return Lang("TpCrafting", player.UserIDString);
            }

            if (config.Home.RequirePrivilegeToTeleport)
            {
                BuildingPrivlidge priv = player.GetBuildingPrivilege();
                if (priv == null || !priv.IsAuthed(player))
                {
                    return Lang("TpHomeNotAuthed", player.UserIDString);
                }
            }

            if (config.Safety.RespectNoEscape && NoEscape != null)
            {
                object raidBlocked = NoEscape.Call("IsRaidBlocked", player);
                if (raidBlocked is bool && (bool)raidBlocked)
                {
                    return Lang("TpRaidBlocked", player.UserIDString);
                }

                object combatBlocked = NoEscape.Call("IsCombatBlocked", player);
                if (combatBlocked is bool && (bool)combatBlocked)
                {
                    return Lang("TpCombatBlocked", player.UserIDString);
                }
            }

            if (!ignorePending && pendingTeleports.ContainsKey(player.userID))
            {
                return Lang("TpAlreadyPending", player.UserIDString);
            }

            PlayerData pdata = GetPlayerData(player.userID);
            int now = GetUnix();

            int globalRemaining = GetRemainingGlobalCooldown(player, pdata, now);
            if (globalRemaining > 0)
            {
                return string.Format("Global teleport cooldown: {0}.", FormatTime(globalRemaining));
            }

            int homeRemaining = pdata.HomeCooldownUntil - now;
            if (homeRemaining > 0)
            {
                return string.Format("Home teleport cooldown: {0}.", FormatTime(homeRemaining));
            }

            int dailyLimit = GetHomeDailyLimit(player);
            if (dailyLimit > 0)
            {
                ResetHomeDailyIfNeeded(pdata);

                if (pdata.HomeDailyCount >= dailyLimit)
                {
                    return string.Format("You have reached your daily home limit of {0}.", dailyLimit);
                }
            }

            return null;
        }

        #endregion

        #region Town Logic

        private string CanUseTownTeleport(BasePlayer player, string town, bool ignorePending = false)
        {
            if (player.IsDead())
            {
                return Lang("TpDead", player.UserIDString);
            }

            if (player.IsWounded())
            {
                return Lang("TpWounded", player.UserIDString);
            }

            if (!player.CanBuild())
            {
                return Lang("TpNoBuild", player.UserIDString);
            }

            if (config.Town.BlockWhenMounted && player.isMounted)
            {
                return Lang("TpMounted", player.UserIDString);
            }

            if (config.Town.BlockFromCargo && player.GetComponentInParent<CargoShip>() != null)
            {
                return Lang("TpCargo", player.UserIDString);
            }

            if (player.IsHostile())
            {
                return Lang("TpHostile", player.UserIDString);
            }

            if (config.Safety.RespectNoEscape && NoEscape != null)
            {
                object raidBlocked = NoEscape.Call("IsRaidBlocked", player);
                if (raidBlocked is bool && (bool)raidBlocked)
                {
                    return Lang("TpRaidBlocked", player.UserIDString);
                }

                object combatBlocked = NoEscape.Call("IsCombatBlocked", player);
                if (combatBlocked is bool && (bool)combatBlocked)
                {
                    return Lang("TpCombatBlocked", player.UserIDString);
                }
            }

            if (!ignorePending && pendingTeleports.ContainsKey(player.userID))
            {
                return Lang("TpAlreadyPending", player.UserIDString);
            }

            PlayerData pdata = GetPlayerData(player.userID);
            int now = GetUnix();

            int globalRemaining = GetRemainingGlobalCooldown(player, pdata, now);
            if (globalRemaining > 0)
            {
                return string.Format("Global teleport cooldown: {0}.", FormatTime(globalRemaining));
            }

            if (!permission.UserHasPermission(player.UserIDString, PermTownNoCooldown))
            {
                int townRemaining = pdata.TownCooldownUntil - now;
                if (townRemaining > 0)
                {
                    return string.Format("{0} teleport cooldown: {1}.", town, FormatTime(townRemaining));
                }
            }

            int dailyLimit = GetTownDailyLimit(player, town);
            if (dailyLimit > 0)
            {
                ResetTownDailyIfNeeded(pdata);

                if (GetTownDailyCount(pdata, town) >= dailyLimit)
                {
                    return Lang("TownDailyLimitReached", player.UserIDString, town, dailyLimit);
                }
            }

            return null;
        }

        private void FindTownSpawns()
        {
            outpostSpawns.Clear();
            banditSpawns.Clear();

            foreach (MonumentInfo monument in TerrainMeta.Path.Monuments)
            {
                if (monument == null || string.IsNullOrEmpty(monument.name))
                {
                    continue;
                }

                string lower = monument.name.ToLowerInvariant();

                if (config.Town.EnableOutpost && lower.Contains("compound"))
                {
                    List<BaseEntity> list = new List<BaseEntity>();
                    Vis.Entities(monument.transform.position, 25f, list);
                    foreach (BaseEntity entity in list)
                    {
                        if (entity != null && entity.name != null && entity.name.Contains("chair"))
                        {
                            Vector3 pos = entity.transform.position;
                            pos.y += 1f;
                            if (!outpostSpawns.Contains(pos))
                            {
                                outpostSpawns.Add(pos);
                            }
                        }
                    }
                }
                else if (config.Town.EnableBandit && lower.Contains("bandit"))
                {
                    Vector3 t = monument.transform.position + -monument.transform.right * -50.75f;
                    Vector3 pos = t + (monument.transform.forward * -21.75f) + (Vector3.up * 7f);
                    if (!banditSpawns.Contains(pos))
                    {
                        banditSpawns.Add(pos);
                    }
                }
            }

            DebugLog(string.Format("Found {0} outpost spawn(s) and {1} bandit spawn(s).", outpostSpawns.Count, banditSpawns.Count));
        }

        #endregion

        #region Teleport Logic

        private void BeginTeleport(BasePlayer player, string type, string name, Vector3 destination, int countdown, string startMessage)
        {
            PendingTeleport pending = new PendingTeleport();
            pending.Type = type;
            pending.HomeName = name;
            pending.Destination = destination;

            pending.Timer = timer.Once(countdown, delegate()
            {
                PendingTeleport current;
                if (!pendingTeleports.TryGetValue(player.userID, out current))
                {
                    return;
                }

                string error = type == "home" ? CanUseHomeTeleport(player, true) : CanUseTownTeleport(player, name, true);
                if (!string.IsNullOrEmpty(error))
                {
                    pendingTeleports.Remove(player.userID);
                    Reply(player, error);
                    return;
                }

                TeleportPlayer(player, destination);

                int now = GetUnix();
                PlayerData pdata = GetPlayerData(player.userID);
                string successMessage;

                if (type == "home")
                {
                    pdata.HomeCooldownUntil = now + GetHomeCooldown(player);

                    int dailyLimit = GetHomeDailyLimit(player);
                    if (dailyLimit > 0)
                    {
                        ResetHomeDailyIfNeeded(pdata);
                        pdata.HomeDailyCount++;
                    }

                    if (config.Safety.UseGlobalCooldown)
                    {
                        pdata.GlobalCooldownUntil = now + GetGlobalCooldown(player);
                    }

                    successMessage = Lang("HomeTeleportSuccess", player.UserIDString, name);
                    if (dailyLimit > 0)
                    {
                        successMessage += " " + FormatDailyRemaining(player.UserIDString, Math.Max(0, dailyLimit - pdata.HomeDailyCount), dailyLimit);
                    }
                }
                else
                {
                    if (!permission.UserHasPermission(player.UserIDString, PermTownNoCooldown))
                    {
                        pdata.TownCooldownUntil = now + GetTownCooldown(player, GetTownBaseCooldown(name));
                    }

                    int townDailyLimit = GetTownDailyLimit(player, name);
                    if (townDailyLimit > 0)
                    {
                        ResetTownDailyIfNeeded(pdata);
                        IncrementTownDailyCount(pdata, name);
                    }

                    if (config.Safety.UseGlobalCooldown)
                    {
                        pdata.GlobalCooldownUntil = now + GetGlobalCooldown(player);
                    }

                    if (config.Town.ResetHostileOnSuccess)
                    {
                        ResetHostile(player);
                    }

                    successMessage = Lang("TownTeleportSuccess", player.UserIDString, GetTownDisplayName(name));
                    if (townDailyLimit > 0)
                    {
                        successMessage += " " + FormatDailyRemaining(player.UserIDString, Math.Max(0, townDailyLimit - GetTownDailyCount(pdata, name)), townDailyLimit);
                    }
                }

                if (pending.Timer != null && !pending.Timer.Destroyed)
                {
                    pending.Timer.Destroy();
                }

                pendingTeleports.Remove(player.userID);
                MarkDirty();
                Reply(player, successMessage);
            });

            pendingTeleports[player.userID] = pending;

            PlayerData currentData = GetPlayerData(player.userID);
            string message;
            if (type == "home")
            {
                int homeDailyLimit = GetHomeDailyLimit(player);
                message = string.Format(startMessage, name, countdown);
                if (homeDailyLimit > 0)
                {
                    ResetHomeDailyIfNeeded(currentData);
                    message += " " + FormatDailyRemaining(player.UserIDString, Math.Max(0, homeDailyLimit - (currentData.HomeDailyCount + 1)), homeDailyLimit);
                }
            }
            else
            {
                int townDailyLimit = GetTownDailyLimit(player, name);
                message = string.Format(startMessage, countdown);
                if (townDailyLimit > 0)
                {
                    ResetTownDailyIfNeeded(currentData);
                    message += " " + FormatDailyRemaining(player.UserIDString, Math.Max(0, townDailyLimit - (GetTownDailyCount(currentData, name) + 1)), townDailyLimit);
                }
            }

            Reply(player, message);
        }

        private void CancelTeleport(BasePlayer player, string message)
        {
            if (player == null)
            {
                return;
            }

            PendingTeleport pending;
            if (!pendingTeleports.TryGetValue(player.userID, out pending))
            {
                return;
            }

            if (pending.Timer != null && !pending.Timer.Destroyed)
            {
                pending.Timer.Destroy();
            }

            pendingTeleports.Remove(player.userID);

            if (!string.IsNullOrEmpty(message))
            {
                Reply(player, message);
            }
        }

        private void TeleportPlayer(BasePlayer player, Vector3 position)
        {
            if (player == null || !player.IsConnected)
            {
                return;
            }

            player.EnsureDismounted();

            if (player.net != null && player.net.connection != null)
            {
                player.ClientRPCPlayer(null, player, "StartLoading");
            }

            StartSleeping(player);
            player.MovePosition(position);

            if (player.net != null && player.net.connection != null)
            {
                player.ClientRPCPlayer(null, player, "ForcePositionTo", position);
                player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);
            }

            player.UpdateNetworkGroup();
            player.SendNetworkUpdateImmediate();

            if (player.net == null || player.net.connection == null)
            {
                return;
            }

            try
            {
                player.ClearEntityQueue();
            }
            catch
            {
            }

            player.SendFullSnapshot();
            player.SetParent(null, true, true);
        }

        private void StartSleeping(BasePlayer player)
        {
            if (player.IsSleeping())
            {
                return;
            }

            player.SetPlayerFlag(BasePlayer.PlayerFlags.Sleeping, true);

            if (!BasePlayer.sleepingPlayerList.Contains(player))
            {
                BasePlayer.sleepingPlayerList.Add(player);
            }

            player.CancelInvoke("InventoryUpdate");
        }

        private void ResetHostile(BasePlayer player)
        {
            player.State.unHostileTimestamp = 0;
            player.ClientRPCPlayer(null, player, "SetHostileLength", 0f);
        }

        #endregion

        private bool TryParseImportedVector(object raw, out SerializableVector3 vector)
        {
            vector = null;
            if (raw == null)
            {
                return false;
            }

            string rawString = raw as string;
            if (!string.IsNullOrEmpty(rawString))
            {
                return TryParseImportedVectorString(rawString, out vector);
            }

            JToken token = raw as JToken;
            if (token != null)
            {
                if (token.Type == JTokenType.String)
                {
                    return TryParseImportedVectorString(token.ToObject<string>(), out vector);
                }

                if (token.Type == JTokenType.Object)
                {
                    JToken xToken = token["x"] ?? token["X"];
                    JToken yToken = token["y"] ?? token["Y"];
                    JToken zToken = token["z"] ?? token["Z"];
                    if (xToken != null && yToken != null && zToken != null)
                    {
                        float x;
                        float y;
                        float z;
                        if (float.TryParse(xToken.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out x) &&
                            float.TryParse(yToken.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out y) &&
                            float.TryParse(zToken.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out z))
                        {
                            vector = new SerializableVector3 { x = x, y = y, z = z };
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private bool TryParseImportedVectorString(string value, out SerializableVector3 vector)
        {
            vector = null;
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            string[] parts = value.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 3)
            {
                return false;
            }

            float x;
            float y;
            float z;
            if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x))
            {
                return false;
            }

            if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y))
            {
                return false;
            }

            if (!float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out z))
            {
                return false;
            }

            vector = new SerializableVector3 { x = x, y = y, z = z };
            return true;
        }

        #region Migration and Wipe Reset

        private void TryMigrateNteleportationHomes(bool force = false)
        {
            if (!config.Migration.AutoMigrateNteleportationHomes && !force)
            {
                return;
            }

            if (storedData.MigrationCompleted && !force)
            {
                return;
            }

            string[] candidates = new string[]
            {
                "NTeleportationHome",
                "NTeleportation/Home",
                "Home"
            };

            bool importedAnything = false;
            bool sourceFound = false;
            int importedCount = 0;

            foreach (string fileName in candidates)
            {
                try
                {
                    Dictionary<ulong, NTeleportationHomeRecord> source = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<ulong, NTeleportationHomeRecord>>(fileName);
                    if (source == null || source.Count == 0)
                    {
                        continue;
                    }

                    sourceFound = true;

                    foreach (KeyValuePair<ulong, NTeleportationHomeRecord> pair in source)
                    {
                        if (pair.Value == null || pair.Value.Buildings == null || pair.Value.Buildings.Count == 0)
                        {
                            continue;
                        }

                        PlayerData pdata = GetPlayerData(pair.Key);
                        foreach (KeyValuePair<string, object> home in pair.Value.Buildings)
                        {
                            SerializableVector3 importedHome;
                            if (pdata.Homes.ContainsKey(home.Key))
                            {
                                continue;
                            }

                            if (!TryParseImportedVector(home.Value, out importedHome))
                            {
                                DebugLog(string.Format("Failed to parse imported home '{0}' for player {1}. Raw value: {2}", home.Key, pair.Key, home.Value == null ? "null" : home.Value.ToString()));
                                continue;
                            }

                            pdata.Homes[home.Key] = importedHome;
                            importedAnything = true;
                            importedCount++;
                        }
                    }

                    if (importedAnything)
                    {
                        ReplyConsole(string.Format("Imported {0} NTeleportation home(s) from data file '{1}'.", importedCount, fileName));
                    }
                    else
                    {
                        ReplyConsole(string.Format("Found NTeleportation data file '{0}', but imported 0 homes.", fileName));
                    }

                    break;
                }
                catch (Exception ex)
                {
                    DebugLog(string.Format("Migration read failed for '{0}': {1}", fileName, ex.Message));
                }
            }

            if (importedAnything)
            {
                storedData.MigrationCompleted = true;
                MarkDirty();
            }
            else if (!sourceFound && config.Migration.MarkCompleteIfSourceMissing)
            {
                storedData.MigrationCompleted = true;
                MarkDirty();
            }
        }

        
        private void CmdShMigrate(BasePlayer player, string command, string[] args)
        {
            if (player != null && !player.IsAdmin)
            {
                ReplyKey(player, "NoPermMigration");
                return;
            }

            storedData.MigrationCompleted = false;
            TryMigrateNteleportationHomes(true);
            MarkDirty();

            if (player != null)
            {
                ReplyKey(player, "MigrationAttempted");
            }
            else
            {
                ReplyConsole(Lang("MigrationAttemptedConsole"));
            }
        }

private void HandleWipeReset()
        {
            int currentSeed = (int)World.Seed;
            int currentSize = (int)World.Size;
            string currentSaveTime = SaveRestore.SaveCreatedTime.ToString("O");

            bool changed =
                storedData.LastMapSeed != currentSeed ||
                storedData.LastMapSize != currentSize ||
                storedData.LastSaveCreatedTime != currentSaveTime;

            if (changed)
            {
                foreach (KeyValuePair<ulong, PlayerData> pair in storedData.Players)
                {
                    PlayerData pdata = pair.Value;
                    if (pdata == null)
                    {
                        continue;
                    }

                    if (config.WipeReset.ResetHomesOnWipe)
                    {
                        pdata.Homes.Clear();
                    }

                    if (config.WipeReset.ResetCooldownsOnWipe)
                    {
                        pdata.HomeCooldownUntil = 0;
                        pdata.TownCooldownUntil = 0;
                        pdata.GlobalCooldownUntil = 0;
                    }

                    if (config.WipeReset.ResetDailyLimitsOnWipe)
                    {
                        pdata.HomeDailyDate = string.Empty;
                        pdata.HomeDailyCount = 0;
                        pdata.TownDailyDate = string.Empty;
                        pdata.OutpostDailyCount = 0;
                        pdata.BanditDailyCount = 0;
                    }
                }

                storedData.LastMapSeed = currentSeed;
                storedData.LastMapSize = currentSize;
                storedData.LastSaveCreatedTime = currentSaveTime;
                MarkDirty();

                DebugLog("Wipe change detected, reset configured counters.");
            }
        }

        #endregion

        #region Helpers

        private PlayerData GetPlayerData(ulong userId)
        {
            PlayerData pdata;
            if (!storedData.Players.TryGetValue(userId, out pdata) || pdata == null)
            {
                pdata = new PlayerData();
                storedData.Players[userId] = pdata;
            }
            return pdata;
        }

        private bool IsVip(BasePlayer player)
        {
            if (player == null)
            {
                return false;
            }

            bool isVip;
            if (!vipCache.TryGetValue(player.userID, out isVip))
            {
                isVip = permission.UserHasPermission(player.UserIDString, PermVip);
                vipCache[player.userID] = isVip;
            }

            return isVip;
        }

        private int GetHomeLimit(BasePlayer player)
        {
            return IsVip(player) ? config.Home.VipLimit : config.Home.Limit;
        }

        private int GetHomeCooldown(BasePlayer player)
        {
            return IsVip(player) ? config.Home.VipCooldown : config.Home.Cooldown;
        }

        private int GetHomeCountdown(BasePlayer player)
        {
            return IsVip(player) ? config.Home.VipCountdown : config.Home.Countdown;
        }

        private int GetHomeDailyLimit(BasePlayer player)
        {
            return IsVip(player) ? config.Home.VipDailyLimit : config.Home.DailyLimit;
        }

        private int GetTownDailyLimit(BasePlayer player, string town)
        {
            bool isVip = IsVip(player);
            if (town == "outpost")
            {
                return isVip ? config.Town.VipOutpostDailyLimit : config.Town.OutpostDailyLimit;
            }

            return isVip ? config.Town.VipBanditDailyLimit : config.Town.BanditDailyLimit;
        }

        private void ResetHomeDailyIfNeeded(PlayerData pdata)
        {
            string today = GetDateKey();
            if (pdata.HomeDailyDate != today)
            {
                pdata.HomeDailyDate = today;
                pdata.HomeDailyCount = 0;
            }
        }

        private void ResetTownDailyIfNeeded(PlayerData pdata)
        {
            string today = GetDateKey();
            if (pdata.TownDailyDate != today)
            {
                pdata.TownDailyDate = today;
                pdata.OutpostDailyCount = 0;
                pdata.BanditDailyCount = 0;
            }
        }

        private int GetTownDailyCount(PlayerData pdata, string town)
        {
            return town == "outpost" ? pdata.OutpostDailyCount : pdata.BanditDailyCount;
        }

        private void IncrementTownDailyCount(PlayerData pdata, string town)
        {
            if (town == "outpost")
            {
                pdata.OutpostDailyCount++;
            }
            else
            {
                pdata.BanditDailyCount++;
            }
        }

        private int GetTownCountdown(BasePlayer player, int normalCountdown)
        {
            return IsVip(player) ? config.Town.VipCountdown : normalCountdown;
        }

        private int GetTownCooldown(BasePlayer player, int normalCooldown)
        {
            return IsVip(player) ? config.Town.VipCooldown : normalCooldown;
        }

        private int GetTownBaseCooldown(string town)
        {
            return town == "outpost" ? config.Town.OutpostCooldown : config.Town.BanditCooldown;
        }

        private int GetTownBaseCountdown(string town)
        {
            return town == "outpost" ? config.Town.OutpostCountdown : config.Town.BanditCountdown;
        }

        private string GetTownDisplayName(string town)
        {
            return town == "outpost" ? "Outpost" : "Bandit";
        }

        private string FormatDailyRemaining(string userId, int remaining, int limit)
        {
            if (limit <= 0)
            {
                return Lang("DailyLimitUnlimited", userId);
            }

            return Lang("DailyRemaining", userId, remaining, limit);
        }

        private int GetGlobalCooldown(BasePlayer player)
        {
            return IsVip(player) ? config.Safety.VipGlobalCooldown : config.Safety.GlobalCooldown;
        }

        private int GetRemainingGlobalCooldown(BasePlayer player, PlayerData pdata, int now)
        {
            if (!config.Safety.UseGlobalCooldown)
            {
                return 0;
            }

            int configured = GetGlobalCooldown(player);
            if (configured <= 0)
            {
                return 0;
            }

            return pdata.GlobalCooldownUntil - now;
        }

        private string GetDateKey()
        {
            return DateTime.UtcNow.ToString("yyyy-MM-dd");
        }

        private int GetUnix()
        {
            return (int)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
        }

        private bool IsCrafting(BasePlayer player)
        {
            return player.inventory != null && player.inventory.crafting != null && player.inventory.crafting.queue != null && player.inventory.crafting.queue.Count > 0;
        }

        private bool IsValidHomeName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (char.IsLetterOrDigit(c) || c == '_' || c == '-')
                {
                    continue;
                }
                return false;
            }

            return true;
        }

        private string FormatTime(int seconds)
        {
            if (seconds <= 0)
            {
                return Lang("SecondsShort");
            }

            if (seconds < 60)
            {
                return seconds.ToString() + "s";
            }

            if (seconds < 3600)
            {
                return (seconds / 60).ToString() + "m";
            }

            return (seconds / 3600).ToString() + "h";
        }

        private string Lang(string key, string userId = null, params object[] args)
        {
            string message = lang.GetMessage(key, this, userId);
            return args != null && args.Length > 0 ? string.Format(message, args) : message;
        }

        private void Reply(BasePlayer player, string message)
        {
            if (player == null)
            {
                return;
            }

            string finalMessage = config.Chat.Prefix + message;
            SendReply(player, finalMessage);
        }

        private void ReplyKey(BasePlayer player, string key, params object[] args)
        {
            Reply(player, Lang(key, player != null ? player.UserIDString : null, args));
        }

        private void ReplyConsole(string message)
        {
            Puts(message);
        }

        private void DebugLog(string message)
        {
            if (config != null && config.Debug)
            {
                Puts("[DEBUG] " + message);
            }
        }

        #endregion

        #region Messages

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["HomeDisabled"] = "Homes are disabled.",
                ["NoPermHome"] = "You do not have permission to use /home.",
                ["HomeUsage"] = "Usage: /home add <name>, /home list, /home remove <name>, /home <name>",
                ["HomeUsageAdd"] = "Usage: /home add <name>",
                ["HomeUsageList"] = "Usage: /home list",
                ["HomeUsageRemove"] = "Usage: /home remove <name>",
                ["OutpostDisabled"] = "Outpost teleport is disabled.",
                ["NoPermOutpost"] = "You do not have permission to teleport to outpost.",
                ["OutpostNotFound"] = "Outpost was not found.",
                ["BanditDisabled"] = "Bandit teleport is disabled.",
                ["NoPermBandit"] = "You do not have permission to teleport to bandit.",
                ["BanditNotFound"] = "Bandit camp was not found.",
                ["NoActiveTeleport"] = "You have no active teleport countdown.",
                ["InvalidHomeName"] = "Home names can only use letters, numbers, underscore and dash.",
                ["HomeExists"] = "You already have a home with that name.",
                ["HomeLimitReached"] = "You have reached your home limit of {0}.",
                ["HomeSaved"] = "Home '{0}' saved.",
                ["NoHomesSaved"] = "You have no homes saved. {0}",
                ["YourHomes"] = "Your homes:",
                ["HomeListEntry"] = "- {0}: ({1:0.0}, {2:0.0}, {3:0.0})",
                ["HomeNotFound"] = "Home not found.",
                ["HomeRemoved"] = "Home '{0}' removed.",
                ["NoPermMigration"] = "You are not allowed to run migration manually.",
                ["MigrationAttempted"] = "Manual migration attempted. Use /home list to check imported homes.",
                ["MigrationAttemptedConsole"] = "Manual migration attempted. Check /home list in game.",
                ["SetHomeWounded"] = "You cannot set a home while wounded.",
                ["SetHomeSwimming"] = "You cannot set a home while swimming.",
                ["SetHomeCrafting"] = "You cannot set a home while crafting.",
                ["SetHomeNoCupboard"] = "You cannot set a home without a tool cupboard.",
                ["SetHomeBlocked"] = "You cannot set a home while building blocked.",
                ["SetHomeNotAuthed"] = "You must be authorized on the cupboard to set a home here.",
                ["TpDead"] = "You cannot teleport while dead.",
                ["TpWounded"] = "You cannot teleport while wounded.",
                ["TpSwimming"] = "You cannot teleport while swimming.",
                ["TpCrafting"] = "You cannot teleport while crafting.",
                ["TpHomeNotAuthed"] = "You must be authorized on a cupboard to teleport home.",
                ["TpRaidBlocked"] = "You cannot teleport while raid blocked.",
                ["TpCombatBlocked"] = "You cannot teleport while combat blocked.",
                ["TpAlreadyPending"] = "You already have a teleport countdown active.",
                ["TpNoBuild"] = "You cannot teleport without building privilege.",
                ["TpMounted"] = "You cannot teleport while mounted.",
                ["TpCargo"] = "You cannot teleport from cargo ship.",
                ["TpHostile"] = "You cannot teleport while hostile.",
                ["HomeDailyLimitReached"] = "You have reached your daily home teleport limit of {0}.",
                ["TownDailyLimitReached"] = "You have reached your daily {0} teleport limit of {1}.",
                ["DailyLimitUnlimited"] = "Daily limit: unlimited.",
                ["DailyRemaining"] = "Daily remaining: {0}/{1}.",
                ["SecondsShort"] = "0s",
                ["CancelDamage"] = "Teleport cancelled because you took damage.",
                ["CancelPlayerDamage"] = "Teleport cancelled due to player damage.",
                ["CancelFallDamage"] = "Teleport cancelled due to fall damage.",
                ["CancelGeneric"] = "Teleport cancelled.",
                ["HomeTeleportStarted"] = "Teleporting to home '{0}' in {1} seconds.",
                ["TownTeleportStarted"] = "Teleporting to {0} in {1} seconds.",
                ["HomeTeleportSuccess"] = "Teleported to home '{0}'.",
                ["TownTeleportSuccess"] = "Teleported to {0}.",
            }, this);
        }

        #endregion
    }
}
