    using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("CWEssentials", "whitecristafer", "1.0.0")]
    [Description("Essential admin tools for Rust servers: maintenance, teleport, god, fly, noclip, vanish, speed, heal, eat, clear, give, repair, repairall, kick, list, ping, time, day/night, rules, help.")]
    public class CWEssentials : RustPlugin
    {
        #region Constants

        private const string PluginVersion = "1.0.0";
        private const string AdminPermission = "cwessentials.admin";
        private const string TargetOthersPermission = "cwessentials.target.others";
        private const string PermissionBase = "cwessentials.";

        // Default values
        private const float DefaultMessageSize = 14;
        private const float DefaultTitleSize = 16;
        private const string DefaultChatPrefix = "<size=12><color=#66ccff><b>CWEssentials</b></color></size> |";
        private const ulong DefaultPluginIcon = 76561198209258869UL;
        private const string DefaultMaintenanceMessage = "Server is under maintenance. Please come back later.";

        #endregion

        #region Configuration

        private PluginConfig _config;

        private class PluginConfig
        {
            [JsonProperty("ConfigVersion")]
            public int Version = 1;

            [JsonProperty("Settings")]
            public SettingsConfig Settings = new SettingsConfig();

            [JsonProperty("Commands")]
            public CommandsConfig Commands = new CommandsConfig();

            [JsonProperty("Maintenance")]
            public MaintenanceConfig Maintenance = new MaintenanceConfig();
        }

        private class SettingsConfig
        {
            [JsonProperty("ChatPrefix")]
            public string ChatPrefix = DefaultChatPrefix;

            [JsonProperty("PluginIcon")]
            public ulong PluginIcon = DefaultPluginIcon;

            [JsonProperty("MessageSize")]
            public int MessageSize = (int)DefaultMessageSize;

            [JsonProperty("TitleSize")]
            public int TitleSize = (int)DefaultTitleSize;

            [JsonProperty("Colors")]
            public ColorsConfig Colors = new ColorsConfig();

            [JsonProperty("MaintenanceMessage")]
            public string MaintenanceMessage = DefaultMaintenanceMessage;

            [JsonProperty("RulesFile")]
            public string RulesFile = "rules.txt";
        }

        private class ColorsConfig
        {
            [JsonProperty("Info")]
            public string Info = "#aaddff";

            [JsonProperty("Success")]
            public string Success = "#66ff66";

            [JsonProperty("Warning")]
            public string Warning = "#ffaa00";

            [JsonProperty("Error")]
            public string Error = "#ff6666";

            [JsonProperty("Highlight")]
            public string Highlight = "#ffffff";
        }

        private class CommandsConfig
        {
            [JsonProperty("Maintenance")] public CommandEntry Maintenance = new CommandEntry();
            [JsonProperty("God")] public CommandEntry God = new CommandEntry();
            [JsonProperty("Fly")] public CommandEntry Fly = new CommandEntry();
            [JsonProperty("Noclip")] public CommandEntry Noclip = new CommandEntry();
            [JsonProperty("Vanish")] public CommandEntry Vanish = new CommandEntry();
            [JsonProperty("Speed")] public CommandEntry Speed = new CommandEntry();
            [JsonProperty("TP")] public CommandEntry TP = new CommandEntry();
            [JsonProperty("TPHere")] public CommandEntry TPHere = new CommandEntry();
            [JsonProperty("TPLoc")] public CommandEntry TPLoc = new CommandEntry();
            [JsonProperty("Heal")] public CommandEntry Heal = new CommandEntry();
            [JsonProperty("Eat")] public CommandEntry Eat = new CommandEntry();
            [JsonProperty("Clear")] public CommandEntry Clear = new CommandEntry();
            [JsonProperty("Give")] public CommandEntry Give = new CommandEntry();
            [JsonProperty("Repair")] public CommandEntry Repair = new CommandEntry();
            [JsonProperty("RepairAll")] public CommandEntry RepairAll = new CommandEntry();
            [JsonProperty("Kick")] public CommandEntry Kick = new CommandEntry();
            [JsonProperty("List")] public CommandEntry List = new CommandEntry();
            [JsonProperty("Ping")] public CommandEntry Ping = new CommandEntry();
            [JsonProperty("Time")] public CommandEntry Time = new CommandEntry();
            [JsonProperty("DayNight")] public CommandEntry DayNight = new CommandEntry();
            [JsonProperty("Rules")] public CommandEntry Rules = new CommandEntry();
            [JsonProperty("Help")] public CommandEntry Help = new CommandEntry();
            [JsonProperty("Reload")] public CommandEntry Reload = new CommandEntry();
            [JsonProperty("Version")] public CommandEntry Version = new CommandEntry();
        }

        private class CommandEntry
        {
            [JsonProperty("Enabled")]
            public bool Enabled = true;
        }

        private class MaintenanceConfig
        {
            [JsonProperty("Enabled")]
            public bool Enabled = false;

            [JsonProperty("Whitelist")]
            public List<string> Whitelist = new List<string>();
        }

        protected override void LoadDefaultConfig()
        {
            _config = new PluginConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<PluginConfig>();
                if (_config == null) _config = new PluginConfig();
                NormalizeConfig();
                SaveConfig();
            }
            catch (Exception ex)
            {
                PrintWarning($"Configuration load error: {ex.Message}. Creating default config.");
                _config = new PluginConfig();
                SaveConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(_config, true);

        private void NormalizeConfig()
        {
            bool migrated = false;

            if (_config.Version != 1)
            {
                _config.Version = 1;
                migrated = true;
            }

            // Ensure all sections exist
            if (_config.Settings == null) { _config.Settings = new SettingsConfig(); migrated = true; }
            if (_config.Commands == null) { _config.Commands = new CommandsConfig(); migrated = true; }
            if (_config.Maintenance == null) { _config.Maintenance = new MaintenanceConfig(); migrated = true; }
            if (_config.Settings.Colors == null) { _config.Settings.Colors = new ColorsConfig(); migrated = true; }

            // Validate fields
            if (string.IsNullOrWhiteSpace(_config.Settings.ChatPrefix))
                _config.Settings.ChatPrefix = DefaultChatPrefix;
            if (_config.Settings.PluginIcon == 0)
                _config.Settings.PluginIcon = DefaultPluginIcon;
            _config.Settings.MessageSize = Mathf.Clamp(_config.Settings.MessageSize, 10, 24);
            _config.Settings.TitleSize = Mathf.Clamp(_config.Settings.TitleSize, 10, 24);
            if (string.IsNullOrWhiteSpace(_config.Settings.MaintenanceMessage))
                _config.Settings.MaintenanceMessage = DefaultMaintenanceMessage;
            if (string.IsNullOrWhiteSpace(_config.Settings.RulesFile))
                _config.Settings.RulesFile = "rules.txt";

            // Normalize hex colors
            _config.Settings.Colors.Info = NormalizeHex(_config.Settings.Colors.Info, "#aaddff");
            _config.Settings.Colors.Success = NormalizeHex(_config.Settings.Colors.Success, "#66ff66");
            _config.Settings.Colors.Warning = NormalizeHex(_config.Settings.Colors.Warning, "#ffaa00");
            _config.Settings.Colors.Error = NormalizeHex(_config.Settings.Colors.Error, "#ff6666");
            _config.Settings.Colors.Highlight = NormalizeHex(_config.Settings.Colors.Highlight, "#ffffff");

            // Ensure whitelist is not null
            if (_config.Maintenance.Whitelist == null)
            {
                _config.Maintenance.Whitelist = new List<string>();
                migrated = true;
            }

            if (migrated) PrintWarning("Configuration was migrated to the latest version.");
        }

        private string NormalizeHex(string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value)) return fallback;
            value = value.Trim();
            if (!value.StartsWith("#")) value = "#" + value;
            if (!Regex.IsMatch(value, "^#(?:[0-9a-fA-F]{3}){1,2}$")) return fallback;
            return value;
        }

        #endregion

        #region Data

        private StoredData _data;

        private class StoredData
        {
            public Dictionary<ulong, PlayerState> PlayerStates = new Dictionary<ulong, PlayerState>();
        }

        private class PlayerState
        {
            public bool God = false;
            public bool Fly = false;
            public bool Noclip = false;
            public bool Vanish = false;
            public float Speed = 1f;
        }

        private void LoadData()
        {
            try
            {
                _data = Interface.Oxide.DataFileSystem.ReadObject<StoredData>("CWEssentials_Data");
            }
            catch
            {
                _data = null;
            }
            if (_data == null)
            {
                _data = new StoredData();
                SaveData();
            }
            if (_data.PlayerStates == null) _data.PlayerStates = new Dictionary<ulong, PlayerState>();
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("CWEssentials_Data", _data);
        }

        #endregion

        #region Localization (English only)

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoPermission"] = "You don't have permission to use this command.",
                ["NoTargetPermission"] = "You don't have permission to target other players.",
                ["CommandDisabled"] = "This command is disabled in configuration.",
                ["PlayerNotFound"] = "Player not found.",
                ["InvalidSyntax"] = "Invalid syntax. Use: {0}",
                ["InvalidNumber"] = "Invalid number.",
                ["InvalidCoordinates"] = "Invalid coordinates. Use: /tploc <x> <y> <z>",
                ["InvalidSpeed"] = "Speed must be between 1 and 10.",
                ["MaintenanceOn"] = "Maintenance mode enabled. All non-whitelisted players have been kicked.",
                ["MaintenanceOff"] = "Maintenance mode disabled.",
                ["MaintenanceAlreadyOn"] = "Maintenance mode is already enabled.",
                ["MaintenanceAlreadyOff"] = "Maintenance mode is already disabled.",
                ["MaintenanceAdded"] = "Player {0} added to whitelist.",
                ["MaintenanceRemoved"] = "Player {0} removed from whitelist.",
                ["MaintenanceNotInWhitelist"] = "Player {0} is not in the whitelist.",
                ["MaintenanceAlreadyInWhitelist"] = "Player {0} is already in the whitelist.",
                ["MaintenanceListHeader"] = "Maintenance Whitelist ({0} entries):",
                ["MaintenanceListEntry"] = "- {0}",
                ["MaintenanceListEmpty"] = "Whitelist is empty.",
                ["MaintenanceStatus"] = "Maintenance mode is {0}.",
                ["MaintenanceStatusOn"] = "ON",
                ["MaintenanceStatusOff"] = "OFF",
                ["GodOn"] = "God mode enabled.",
                ["GodOff"] = "God mode disabled.",
                ["GodOnOther"] = "God mode enabled for {0}.",
                ["GodOffOther"] = "God mode disabled for {0}.",
                ["FlyOn"] = "Flight mode enabled.",
                ["FlyOff"] = "Flight mode disabled.",
                ["FlyOnOther"] = "Flight mode enabled for {0}.",
                ["FlyOffOther"] = "Flight mode disabled for {0}.",
                ["NoclipOn"] = "Noclip mode enabled.",
                ["NoclipOff"] = "Noclip mode disabled.",
                ["NoclipOnOther"] = "Noclip mode enabled for {0}.",
                ["NoclipOffOther"] = "Noclip mode disabled for {0}.",
                ["VanishOn"] = "Vanish mode enabled.",
                ["VanishOff"] = "Vanish mode disabled.",
                ["VanishOnOther"] = "Vanish mode enabled for {0}.",
                ["VanishOffOther"] = "Vanish mode disabled for {0}.",
                ["SpeedSet"] = "Speed set to {0}.",
                ["SpeedSetOther"] = "Speed set to {0} for {1}.",
                ["SpeedReset"] = "Speed reset to normal.",
                ["SpeedResetOther"] = "Speed reset to normal for {0}.",
                ["TeleportToTarget"] = "Teleported to {0}.",
                ["TeleportHere"] = "Teleported {0} to your location.",
                ["TeleportToLoc"] = "Teleported to coordinates {0}.",
                ["HealDone"] = "You have been fully healed.",
                ["HealOther"] = "Healed {0}.",
                ["EatDone"] = "You have been fully fed and hydrated.",
                ["EatOther"] = "Fed and hydrated {0}.",
                ["ClearDone"] = "Your inventory has been cleared.",
                ["ClearOther"] = "Cleared inventory of {0}.",
                ["GiveSelf"] = "You received {0} x {1}.",
                ["GiveOther"] = "Gave {0} x {1} to {2}.",
                ["GiveNotFound"] = "Item '{0}' not found.",
                ["RepairDone"] = "Item repaired.",
                ["RepairOther"] = "Repaired item of {0}.",
                ["RepairAllDone"] = "All items repaired.",
                ["RepairAllOther"] = "All items repaired for {0}.",
                ["KickSuccess"] = "Kicked {0} with reason: {1}",
                ["KickSelf"] = "You cannot kick yourself.",
                ["ListHeader"] = "Players online ({0}):",
                ["ListEntry"] = "{0} [SteamID: {1}]",
                ["PingResult"] = "Your ping: {0}ms.",
                ["TimeResult"] = "Current time: {0}.",
                ["DaySet"] = "Time set to day.",
                ["NightSet"] = "Time set to night.",
                ["RulesHeader"] = "Server Rules:",
                ["RulesLine"] = "  {0}",
                ["RulesNotFound"] = "Rules file not found.",
                ["HelpHeader"] = "<size=16><color=#66ccff><b>CWEssentials Help</b></color></size>",
                ["HelpPage"] = "Page {0}/{1}",
                ["HelpLine"] = "/{0} - {1}",
                ["HelpNoCommands"] = "No commands available.",
                ["ReloadSuccess"] = "CWEssentials reloaded successfully.",
                ["ReloadFailed"] = "Reload failed. Check server logs.",
                ["VersionMessage"] = "CWEssentials v{0} by whitecristafer.",
                ["UnknownCommand"] = "Unknown command. Use /cwe help."
            }, this, "en");
        }

        #endregion

        #region Permissions

        private void RegisterPermissions()
        {
            permission.RegisterPermission(AdminPermission, this);
            permission.RegisterPermission(TargetOthersPermission, this);
            // Register command-specific permissions dynamically
            string[] perms = {
                "maintenance", "god", "fly", "noclip", "vanish", "speed",
                "tp", "tphere", "tploc", "heal", "eat", "clear", "give",
                "repair", "repairall", "kick", "list", "ping", "time", "time.set",
                "rules", "help", "reload", "version"
            };
            foreach (var p in perms)
                permission.RegisterPermission(PermissionBase + p, this);
            // Bypass permission for maintenance
            permission.RegisterPermission("cwessentials.maintenance.bypass", this);
        }

        private bool HasPermission(BasePlayer player, string perm)
        {
            if (player == null) return false;
            if (player.IsAdmin) return true;
            return permission.UserHasPermission(player.UserIDString, perm);
        }

        private bool IsAdmin(BasePlayer player) => HasPermission(player, AdminPermission);

        private bool CanTargetOthers(BasePlayer player) => HasPermission(player, TargetOthersPermission) || IsAdmin(player);

        private bool IsCommandEnabled(string cmdName)
        {
            var prop = typeof(CommandsConfig).GetProperty(cmdName);
            if (prop == null) return false;
            var entry = prop.GetValue(_config.Commands) as CommandEntry;
            return entry != null && entry.Enabled;
        }

        #endregion

        #region Initialization

        private void Init()
        {
            LoadConfig();
            LoadData();
            RegisterPermissions();
        }

        private void OnServerInitialized()
        {
            // If maintenance is enabled at startup, apply to all connected
            if (_config.Maintenance.Enabled)
                EnforceMaintenanceKick();

            // Start periodic speed/fly/noclip maintenance? We'll handle in hooks.
            PrintBanner();
        }

        private void Unload()
        {
            // Clean up vanish / noclip / fly flags for all players to avoid stuck state
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player == null) continue;
                if (player.HasPlayerFlag(BasePlayer.PlayerFlags.Flying))
                    player.SetPlayerFlag(BasePlayer.PlayerFlags.Flying, false);
                if (player.HasPlayerFlag(BasePlayer.PlayerFlags.Noclip))
                    player.SetPlayerFlag(BasePlayer.PlayerFlags.Noclip, false);
                if (player.HasPlayerFlag(BasePlayer.PlayerFlags.Invisible))
                    player.SetPlayerFlag(BasePlayer.PlayerFlags.Invisible, false);
                if (player.HasPlayerFlag(BasePlayer.PlayerFlags.SafeZone))
                    player.SetPlayerFlag(BasePlayer.PlayerFlags.SafeZone, false); // хотя мы не используем safe zone
                // Сбрасываем скорость
                player.walkSpeed = 1f;
                player.runSpeed = 1f;
                player.SendNetworkUpdateImmediate();
            }
            SaveData();
        }

        private void PrintBanner()
        {
            Puts($"===============================================");
            Puts($"CWEssentials v{PluginVersion} loaded.");
            Puts($"Maintenance: {(_config.Maintenance.Enabled ? "ON" : "OFF")}");
            Puts($"Whitelist count: {_config.Maintenance.Whitelist.Count}");
            Puts($"===============================================");
        }

        #endregion

        #region Helpers

        private BasePlayer FindPlayer(string ident)
        {
            if (string.IsNullOrEmpty(ident)) return null;
            if (ulong.TryParse(ident, out ulong id))
                return BasePlayer.FindByID(id);
            // Search by name (partial match)
            var players = BasePlayer.activePlayerList;
            var found = players.FirstOrDefault(p => p.displayName.IndexOf(ident, StringComparison.OrdinalIgnoreCase) >= 0);
            if (found != null) return found;
            // Try exact UserIDString
            return players.FirstOrDefault(p => p.UserIDString == ident);
        }

        private string Lang(string key, string playerId = null, params object[] args)
        {
            string msg = lang.GetMessage(key, this, playerId);
            if (args.Length > 0) msg = string.Format(msg, args);
            return msg;
        }

        private void SendMessage(BasePlayer player, string message, string colorKey = "Info")
        {
            if (player == null || string.IsNullOrEmpty(message)) return;
            var settings = _config.Settings;
            string prefix = settings.ChatPrefix;
            int size = settings.MessageSize;
            string color = settings.Colors[colorKey] ?? "#ffffff";
            string formatted = $"{prefix} <size={size}><color={color}>{message}</color></size>";
            player.SendConsoleCommand("chat.add", new object[] { settings.PluginIcon, formatted });
        }

        private void SendInfo(BasePlayer player, string msg) => SendMessage(player, msg, "Info");
        private void SendSuccess(BasePlayer player, string msg) => SendMessage(player, msg, "Success");
        private void SendWarning(BasePlayer player, string msg) => SendMessage(player, msg, "Warning");
        private void SendError(BasePlayer player, string msg) => SendMessage(player, msg, "Error");

        private void LogToConsole(string message)
        {
            // Strip rich tags for console
            string plain = Regex.Replace(message, "<[^>]*>", "");
            Puts($"[CWEssentials] {plain}");
        }

        private void TeleportPlayer(BasePlayer player, Vector3 pos, Quaternion rot = default)
        {
            if (player == null || !player.IsConnected) return;
            Effect.server.Run("assets/prefabs/misc/transferable/effects/teleport.prefab", player.transform.position, Vector3.up);
            player.Teleport(pos);
            if (rot != default)
                player.eyes.rotation = rot;
            player.SendNetworkUpdateImmediate();
            Effect.server.Run("assets/prefabs/misc/transferable/effects/teleport.prefab", pos, Vector3.up);
        }

        private void EnforceMaintenanceKick()
        {
            if (!_config.Maintenance.Enabled) return;
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (!HasPermission(player, "cwessentials.maintenance.bypass"))
                    player.Kick(_config.Settings.MaintenanceMessage);
            }
        }

        private PlayerState GetState(BasePlayer player)
        {
            if (player == null) return null;
            if (!_data.PlayerStates.TryGetValue(player.userID, out var state))
            {
                state = new PlayerState();
                _data.PlayerStates[player.userID] = state;
            }
            return state;
        }

        private void SaveState(BasePlayer player) => SaveData();

        #endregion

        #region Chat Commands

        [ChatCommand("maintenance")]
        private void CmdMaintenance(BasePlayer player, string cmd, string[] args)
        {
            if (!IsCommandEnabled("Maintenance")) { SendError(player, Lang("CommandDisabled")); return; }
            if (!HasPermission(player, PermissionBase + "maintenance")) { SendError(player, Lang("NoPermission")); return; }

            if (args.Length == 0)
            {
                // Toggle
                bool newState = !_config.Maintenance.Enabled;
                _config.Maintenance.Enabled = newState;
                SaveConfig();
                if (newState)
                {
                    EnforceMaintenanceKick();
                    SendSuccess(player, Lang("MaintenanceOn"));
                }
                else
                {
                    SendSuccess(player, Lang("MaintenanceOff"));
                }
                LogToConsole($"Maintenance toggled {(newState ? "ON" : "OFF")} by {player.displayName}");
                return;
            }

            switch (args[0].ToLowerInvariant())
            {
                case "add":
                    if (args.Length < 2) { SendError(player, Lang("InvalidSyntax", player.UserIDString, "/maintenance add <name|id>")); return; }
                    var targetAdd = FindPlayer(args[1]);
                    if (targetAdd == null) { SendError(player, Lang("PlayerNotFound")); return; }
                    string idAdd = targetAdd.UserIDString;
                    if (_config.Maintenance.Whitelist.Contains(idAdd))
                        SendWarning(player, Lang("MaintenanceAlreadyInWhitelist", player.UserIDString, targetAdd.displayName));
                    else
                    {
                        _config.Maintenance.Whitelist.Add(idAdd);
                        SaveConfig();
                        SendSuccess(player, Lang("MaintenanceAdded", player.UserIDString, targetAdd.displayName));
                        LogToConsole($"Added {targetAdd.displayName} ({idAdd}) to maintenance whitelist by {player.displayName}");
                    }
                    break;

                case "remove":
                    if (args.Length < 2) { SendError(player, Lang("InvalidSyntax", player.UserIDString, "/maintenance remove <name|id>")); return; }
                    var targetRem = FindPlayer(args[1]);
                    if (targetRem == null) { SendError(player, Lang("PlayerNotFound")); return; }
                    string idRem = targetRem.UserIDString;
                    if (!_config.Maintenance.Whitelist.Contains(idRem))
                        SendWarning(player, Lang("MaintenanceNotInWhitelist", player.UserIDString, targetRem.displayName));
                    else
                    {
                        _config.Maintenance.Whitelist.Remove(idRem);
                        SaveConfig();
                        SendSuccess(player, Lang("MaintenanceRemoved", player.UserIDString, targetRem.displayName));
                        LogToConsole($"Removed {targetRem.displayName} ({idRem}) from maintenance whitelist by {player.displayName}");
                    }
                    break;

                case "list":
                    var list = _config.Maintenance.Whitelist;
                    if (list.Count == 0)
                    {
                        SendInfo(player, Lang("MaintenanceListEmpty"));
                    }
                    else
                    {
                        SendInfo(player, Lang("MaintenanceListHeader", player.UserIDString, list.Count));
                        foreach (var entry in list)
                            SendInfo(player, Lang("MaintenanceListEntry", player.UserIDString, entry));
                    }
                    break;

                case "status":
                    string status = _config.Maintenance.Enabled ? Lang("MaintenanceStatusOn") : Lang("MaintenanceStatusOff");
                    SendInfo(player, Lang("MaintenanceStatus", player.UserIDString, status));
                    break;

                default:
                    SendError(player, Lang("UnknownCommand"));
                    break;
            }
        }

        [ChatCommand("god")]
        private void CmdGod(BasePlayer player, string cmd, string[] args)
        {
            if (!IsCommandEnabled("God")) { SendError(player, Lang("CommandDisabled")); return; }
            if (!HasPermission(player, PermissionBase + "god")) { SendError(player, Lang("NoPermission")); return; }

            BasePlayer target = player;
            if (args.Length > 0)
            {
                target = FindPlayer(args[0]);
                if (target == null) { SendError(player, Lang("PlayerNotFound")); return; }
                if (target != player && !CanTargetOthers(player))
                { SendError(player, Lang("NoTargetPermission")); return; }
            }

            var state = GetState(target);
            state.God = !state.God;
            SaveData();
            if (target == player)
                SendSuccess(player, state.God ? Lang("GodOn") : Lang("GodOff"));
            else
                SendSuccess(player, state.God ? Lang("GodOnOther", player.UserIDString, target.displayName) : Lang("GodOffOther", player.UserIDString, target.displayName));
            LogToConsole($"God {(state.God ? "ON" : "OFF")} for {target.displayName} by {player.displayName}");
        }

        [ChatCommand("fly")]
        private void CmdFly(BasePlayer player, string cmd, string[] args)
        {
            if (!IsCommandEnabled("Fly")) { SendError(player, Lang("CommandDisabled")); return; }
            if (!HasPermission(player, PermissionBase + "fly")) { SendError(player, Lang("NoPermission")); return; }

            BasePlayer target = player;
            if (args.Length > 0)
            {
                target = FindPlayer(args[0]);
                if (target == null) { SendError(player, Lang("PlayerNotFound")); return; }
                if (target != player && !CanTargetOthers(player))
                { SendError(player, Lang("NoTargetPermission")); return; }
            }

            var state = GetState(target);
            state.Fly = !state.Fly;
            target.SetPlayerFlag(BasePlayer.PlayerFlags.Flying, state.Fly);
            target.SendNetworkUpdateImmediate();
            SaveData();
            if (target == player)
                SendSuccess(player, state.Fly ? Lang("FlyOn") : Lang("FlyOff"));
            else
                SendSuccess(player, state.Fly ? Lang("FlyOnOther", player.UserIDString, target.displayName) : Lang("FlyOffOther", player.UserIDString, target.displayName));
            LogToConsole($"Fly {(state.Fly ? "ON" : "OFF")} for {target.displayName} by {player.displayName}");
        }

        [ChatCommand("noclip")]
        private void CmdNoclip(BasePlayer player, string cmd, string[] args)
        {
            if (!IsCommandEnabled("Noclip")) { SendError(player, Lang("CommandDisabled")); return; }
            if (!HasPermission(player, PermissionBase + "noclip")) { SendError(player, Lang("NoPermission")); return; }

            BasePlayer target = player;
            if (args.Length > 0)
            {
                target = FindPlayer(args[0]);
                if (target == null) { SendError(player, Lang("PlayerNotFound")); return; }
                if (target != player && !CanTargetOthers(player))
                { SendError(player, Lang("NoTargetPermission")); return; }
            }

            var state = GetState(target);
            state.Noclip = !state.Noclip;
            target.SetPlayerFlag(BasePlayer.PlayerFlags.Noclip, state.Noclip);
            target.SetPlayerFlag(BasePlayer.PlayerFlags.Flying, state.Noclip); // noclip implies fly
            target.SendNetworkUpdateImmediate();
            SaveData();
            if (target == player)
                SendSuccess(player, state.Noclip ? Lang("NoclipOn") : Lang("NoclipOff"));
            else
                SendSuccess(player, state.Noclip ? Lang("NoclipOnOther", player.UserIDString, target.displayName) : Lang("NoclipOffOther", player.UserIDString, target.displayName));
            LogToConsole($"Noclip {(state.Noclip ? "ON" : "OFF")} for {target.displayName} by {player.displayName}");
        }

        [ChatCommand("vanish")]
        private void CmdVanish(BasePlayer player, string cmd, string[] args)
        {
            if (!IsCommandEnabled("Vanish")) { SendError(player, Lang("CommandDisabled")); return; }
            if (!HasPermission(player, PermissionBase + "vanish")) { SendError(player, Lang("NoPermission")); return; }

            BasePlayer target = player;
            if (args.Length > 0)
            {
                target = FindPlayer(args[0]);
                if (target == null) { SendError(player, Lang("PlayerNotFound")); return; }
                if (target != player && !CanTargetOthers(player))
                { SendError(player, Lang("NoTargetPermission")); return; }
            }

            var state = GetState(target);
            state.Vanish = !state.Vanish;
            target.SetPlayerFlag(BasePlayer.PlayerFlags.Invisible, state.Vanish);
            target.SendNetworkUpdateImmediate();
            SaveData();
            if (target == player)
                SendSuccess(player, state.Vanish ? Lang("VanishOn") : Lang("VanishOff"));
            else
                SendSuccess(player, state.Vanish ? Lang("VanishOnOther", player.UserIDString, target.displayName) : Lang("VanishOffOther", player.UserIDString, target.displayName));
            LogToConsole($"Vanish {(state.Vanish ? "ON" : "OFF")} for {target.displayName} by {player.displayName}");
        }

        [ChatCommand("speed")]
        private void CmdSpeed(BasePlayer player, string cmd, string[] args)
        {
            if (!IsCommandEnabled("Speed")) { SendError(player, Lang("CommandDisabled")); return; }
            if (!HasPermission(player, PermissionBase + "speed")) { SendError(player, Lang("NoPermission")); return; }

            if (args.Length == 0) { SendError(player, Lang("InvalidSyntax", player.UserIDString, "/speed <1-10> or /speed <target> <1-10>")); return; }

            BasePlayer target = player;
            int speedArgIndex = 0;
            float speed = 1f;
            bool reset = false;

            // Check if first arg is a player name
            BasePlayer possibleTarget = FindPlayer(args[0]);
            if (possibleTarget != null && possibleTarget != player)
            {
                target = possibleTarget;
                if (!CanTargetOthers(player)) { SendError(player, Lang("NoTargetPermission")); return; }
                speedArgIndex = 1;
            }

            if (args.Length > speedArgIndex)
            {
                if (args[speedArgIndex].ToLowerInvariant() == "reset")
                {
                    reset = true;
                    speed = 1f;
                }
                else if (float.TryParse(args[speedArgIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out speed))
                {
                    speed = Mathf.Clamp(speed, 1f, 10f);
                }
                else
                {
                    SendError(player, Lang("InvalidNumber"));
                    return;
                }
            }
            else
            {
                // No speed value provided, toggle? Actually we set default or reset.
                reset = true;
                speed = 1f;
            }

            var state = GetState(target);
            if (reset)
            {
                state.Speed = 1f;
                target.walkSpeed = 1f;
                target.runSpeed = 1f;
                target.SendNetworkUpdateImmediate();
                SaveData();
                if (target == player)
                    SendSuccess(player, Lang("SpeedReset"));
                else
                    SendSuccess(player, Lang("SpeedResetOther", player.UserIDString, target.displayName));
            }
            else
            {
                state.Speed = speed;
                target.walkSpeed = speed;
                target.runSpeed = speed;
                target.SendNetworkUpdateImmediate();
                SaveData();
                if (target == player)
                    SendSuccess(player, Lang("SpeedSet", player.UserIDString, speed));
                else
                    SendSuccess(player, Lang("SpeedSetOther", player.UserIDString, speed, target.displayName));
            }
            LogToConsole($"Speed set to {(reset ? "1 (reset)" : speed.ToString())} for {target.displayName} by {player.displayName}");
        }

        [ChatCommand("tp")]
        private void CmdTp(BasePlayer player, string cmd, string[] args)
        {
            if (!IsCommandEnabled("TP")) { SendError(player, Lang("CommandDisabled")); return; }
            if (!HasPermission(player, PermissionBase + "tp")) { SendError(player, Lang("NoPermission")); return; }
            if (args.Length < 1) { SendError(player, Lang("InvalidSyntax", player.UserIDString, "/tp <player>")); return; }

            BasePlayer target = FindPlayer(args[0]);
            if (target == null) { SendError(player, Lang("PlayerNotFound")); return; }
            if (target == player) { SendError(player, "You cannot teleport to yourself."); return; }

            TeleportPlayer(player, target.transform.position, target.eyes.rotation);
            SendSuccess(player, Lang("TeleportToTarget", player.UserIDString, target.displayName));
            LogToConsole($"{player.displayName} teleported to {target.displayName}");
        }

        [ChatCommand("tphere")]
        private void CmdTpHere(BasePlayer player, string cmd, string[] args)
        {
            if (!IsCommandEnabled("TPHere")) { SendError(player, Lang("CommandDisabled")); return; }
            if (!HasPermission(player, PermissionBase + "tphere")) { SendError(player, Lang("NoPermission")); return; }
            if (args.Length < 1) { SendError(player, Lang("InvalidSyntax", player.UserIDString, "/tphere <player>")); return; }

            BasePlayer target = FindPlayer(args[0]);
            if (target == null) { SendError(player, Lang("PlayerNotFound")); return; }
            if (target == player) { SendError(player, "You cannot teleport yourself to yourself."); return; }
            if (target != player && !CanTargetOthers(player))
            { SendError(player, Lang("NoTargetPermission")); return; }

            TeleportPlayer(target, player.transform.position, player.eyes.rotation);
            SendSuccess(player, Lang("TeleportHere", player.UserIDString, target.displayName));
            LogToConsole($"{player.displayName} teleported {target.displayName} to themselves");
        }

        [ChatCommand("tploc")]
        private void CmdTpLoc(BasePlayer player, string cmd, string[] args)
        {
            if (!IsCommandEnabled("TPLoc")) { SendError(player, Lang("CommandDisabled")); return; }
            if (!HasPermission(player, PermissionBase + "tploc")) { SendError(player, Lang("NoPermission")); return; }
            if (args.Length < 3) { SendError(player, Lang("InvalidSyntax", player.UserIDString, "/tploc <x> <y> <z>")); return; }

            if (!float.TryParse(args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) ||
                !float.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) ||
                !float.TryParse(args[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
            {
                SendError(player, Lang("InvalidCoordinates"));
                return;
            }

            Vector3 pos = new Vector3(x, y, z);
            TeleportPlayer(player, pos);
            SendSuccess(player, Lang("TeleportToLoc", player.UserIDString, pos.ToString()));
            LogToConsole($"{player.displayName} teleported to {pos}");
        }

        [ChatCommand("heal")]
        private void CmdHeal(BasePlayer player, string cmd, string[] args)
        {
            if (!IsCommandEnabled("Heal")) { SendError(player, Lang("CommandDisabled")); return; }
            if (!HasPermission(player, PermissionBase + "heal")) { SendError(player, Lang("NoPermission")); return; }

            BasePlayer target = player;
            if (args.Length > 0)
            {
                target = FindPlayer(args[0]);
                if (target == null) { SendError(player, Lang("PlayerNotFound")); return; }
                if (target != player && !CanTargetOthers(player))
                { SendError(player, Lang("NoTargetPermission")); return; }
            }

            target.health = target.MaxHealth();
            target.metabolism.radiation_poison.value = 0f;
            target.metabolism.poison.value = 0f;
            target.metabolism.bleeding.value = 0f;
            target.SendNetworkUpdateImmediate();
            if (target == player)
                SendSuccess(player, Lang("HealDone"));
            else
                SendSuccess(player, Lang("HealOther", player.UserIDString, target.displayName));
            LogToConsole($"{player.displayName} healed {target.displayName}");
        }

        [ChatCommand("eat")]
        private void CmdEat(BasePlayer player, string cmd, string[] args)
        {
            if (!IsCommandEnabled("Eat")) { SendError(player, Lang("CommandDisabled")); return; }
            if (!HasPermission(player, PermissionBase + "eat")) { SendError(player, Lang("NoPermission")); return; }

            BasePlayer target = player;
            if (args.Length > 0)
            {
                target = FindPlayer(args[0]);
                if (target == null) { SendError(player, Lang("PlayerNotFound")); return; }
                if (target != player && !CanTargetOthers(player))
                { SendError(player, Lang("NoTargetPermission")); return; }
            }

            target.metabolism.calories.value = target.metabolism.calories.max;
            target.metabolism.hydration.value = target.metabolism.hydration.max;
            target.SendNetworkUpdateImmediate();
            if (target == player)
                SendSuccess(player, Lang("EatDone"));
            else
                SendSuccess(player, Lang("EatOther", player.UserIDString, target.displayName));
            LogToConsole($"{player.displayName} fed {target.displayName}");
        }

        [ChatCommand("clear")]
        private void CmdClear(BasePlayer player, string cmd, string[] args)
        {
            if (!IsCommandEnabled("Clear")) { SendError(player, Lang("CommandDisabled")); return; }
            if (!HasPermission(player, PermissionBase + "clear")) { SendError(player, Lang("NoPermission")); return; }

            BasePlayer target = player;
            if (args.Length > 0)
            {
                target = FindPlayer(args[0]);
                if (target == null) { SendError(player, Lang("PlayerNotFound")); return; }
                if (target != player && !CanTargetOthers(player))
                { SendError(player, Lang("NoTargetPermission")); return; }
            }

            target.inventory.Strip();
            if (target == player)
                SendSuccess(player, Lang("ClearDone"));
            else
                SendSuccess(player, Lang("ClearOther", player.UserIDString, target.displayName));
            LogToConsole($"{player.displayName} cleared inventory of {target.displayName}");
        }

        [ChatCommand("give")]
        private void CmdGive(BasePlayer player, string cmd, string[] args)
        {
            if (!IsCommandEnabled("Give")) { SendError(player, Lang("CommandDisabled")); return; }
            if (!HasPermission(player, PermissionBase + "give")) { SendError(player, Lang("NoPermission")); return; }
            if (args.Length < 2) { SendError(player, Lang("InvalidSyntax", player.UserIDString, "/give <item> <amount> [target]")); return; }

            string itemName = args[0];
            if (!int.TryParse(args[1], out int amount) || amount <= 0) { SendError(player, Lang("InvalidNumber")); return; }

            BasePlayer target = player;
            if (args.Length > 2)
            {
                target = FindPlayer(args[2]);
                if (target == null) { SendError(player, Lang("PlayerNotFound")); return; }
                if (target != player && !CanTargetOthers(player))
                { SendError(player, Lang("NoTargetPermission")); return; }
            }

            ItemDefinition def = ItemManager.FindItemDefinition(itemName);
            if (def == null) { SendError(player, Lang("GiveNotFound", player.UserIDString, itemName)); return; }

            Item item = ItemManager.Create(def, amount);
            if (item == null) { SendError(player, "Failed to create item."); return; }
            if (!target.inventory.GiveItem(item))
            {
                // Если инвентарь полон - выдать в рюкзак или уведомить
                item.Drop(target.transform.position + Vector3.up * 2f, Vector3.zero);
                SendWarning(player, $"Inventory full, item dropped on ground for {target.displayName}.");
            }

            if (target == player)
                SendSuccess(player, Lang("GiveSelf", player.UserIDString, amount, def.displayName.english));
            else
                SendSuccess(player, Lang("GiveOther", player.UserIDString, amount, def.displayName.english, target.displayName));
            LogToConsole($"{player.displayName} gave {amount} x {def.shortname} to {target.displayName}");
        }

        [ChatCommand("repair")]
        private void CmdRepair(BasePlayer player, string cmd, string[] args)
        {
            if (!IsCommandEnabled("Repair")) { SendError(player, Lang("CommandDisabled")); return; }
            if (!HasPermission(player, PermissionBase + "repair")) { SendError(player, Lang("NoPermission")); return; }

            BasePlayer target = player;
            if (args.Length > 0)
            {
                target = FindPlayer(args[0]);
                if (target == null) { SendError(player, Lang("PlayerNotFound")); return; }
                if (target != player && !CanTargetOthers(player))
                { SendError(player, Lang("NoTargetPermission")); return; }
            }

            Item item = target.GetActiveItem();
            if (item == null) { SendError(player, "You have no active item to repair."); return; }
            if (item.condition >= item.maxCondition) { SendWarning(player, "Item is already at full condition."); return; }

            item.Repair(true);
            if (target == player)
                SendSuccess(player, Lang("RepairDone"));
            else
                SendSuccess(player, Lang("RepairOther", player.UserIDString, target.displayName));
            LogToConsole($"{player.displayName} repaired active item of {target.displayName}");
        }

        [ChatCommand("repairall")]
        private void CmdRepairAll(BasePlayer player, string cmd, string[] args)
        {
            if (!IsCommandEnabled("RepairAll")) { SendError(player, Lang("CommandDisabled")); return; }
            if (!HasPermission(player, PermissionBase + "repairall")) { SendError(player, Lang("NoPermission")); return; }

            BasePlayer target = player;
            if (args.Length > 0)
            {
                target = FindPlayer(args[0]);
                if (target == null) { SendError(player, Lang("PlayerNotFound")); return; }
                if (target != player && !CanTargetOthers(player))
                { SendError(player, Lang("NoTargetPermission")); return; }
            }

            int repaired = 0;
            foreach (var item in target.inventory.AllItems())
            {
                if (item.condition < item.maxCondition)
                {
                    item.Repair(true);
                    repaired++;
                }
            }
            if (repaired == 0) { SendWarning(player, "No items needed repair."); return; }
            if (target == player)
                SendSuccess(player, Lang("RepairAllDone"));
            else
                SendSuccess(player, Lang("RepairAllOther", player.UserIDString, target.displayName));
            LogToConsole($"{player.displayName} repaired all items of {target.displayName} ({repaired} items)");
        }

        [ChatCommand("kick")]
        private void CmdKick(BasePlayer player, string cmd, string[] args)
        {
            if (!IsCommandEnabled("Kick")) { SendError(player, Lang("CommandDisabled")); return; }
            if (!HasPermission(player, PermissionBase + "kick")) { SendError(player, Lang("NoPermission")); return; }
            if (args.Length < 1) { SendError(player, Lang("InvalidSyntax", player.UserIDString, "/kick <player> [reason]")); return; }

            BasePlayer target = FindPlayer(args[0]);
            if (target == null) { SendError(player, Lang("PlayerNotFound")); return; }
            if (target == player) { SendError(player, Lang("KickSelf")); return; }

            string reason = args.Length > 1 ? string.Join(" ", args.Skip(1)) : "Kicked by admin.";
            target.Kick(reason);
            SendSuccess(player, Lang("KickSuccess", player.UserIDString, target.displayName, reason));
            LogToConsole($"{player.displayName} kicked {target.displayName} (reason: {reason})");
        }

        [ChatCommand("list")]
        private void CmdList(BasePlayer player, string cmd, string[] args)
        {
            if (!IsCommandEnabled("List")) { SendError(player, Lang("CommandDisabled")); return; }
            if (!HasPermission(player, PermissionBase + "list")) { SendError(player, Lang("NoPermission")); return; }

            var players = BasePlayer.activePlayerList;
            if (players.Count == 0) { SendInfo(player, "No players online."); return; }

            SendInfo(player, Lang("ListHeader", player.UserIDString, players.Count));
            foreach (var p in players)
                SendInfo(player, Lang("ListEntry", player.UserIDString, p.displayName, p.UserIDString));
        }

        [ChatCommand("ping")]
        private void CmdPing(BasePlayer player, string cmd, string[] args)
        {
            if (!IsCommandEnabled("Ping")) { SendError(player, Lang("CommandDisabled")); return; }
            if (!HasPermission(player, PermissionBase + "ping")) { SendError(player, Lang("NoPermission")); return; }

            int ping = player.net.connection.ping;
            SendInfo(player, Lang("PingResult", player.UserIDString, ping));
        }

        [ChatCommand("time")]
        private void CmdTime(BasePlayer player, string cmd, string[] args)
        {
            if (!IsCommandEnabled("Time")) { SendError(player, Lang("CommandDisabled")); return; }
            if (!HasPermission(player, PermissionBase + "time")) { SendError(player, Lang("NoPermission")); return; }

            float time = TOD_Sky.Instance.Cycle.Hour;
            SendInfo(player, Lang("TimeResult", player.UserIDString, $"{time:F2}"));
        }

        [ChatCommand("day")]
        private void CmdDay(BasePlayer player, string cmd, string[] args)
        {
            if (!IsCommandEnabled("DayNight")) { SendError(player, Lang("CommandDisabled")); return; }
            if (!HasPermission(player, PermissionBase + "time.set")) { SendError(player, Lang("NoPermission")); return; }

            TOD_Sky.Instance.Cycle.Hour = 12f;
            SendSuccess(player, Lang("DaySet"));
            LogToConsole($"{player.displayName} set time to day");
        }

        [ChatCommand("night")]
        private void CmdNight(BasePlayer player, string cmd, string[] args)
        {
            if (!IsCommandEnabled("DayNight")) { SendError(player, Lang("CommandDisabled")); return; }
            if (!HasPermission(player, PermissionBase + "time.set")) { SendError(player, Lang("NoPermission")); return; }

            TOD_Sky.Instance.Cycle.Hour = 0f;
            SendSuccess(player, Lang("NightSet"));
            LogToConsole($"{player.displayName} set time to night");
        }

        [ChatCommand("rules")]
        private void CmdRules(BasePlayer player, string cmd, string[] args)
        {
            if (!IsCommandEnabled("Rules")) { SendError(player, Lang("CommandDisabled")); return; }
            if (!HasPermission(player, PermissionBase + "rules")) { SendError(player, Lang("NoPermission")); return; }

            string filePath = Path.Combine(Interface.Oxide.ConfigDirectory, _config.Settings.RulesFile);
            if (!File.Exists(filePath))
            {
                SendError(player, Lang("RulesNotFound"));
                return;
            }

            var lines = File.ReadAllLines(filePath);
            if (lines.Length == 0) { SendInfo(player, "No rules defined."); return; }

            SendInfo(player, Lang("RulesHeader"));
            foreach (var line in lines)
                SendInfo(player, Lang("RulesLine", player.UserIDString, line));
        }

        [ChatCommand("cwe")]
        private void CmdCWE(BasePlayer player, string cmd, string[] args)
        {
            if (args.Length == 0)
            {
                ShowHelp(player, 1);
                return;
            }

            switch (args[0].ToLowerInvariant())
            {
                case "help":
                    int page = 1;
                    if (args.Length > 1 && int.TryParse(args[1], out page))
                        ShowHelp(player, page);
                    else
                        ShowHelp(player, 1);
                    break;

                case "reload":
                    CmdReload(player, cmd, args.Skip(1).ToArray());
                    break;

                case "version":
                    CmdVersion(player, cmd, args.Skip(1).ToArray());
                    break;

                default:
                    SendError(player, Lang("UnknownCommand"));
                    break;
            }
        }

        private void ShowHelp(BasePlayer player, int page)
        {
            if (!IsCommandEnabled("Help")) { SendError(player, Lang("CommandDisabled")); return; }
            if (!HasPermission(player, PermissionBase + "help")) { SendError(player, Lang("NoPermission")); return; }

            var lines = new List<string>();
            // Build help entries for all enabled commands that the player has permission for.
            // We'll just list all commands (the help itself doesn't require per-command permission? We can still show all).
            // We'll show categories.

            // Gather available commands (only those enabled and that the player has permission for)
            var commands = new Dictionary<string, string>
            {
                { "maintenance", "Manage maintenance mode" },
                { "god", "Toggle god mode" },
                { "fly", "Toggle flight mode" },
                { "noclip", "Toggle noclip mode" },
                { "vanish", "Toggle vanish mode" },
                { "speed", "Set movement speed" },
                { "tp", "Teleport to player" },
                { "tphere", "Teleport player to you" },
                { "tploc", "Teleport to coordinates" },
                { "heal", "Fully heal yourself or target" },
                { "eat", "Restore food and water" },
                { "clear", "Clear inventory" },
                { "give", "Give item to player" },
                { "repair", "Repair active item" },
                { "repairall", "Repair all items" },
                { "kick", "Kick player" },
                { "list", "List online players" },
                { "ping", "Show your ping" },
                { "time", "Show current time" },
                { "day", "Set time to day" },
                { "night", "Set time to night" },
                { "rules", "Show server rules" },
                { "cwe help", "Show this help" },
                { "cwe reload", "Reload plugin" },
                { "cwe version", "Show version" }
            };

            foreach (var kv in commands)
            {
                string cmdName = kv.Key.Replace(" ", ""); // strip spaces for command name lookup
                // Check if command is enabled and player has permission (if not admin)
                if (!IsCommandEnabled(cmdName)) continue;
                string perm = PermissionBase + cmdName;
                if (!HasPermission(player, perm) && !IsAdmin(player)) continue; // skip if no permission
                // Allow help command always if they got this far
                lines.Add($"<color={_config.Settings.Colors.Highlight}>/{kv.Key}</color> - {kv.Value}");
            }

            if (lines.Count == 0)
            {
                SendInfo(player, Lang("HelpNoCommands"));
                return;
            }

            // Pagination
            int perPage = 8;
            int totalPages = Mathf.CeilToInt((float)lines.Count / perPage);
            page = Mathf.Clamp(page, 1, totalPages);
            int start = (page - 1) * perPage;
            int end = Mathf.Min(start + perPage, lines.Count);

            SendMessage(player, Lang("HelpHeader"), "Highlight");
            SendMessage(player, Lang("HelpPage", player.UserIDString, page, totalPages), "Info");
            for (int i = start; i < end; i++)
                SendInfo(player, lines[i]);
        }

        private void CmdReload(BasePlayer player, string cmd, string[] args)
        {
            if (!IsCommandEnabled("Reload")) { SendError(player, Lang("CommandDisabled")); return; }
            if (!HasPermission(player, PermissionBase + "reload")) { SendError(player, Lang("NoPermission")); return; }

            try
            {
                LoadConfig();
                LoadData();
                RegisterPermissions();
                // Reapply maintenance if enabled
                if (_config.Maintenance.Enabled) EnforceMaintenanceKick();
                SendSuccess(player, Lang("ReloadSuccess"));
                LogToConsole($"Reloaded by {player.displayName}");
            }
            catch (Exception ex)
            {
                SendError(player, Lang("ReloadFailed"));
                PrintError($"Reload error: {ex}");
            }
        }

        private void CmdVersion(BasePlayer player, string cmd, string[] args)
        {
            if (!IsCommandEnabled("Version")) { SendError(player, Lang("CommandDisabled")); return; }
            if (!HasPermission(player, PermissionBase + "version")) { SendError(player, Lang("NoPermission")); return; }

            SendInfo(player, Lang("VersionMessage", player.UserIDString, PluginVersion));
        }

        #endregion

        #region Hooks

        // Handle God mode (block damage)
        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null) return null;
            if (!_config.Commands.God.Enabled) return null;

            BasePlayer player = entity as BasePlayer;
            if (player == null) return null;

            var state = GetState(player);
            if (state.God)
            {
                // Block all damage
                return false;
            }
            return null;
        }

        // Maintain fly/noclip flags and speed on tick (to prevent server reset)
        private object OnPlayerTick(BasePlayer player, PlayerTick msg, bool wasStalled)
        {
            if (player == null || !player.IsConnected) return null;
            if (!_config.Settings.Enabled) return null;

            var state = GetState(player);

            // Fly
            if (state.Fly && !player.HasPlayerFlag(BasePlayer.PlayerFlags.Flying))
                player.SetPlayerFlag(BasePlayer.PlayerFlags.Flying, true);

            if (!state.Fly && player.HasPlayerFlag(BasePlayer.PlayerFlags.Flying))
                player.SetPlayerFlag(BasePlayer.PlayerFlags.Flying, false);

            // Noclip
            if (state.Noclip && !player.HasPlayerFlag(BasePlayer.PlayerFlags.Noclip))
                player.SetPlayerFlag(BasePlayer.PlayerFlags.Noclip, true);

            if (!state.Noclip && player.HasPlayerFlag(BasePlayer.PlayerFlags.Noclip))
                player.SetPlayerFlag(BasePlayer.PlayerFlags.Noclip, false);

            // Vanish (invisible)
            if (state.Vanish && !player.HasPlayerFlag(BasePlayer.PlayerFlags.Invisible))
                player.SetPlayerFlag(BasePlayer.PlayerFlags.Invisible, true);

            if (!state.Vanish && player.HasPlayerFlag(BasePlayer.PlayerFlags.Invisible))
                player.SetPlayerFlag(BasePlayer.PlayerFlags.Invisible, false);

            // Speed (apply if different from 1)
            float targetSpeed = state.Speed;
            if (Mathf.Abs(player.walkSpeed - targetSpeed) > 0.01f || Mathf.Abs(player.runSpeed - targetSpeed) > 0.01f)
            {
                player.walkSpeed = targetSpeed;
                player.runSpeed = targetSpeed;
            }

            return null;
        }

        // On player connected - if maintenance enabled, kick non-whitelisted
        private void OnPlayerConnected(BasePlayer player)
        {
            if (player == null) return;
            if (_config.Maintenance.Enabled)
            {
                if (!HasPermission(player, "cwessentials.maintenance.bypass"))
                {
                    NextTick(() => player.Kick(_config.Settings.MaintenanceMessage));
                }
            }
        }

        // On player disconnected - we might want to clear state? But we keep data for reconnect.
        // Nothing special.

        #endregion
    }
}