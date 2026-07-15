using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("CWEssentials", "whitecristafer", "1.3.7", ResourceId = 258869)]
    public class CWEssentials : RustPlugin
    {
        private const string PluginVersion = "1.3.7";
        private const string PermissionAdmin = "cwessentials.admin";
        private const string PermissionTargetOthers = "cwessentials.target.others";
        private const string PermissionBase = "cwessentials.";
        private const string DataFileName = "CWEssentials_Data";
        private const string DefaultChatPrefix = "<size=12><color=#66ccff><b>CWEssentials</b></color></size> |";
        private const ulong DefaultPluginIcon = 76561198209258869UL;
        private const string DefaultMaintenanceMessage = "Server is under maintenance. Please come back later.";

        private PluginConfig _config;
        private StoredData _data;
        private Timer _maintenanceTimer;
        private const string VanishBadgeUiName = "CWEssentials.VanishBadge";

        #region Configuration

        private class PluginConfig
        {
            [JsonProperty("ConfigVersion")]
            public int Version = 2;

            [JsonProperty("Settings")]
            public SettingsConfig Settings = new SettingsConfig();

            [JsonProperty("Commands")]
            public CommandsConfig Commands = new CommandsConfig();

            [JsonProperty("Maintenance")]
            public MaintenanceConfig Maintenance = new MaintenanceConfig();
        }

        private class SettingsConfig
        {
            [JsonProperty("Enabled")]
            public bool Enabled = true;

            [JsonProperty("ChatPrefix")]
            public string ChatPrefix = DefaultChatPrefix;

            [JsonProperty("PluginIcon")]
            public ulong PluginIcon = DefaultPluginIcon;

            [JsonProperty("Show Vanish Badge")]
            public bool ShowVanishBadge = true;

            [JsonProperty("Vanish Badge Text")]
            public string VanishBadgeText = "👻 VANISH";

            [JsonProperty("Vanish Badge Color")]
            public string VanishBadgeColor = "0.95 0.85 1 0.95";

            [JsonProperty("Vanish Badge Background")]
            public string VanishBadgeBackground = "0 0 0 0.35";

            [JsonProperty("Vanish Badge Anchor Min")]
            public string VanishBadgeAnchorMin = "0.40 0.92";

            [JsonProperty("Vanish Badge Anchor Max")]
            public string VanishBadgeAnchorMax = "0.60 0.97";

            [JsonProperty("MessageSize")]
            public int MessageSize = 14;

            [JsonProperty("TitleSize")]
            public int TitleSize = 16;

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
            [JsonProperty("PlayerInfo")] public CommandEntry PlayerInfo = new CommandEntry();
            [JsonProperty("PlayerInfoAll")] public CommandEntry PlayerInfoAll = new CommandEntry();
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
                EnsureRulesFolderExists();
                SaveConfig();
            }
            catch (Exception ex)
            {
                PrintWarning($"Configuration load error: {ex.Message}. A new default configuration will be created.");
                _config = new PluginConfig();
                EnsureRulesFolderExists();
                SaveConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(_config, true);

        private void NormalizeConfig()
        {
            bool migrated = false;

            if (_config.Version != 2)
            {
                _config.Version = 2;
                migrated = true;
            }

            if (_config.Settings == null) _config.Settings = new SettingsConfig();
            if (_config.Settings.Colors == null) _config.Settings.Colors = new ColorsConfig();
            if (_config.Commands == null) _config.Commands = new CommandsConfig();
            if (_config.Maintenance == null) _config.Maintenance = new MaintenanceConfig();
            if (_config.Maintenance.Whitelist == null) _config.Maintenance.Whitelist = new List<string>();

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

            _config.Settings.Colors.Info = NormalizeHex(_config.Settings.Colors.Info, "#aaddff");
            _config.Settings.Colors.Success = NormalizeHex(_config.Settings.Colors.Success, "#66ff66");
            _config.Settings.Colors.Warning = NormalizeHex(_config.Settings.Colors.Warning, "#ffaa00");
            _config.Settings.Colors.Error = NormalizeHex(_config.Settings.Colors.Error, "#ff6666");
            _config.Settings.Colors.Highlight = NormalizeHex(_config.Settings.Colors.Highlight, "#ffffff");

            if (migrated)
                PrintWarning("Configuration was migrated to the latest version.");
        }

        private string NormalizeHex(string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
                return fallback;

            value = value.Trim();
            if (!value.StartsWith("#"))
                value = "#" + value;

            return Regex.IsMatch(value, "^#(?:[0-9a-fA-F]{3}){1,2}$") ? value : fallback;
        }

        #endregion

        #region Data

        private class StoredData
        {
            public Dictionary<ulong, PlayerState> PlayerStates = new Dictionary<ulong, PlayerState>();
        }

        private class PlayerState
        {
            public bool God;
            public bool Fly;
            public bool Noclip;
            public bool Vanish;
            public bool MovementSynced;
            public float Speed = 1f;
        }

        private void LoadData()
        {
            try
            {
                _data = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(DataFileName);
            }
            catch
            {
                _data = null;
            }

            if (_data == null) _data = new StoredData();
            if (_data.PlayerStates == null) _data.PlayerStates = new Dictionary<ulong, PlayerState>();
        }

        private void SaveData()
        {
            if (_data == null)
                _data = new StoredData();

            Interface.Oxide.DataFileSystem.WriteObject(DataFileName, _data);
        }

        private PlayerState GetState(BasePlayer player)
        {
            if (player == null)
                return null;

            if (!_data.PlayerStates.TryGetValue(player.userID, out var state))
            {
                state = new PlayerState();
                _data.PlayerStates[player.userID] = state;
            }

            return state;
        }

        #endregion

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoPermission"] = "You don't have permission to use this command.",
                ["NoTargetPermission"] = "You don't have permission to target other players.",
                ["CommandDisabled"] = "This command is disabled in the configuration.",
                ["PluginDisabled"] = "The plugin is currently disabled.",
                ["PlayerNotFound"] = "Player not found.",
                ["InvalidSyntax"] = "Invalid syntax. Use: {0}",
                ["InvalidNumber"] = "Invalid number.",
                ["InvalidCoordinates"] = "Invalid coordinates. Use: /tploc <x> <y> <z>",
                ["InvalidSpeed"] = "Speed must be between 1 and 10.",
                ["TimeSystemUnavailable"] = "Time system is unavailable on this map.",
                ["NoRulesDefined"] = "No rules defined.",
                ["RulesReadFailed"] = "Failed to read the rules file.",
                ["MaintenanceOn"] = "Maintenance mode enabled. All non-whitelisted players have been kicked.",
                ["MaintenanceOff"] = "Maintenance mode disabled.",
                ["MaintenanceAlreadyOn"] = "Maintenance mode is already enabled.",
                ["MaintenanceAlreadyOff"] = "Maintenance mode is already disabled.",
                ["MaintenanceAdded"] = "Player {0} added to the whitelist.",
                ["MaintenanceRemoved"] = "Player {0} removed from the whitelist.",
                ["MaintenanceNotInWhitelist"] = "Player {0} is not in the whitelist.",
                ["MaintenanceAlreadyInWhitelist"] = "Player {0} is already in the whitelist.",
                ["MaintenanceListHeader"] = "Maintenance whitelist ({0} entries):",
                ["MaintenanceListEntry"] = "- {0}",
                ["MaintenanceListEmpty"] = "The whitelist is empty.",
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
                ["VanishOn"] = "👻 Vanish mode enabled.",
                ["VanishOff"] = "👻 Vanish mode disabled.",
                ["VanishOnOther"] = "👻 Vanish mode enabled for {0}.",
                ["VanishOffOther"] = "👻 Vanish mode disabled for {0}.",
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
                ["GiveDropped"] = "Inventory is full. The item was dropped on the ground near {0}.",
                ["RepairDone"] = "Item repaired.",
                ["RepairOther"] = "Repaired item of {0}.",
                ["RepairAllDone"] = "All items repaired.",
                ["RepairAllOther"] = "All items repaired for {0}.",
                ["KickSuccess"] = "Kicked {0} with reason: {1}",
                ["KickSelf"] = "You cannot kick yourself.",
                ["ListHeader"] = "Players online ({0}):",
                ["ListEntry"] = "{0} [SteamID: {1}]",
                ["PingResult"] = "Your ping: {0} ms.",
                ["PingOtherResult"] = "{0}'s ping: {1} ms.",
                ["TimeResult"] = "Current time: {0}.",
                ["DaySet"] = "Time set to day.",
                ["NightSet"] = "Time set to night.",
                ["RulesHeader"] = "Server rules:",
                ["RulesLine"] = "{0}",
                ["RulesNotFound"] = "Rules file not found.",
                ["HelpHeader"] = "CWEssentials Help",
                ["HelpPage"] = "Page {0}/{1}",
                ["HelpLine"] = "/{0} - {1}",
                ["HelpNoCommands"] = "No commands are available.",
                ["ReloadSuccess"] = "CWEssentials reloaded successfully.",
                ["ReloadFailed"] = "Reload failed. Check the server logs.",
                ["VersionMessage"] = "CWEssentials v{0} by whitecristafer.",
                ["UnknownCommand"] = "Unknown command. Use /cwe help.",
                ["PlayerInfoBriefHeader"] = "--- Player Info: {0} ---",
                ["PlayerInfoDetailedHeader"] = "=== Comprehensive Diagnostics: {0} ===",
                ["MustSpecifyPlayer"] = "You must specify a player name or SteamID when executing from server console."
            }, this, "en");
        }

        #endregion

        #region Permissions

        private void RegisterPermissions()
        {
            permission.RegisterPermission(PermissionAdmin, this);
            permission.RegisterPermission(PermissionTargetOthers, this);

            string[] perms =
            {
                "maintenance", "maintenance.bypass", "god", "fly", "noclip", "vanish", "speed",
                "tp", "tphere", "tploc", "heal", "eat", "clear", "give", "repair", "repairall",
                "kick", "list", "ping", "time", "time.set", "rules", "help", "reload", "version",
                "playerinfo", "playerinfoall"
            };

            foreach (string perm in perms)
                permission.RegisterPermission(PermissionBase + perm, this);
        }

        private bool IsServerConsole(CommandContext context)
        {
            return context.Arg != null && context.Arg.Connection == null && context.Player == null;
        }

        private bool HasPermission(BasePlayer player, string permissionName)
        {
            if (player == null)
                return false;

            if (player.IsAdmin)
                return true;

            return permission.UserHasPermission(player.UserIDString, permissionName);
        }

        private bool HasPermission(CommandContext context, string permissionName)
        {
            if (IsServerConsole(context))
            {
                return true;
            }

            if (context.Player == null)
            {
                return false;
            }

            return HasPermission(context.Player, permissionName);
        }

        private bool IsAdmin(BasePlayer player)
        {
            return player != null && (player.IsAdmin || permission.UserHasPermission(player.UserIDString, PermissionAdmin));
        }

        private bool CanTargetOthers(BasePlayer player)
        {
            return IsAdmin(player) || HasPermission(player, PermissionTargetOthers);
        }

        #endregion

        #region Helpers

        private bool IsPluginEnabled()
        {
            return _config?.Settings?.Enabled ?? true;
        }

        private bool IsCommandEnabled(string commandName)
        {
            if (_config?.Commands == null || string.IsNullOrWhiteSpace(commandName))
                return false;

            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;

            PropertyInfo property = typeof(CommandsConfig).GetProperty(commandName, flags);
            if (property != null && property.GetValue(_config.Commands) is CommandEntry propertyEntry)
                return propertyEntry.Enabled;

            FieldInfo field = typeof(CommandsConfig).GetField(commandName, flags);
            if (field != null && field.GetValue(_config.Commands) is CommandEntry fieldEntry)
                return fieldEntry.Enabled;

            return false;
        }

        private string GetColor(string key)
        {
            var colors = _config?.Settings?.Colors ?? new ColorsConfig();

            switch (key)
            {
                case "Success":
                    return colors.Success;
                case "Warning":
                    return colors.Warning;
                case "Error":
                    return colors.Error;
                case "Highlight":
                    return colors.Highlight;
                default:
                    return colors.Info;
            }
        }

        private string StripRichText(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return Regex.Replace(value, "<[^>]*>", string.Empty);
        }

        private void SyncMovementMode(BasePlayer player, PlayerState state)
        {
            if (player == null || state == null || !player.IsConnected)
                return;

            if (!state.Fly && !state.Noclip)
            {
                state.MovementSynced = false;
                return;
            }

            if (state.MovementSynced)
                return;

            rust.RunClientCommand(player, "noclip");
            state.MovementSynced = true;
        }

        private bool IsVanishEnabled(BasePlayer player)
        {
            return player != null && player._limitedNetworking;
        }

        private bool HasSavedVanishState(BasePlayer player)
        {
            if (player == null || _data?.PlayerStates == null)
                return false;

            return _data.PlayerStates.TryGetValue(player.userID, out PlayerState state) && state?.Vanish == true;
        }

        private void UpdateVanishBadge(BasePlayer player, bool enabled)
        {
            if (player == null)
                return;

            CuiHelper.DestroyUi(player, VanishBadgeUiName);

            if (!enabled || _config?.Settings?.ShowVanishBadge != true)
                return;

            string text = string.IsNullOrWhiteSpace(_config.Settings.VanishBadgeText) ? "👻 VANISH" : _config.Settings.VanishBadgeText;
            string background = string.IsNullOrWhiteSpace(_config.Settings.VanishBadgeBackground) ? "0 0 0 0.35" : _config.Settings.VanishBadgeBackground;
            string color = string.IsNullOrWhiteSpace(_config.Settings.VanishBadgeColor) ? "0.95 0.85 1 0.95" : _config.Settings.VanishBadgeColor;
            string anchorMin = string.IsNullOrWhiteSpace(_config.Settings.VanishBadgeAnchorMin) ? "0.40 0.92" : _config.Settings.VanishBadgeAnchorMin;
            string anchorMax = string.IsNullOrWhiteSpace(_config.Settings.VanishBadgeAnchorMax) ? "0.60 0.97" : _config.Settings.VanishBadgeAnchorMax;

            var container = new CuiElementContainer();
            var panel = container.Add(new CuiPanel
            {
                Image = { Color = background },
                RectTransform = { AnchorMin = anchorMin, AnchorMax = anchorMax },
                CursorEnabled = false
            }, "Hud", VanishBadgeUiName);

            container.Add(new CuiLabel
            {
                Text =
                {
                    Text = text,
                    FontSize = 14,
                    Align = TextAnchor.MiddleCenter,
                    Color = color
                },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, panel);

            CuiHelper.AddUi(player, container);
        }

        private void SyncVanishState(BasePlayer player, bool enabled)
        {
            if (player == null)
                return;

            if (enabled)
            {
                if (player._limitedNetworking && player.limitNetworking)
                {
                    UpdateVanishBadge(player, true);
                    return;
                }

                BaseEntity.Query.Server.RemovePlayer(player);

                player._limitedNetworking = true;
                player.syncPosition = false;
                player.limitNetworking = true;
                player.isInvisible = true;
                player.DisablePlayerCollider();
                player.UpdateNetworkGroup();
                player.SendNetworkUpdateImmediate();
                UpdateVanishBadge(player, true);
                return;
            }

            if (!player._limitedNetworking && !player.limitNetworking && !player.isInvisible)
            {
                UpdateVanishBadge(player, false);
                return;
            }

            player._limitedNetworking = false;
            player.syncPosition = true;
            player.limitNetworking = false;
            player.isInvisible = false;

            BaseEntity.Query.Server.AddPlayer(player);
            player.EnablePlayerCollider();
            player.UpdateNetworkGroup();
            player.SendNetworkUpdateImmediate();
            UpdateVanishBadge(player, false);
        }

        private string FormatChatMessage(string message, string colorKey = "Info", bool useTitleSize = false)
        {
            var settings = _config?.Settings ?? new SettingsConfig();
            string prefix = settings.ChatPrefix ?? DefaultChatPrefix;
            string color = GetColor(colorKey);
            int size = useTitleSize ? settings.TitleSize : settings.MessageSize;

            return $"{prefix} <size={size}><color={color}>{message}</color></size>";
        }

        private void SendMessage(BasePlayer player, string message)
        {
            if (player == null || string.IsNullOrEmpty(message))
                return;

            ulong icon = _config?.Settings?.PluginIcon ?? DefaultPluginIcon;
            string final = message;
            player.SendConsoleCommand("chat.add", 2, icon, final);
        }

        private void Reply(BasePlayer player, string message, string colorKey = "Info", bool useTitleSize = false)
        {
            if (player == null || string.IsNullOrEmpty(message))
                return;

            SendMessage(player, FormatChatMessage(message, colorKey, useTitleSize));
        }

        private void Reply(ConsoleSystem.Arg arg, string message)
        {
            if (arg == null || string.IsNullOrEmpty(message))
                return;

            arg.ReplyWith(StripRichText(message));
        }

        private void Reply(CommandContext context, string message, string colorKey = "Info", bool useTitleSize = false)
        {
            if (context.Player != null)
            {
                Reply(context.Player, message, colorKey, useTitleSize);
                return;
            }

            if (context.Arg != null)
            {
                Reply(context.Arg, FormatChatMessage(message, colorKey, useTitleSize));
                return;
            }

            Puts(StripRichText(message));
        }

        private void LogToConsole(string message)
        {
            Puts($"[CWEssentials] {StripRichText(message)}");
        }

        private BasePlayer FindPlayer(string ident)
        {
            if (string.IsNullOrWhiteSpace(ident))
                return null;

            if (ulong.TryParse(ident, NumberStyles.None, CultureInfo.InvariantCulture, out ulong userId))
            {
                BasePlayer byId = BasePlayer.activePlayerList.FirstOrDefault(p => p != null && p.userID == userId);
                if (byId != null)
                    return byId;

                byId = BasePlayer.sleepingPlayerList.FirstOrDefault(p => p != null && p.userID == userId);
                if (byId != null)
                    return byId;
            }

            IEnumerable<BasePlayer> allPlayers = BasePlayer.activePlayerList.Concat(BasePlayer.sleepingPlayerList).Where(p => p != null);
            BasePlayer found = allPlayers.FirstOrDefault(p => p.displayName != null && p.displayName.IndexOf(ident, StringComparison.OrdinalIgnoreCase) >= 0);
            if (found != null)
                return found;

            return allPlayers.FirstOrDefault(p => string.Equals(p.UserIDString, ident, StringComparison.OrdinalIgnoreCase));
        }

        private void TeleportPlayer(BasePlayer player, Vector3 position, Quaternion? rotation = null)
        {
            if (player == null || !player.IsConnected)
                return;

            Effect.server.Run("assets/prefabs/misc/transferable/effects/teleport.prefab", player.transform.position, Vector3.up);
            player.Teleport(position);

            if (rotation.HasValue)
                player.eyes.rotation = rotation.Value;

            player.SendNetworkUpdateImmediate();
            Effect.server.Run("assets/prefabs/misc/transferable/effects/teleport.prefab", position, Vector3.up);
        }

        private string GetRulesFilePath()
        {
            string folder = Path.Combine(Interface.Oxide.ConfigDirectory, Title);
            return Path.Combine(folder, _config?.Settings?.RulesFile ?? "rules.txt");
        }

        private void EnsureRulesFolderExists()
        {
            string folder = Path.Combine(Interface.Oxide.ConfigDirectory, Title);
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
        }

        private bool HasMaintenanceBypass(BasePlayer player)
        {
            if (player == null)
                return true;

            if (player.IsAdmin || IsAdmin(player) || permission.UserHasPermission(player.UserIDString, PermissionBase + "maintenance.bypass"))
                return true;

            return _config?.Maintenance?.Whitelist != null && _config.Maintenance.Whitelist.Contains(player.UserIDString);
        }

        private void KickPlayersForMaintenance()
        {
            if (!IsPluginEnabled() || _config?.Maintenance?.Enabled != true)
                return;

            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (player == null)
                    continue;

                KickPlayerForMaintenance(player);
            }
        }

        private void KickPlayerForMaintenance(BasePlayer player)
        {
            if (player == null || !IsPluginEnabled() || _config?.Maintenance?.Enabled != true)
                return;

            if (HasMaintenanceBypass(player))
                return;

            player.Kick(_config?.Settings?.MaintenanceMessage ?? DefaultMaintenanceMessage);
        }

        private void RefreshMaintenanceTimer()
        {
            _maintenanceTimer?.Destroy();
            _maintenanceTimer = null;

            if (!IsPluginEnabled() || _config?.Maintenance?.Enabled != true)
                return;

            _maintenanceTimer = timer.Every(30f, KickPlayersForMaintenance);
        }

        private void EnsurePlayerState(BasePlayer player)
        {
            if (player == null || _data == null)
                return;

            GetState(player);
        }

        private bool TryParseFloat(string value, out float result)
        {
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
        }

        private bool TryParseInt(string value, out int result)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
        }

        private bool TryGetTargetAndValue(CommandContext context, string[] args, out BasePlayer target, out string value, int valueIndex = 0)
        {
            target = context.Player;
            value = null;

            if (args == null || args.Length == 0)
                return false;

            if (args.Length > valueIndex)
                value = args[valueIndex];

            if (args.Length > 1)
            {
                BasePlayer possibleTarget = FindPlayer(args[0]);
                if (possibleTarget != null && possibleTarget != context.Player)
                {
                    if (context.Player != null && !CanTargetOthers(context.Player))
                    {
                        Reply(context, Lang("NoTargetPermission", context.UserId), "Error");
                        return false;
                    }

                    target = possibleTarget;
                    value = args.Length > valueIndex + 1 ? args[valueIndex + 1] : null;
                }
            }

            return true;
        }

        private string Lang(string key, string playerId = null, params object[] args)
        {
            string message = lang.GetMessage(key, this, playerId);
            return args.Length > 0 ? string.Format(message, args) : message;
        }

        private void GuardDisabledCommand(CommandContext context, string commandName)
        {
            if (!IsPluginEnabled() || !IsCommandEnabled(commandName))
                throw new InvalidOperationException("DisabledCommand");
        }

        private bool IsCommandAllowed(CommandContext context, string commandName, string permissionName)
        {
            if (!IsPluginEnabled() || !IsCommandEnabled(commandName))
                return false;

            return HasPermission(context, permissionName);
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
            if (IsPluginEnabled() && _config?.Maintenance?.Enabled == true)
                KickPlayersForMaintenance();

            RefreshMaintenanceTimer();
            PrintBanner();
        }

        private void OnPlayerInit(BasePlayer player)
        {
            if (player == null || _data?.PlayerStates == null)
                return;

            if (_data.PlayerStates.TryGetValue(player.userID, out PlayerState state))
            {
                state.MovementSynced = false;

                if (state.Vanish)
                {
                    timer.Once(0.2f, () =>
                    {
                        if (player != null && player.IsConnected)
                            SyncVanishState(player, true);
                    });
                }
                else if (player._limitedNetworking || player.limitNetworking || player.isInvisible)
                {
                    timer.Once(0.2f, () =>
                    {
                        if (player != null && player.IsConnected)
                            SyncVanishState(player, false);
                    });
                }
            }

            if (IsPluginEnabled() && _config?.Maintenance?.Enabled == true)
            {
                timer.Once(1f, () =>
                {
                    if (player != null && player.IsConnected)
                        KickPlayerForMaintenance(player);
                });
            }
        }

        private void Unload()
        {
            _maintenanceTimer?.Destroy();
            _maintenanceTimer = null;

            try
            {
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                {
                    if (player == null)
                        continue;

                    if (player._limitedNetworking || player.limitNetworking || player.isInvisible)
                        SyncVanishState(player, false);

                    CuiHelper.DestroyUi(player, VanishBadgeUiName);
                    player.SendNetworkUpdateImmediate();
                }
            }
            catch (Exception ex)
            {
                PrintWarning($"Unload cleanup warning: {ex.Message}");
            }

            SaveData();
        }

        private void PrintBanner()
        {
            Puts("===============================================");
            Puts($"CWEssentials v{PluginVersion} loaded.");
            Puts($"Plugin enabled: {(_config?.Settings?.Enabled == true ? "ON" : "OFF")}");
            Puts($"Maintenance: {(_config?.Maintenance?.Enabled == true ? "ON" : "OFF")}");
            Puts($"Whitelist count: {_config?.Maintenance?.Whitelist?.Count ?? 0}");
            Puts("===============================================");
        }

        #endregion

        #region Command Context

        private struct CommandContext
        {
            public CommandContext(CWEssentials plugin, BasePlayer player, ConsoleSystem.Arg arg = null)
            {
                Plugin = plugin;
                Player = player;
                Arg = arg;
            }

            public CWEssentials Plugin { get; }
            public BasePlayer Player { get; }
            public ConsoleSystem.Arg Arg { get; }

            public string UserId => Player?.UserIDString ?? "0";
            public string Name => Player?.displayName ?? "Console";
        }

        #endregion

        private string[] GetConsoleArgs(ConsoleSystem.Arg arg)
        {
            if (arg?.Args == null || arg.Args.Length == 0)
                return Array.Empty<string>();

            string[] result = new string[arg.Args.Length];
            for (int i = 0; i < arg.Args.Length; i++)
                result[i] = arg.Args[i].ToString();

            return result;
        }

        #region Commands

        [ChatCommand("maintenance")]
        private void CmdMaintenance(BasePlayer player, string cmd, string[] args) => HandleMaintenance(new CommandContext(this, player), args);

        [ConsoleCommand("maintenance")]
        private void CCmdMaintenance(ConsoleSystem.Arg arg) => HandleMaintenance(new CommandContext(this, arg?.Player(), arg), GetConsoleArgs(arg));

        private void HandleMaintenance(CommandContext context, string[] args)
        {
            if (!IsPluginEnabled() || !IsCommandEnabled("Maintenance"))
                return;

            if (!HasPermission(context, PermissionBase + "maintenance"))
            {
                Reply(context, Lang("NoPermission", context.UserId), "Error");
                return;
            }

            if (args == null) args = Array.Empty<string>();

            if (args.Length == 0)
            {
                bool newState = !_config.Maintenance.Enabled;

                if (newState)
                {
                    _config.Maintenance.Enabled = true;
                    SaveConfig();
                    RefreshMaintenanceTimer();
                    KickPlayersForMaintenance();
                    Reply(context, Lang("MaintenanceOn", context.UserId), "Success");
                    LogToConsole($"Maintenance mode enabled by {context.Name}");
                }
                else
                {
                    _config.Maintenance.Enabled = false;
                    SaveConfig();
                    RefreshMaintenanceTimer();
                    Reply(context, Lang("MaintenanceOff", context.UserId), "Success");
                    LogToConsole($"Maintenance mode disabled by {context.Name}");
                }

                return;
            }

            switch (args[0].ToLowerInvariant())
            {
                case "add":
                {
                    if (args.Length < 2)
                    {
                        Reply(context, Lang("InvalidSyntax", context.UserId, "/maintenance add <name|id>"), "Error");
                        return;
                    }

                    BasePlayer target = FindPlayer(args[1]);
                    string id = target != null ? target.UserIDString : null;
                    string label = target != null ? target.displayName : args[1];

                    if (id == null)
                    {
                        if (!ulong.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out ulong parsedId))
                        {
                            Reply(context, Lang("PlayerNotFound", context.UserId), "Error");
                            return;
                        }

                        id = parsedId.ToString(CultureInfo.InvariantCulture);
                    }

                    if (_config.Maintenance.Whitelist.Contains(id))
                    {
                        Reply(context, Lang("MaintenanceAlreadyInWhitelist", context.UserId, label), "Warning");
                        return;
                    }

                    _config.Maintenance.Whitelist.Add(id);
                    SaveConfig();
                    Reply(context, Lang("MaintenanceAdded", context.UserId, label), "Success");
                    LogToConsole($"Added {label} ({id}) to the maintenance whitelist by {context.Name}");
                    return;
                }

                case "remove":
                {
                    if (args.Length < 2)
                    {
                        Reply(context, Lang("InvalidSyntax", context.UserId, "/maintenance remove <name|id>"), "Error");
                        return;
                    }

                    BasePlayer target = FindPlayer(args[1]);
                    string id = target != null ? target.UserIDString : null;
                    string label = target != null ? target.displayName : args[1];

                    if (id == null)
                    {
                        if (!ulong.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out ulong parsedId))
                        {
                            Reply(context, Lang("PlayerNotFound", context.UserId), "Error");
                            return;
                        }

                        id = parsedId.ToString(CultureInfo.InvariantCulture);
                    }

                    if (!_config.Maintenance.Whitelist.Contains(id))
                    {
                        Reply(context, Lang("MaintenanceNotInWhitelist", context.UserId, label), "Warning");
                        return;
                    }

                    _config.Maintenance.Whitelist.Remove(id);
                    SaveConfig();
                    Reply(context, Lang("MaintenanceRemoved", context.UserId, label), "Success");
                    LogToConsole($"Removed {label} ({id}) from the maintenance whitelist by {context.Name}");
                    return;
                }

                case "list":
                {
                    List<string> list = _config.Maintenance.Whitelist ?? new List<string>();
                    if (list.Count == 0)
                    {
                        Reply(context, Lang("MaintenanceListEmpty", context.UserId), "Info");
                        return;
                    }

                    Reply(context, Lang("MaintenanceListHeader", context.UserId, list.Count), "Highlight", true);
                    foreach (string entry in list)
                        Reply(context, Lang("MaintenanceListEntry", context.UserId, entry), "Info");
                    return;
                }

                case "status":
                {
                    string status = _config.Maintenance.Enabled ? Lang("MaintenanceStatusOn", context.UserId) : Lang("MaintenanceStatusOff", context.UserId);
                    Reply(context, Lang("MaintenanceStatus", context.UserId, status), "Info");
                    return;
                }

                default:
                    Reply(context, Lang("UnknownCommand", context.UserId), "Error");
                    return;
            }
        }

        [ChatCommand("god")]
        private void CmdGod(BasePlayer player, string cmd, string[] args) => HandleToggleState(new CommandContext(this, player), args, "God", "god", state => { state.God = !state.God; }, state => state.God, "GodOn", "GodOff", "GodOnOther", "GodOffOther");

        [ConsoleCommand("god")]
        private void CCmdGod(ConsoleSystem.Arg arg) => HandleToggleState(new CommandContext(this, arg?.Player(), arg), GetConsoleArgs(arg), "God", "god", state => { state.God = !state.God; }, state => state.God, "GodOn", "GodOff", "GodOnOther", "GodOffOther");

        [ChatCommand("fly")]
        private void CmdFly(BasePlayer player, string cmd, string[] args) => HandleMovementMode(new CommandContext(this, player), args, "Fly", "fly", true);

        [ConsoleCommand("fly")]
        private void CCmdFly(ConsoleSystem.Arg arg) => HandleMovementMode(new CommandContext(this, arg?.Player(), arg), GetConsoleArgs(arg), "Fly", "fly", true);

        [ChatCommand("noclip")]
        private void CmdNoclip(BasePlayer player, string cmd, string[] args) => HandleMovementMode(new CommandContext(this, player), args, "Noclip", "noclip", false);

        [ConsoleCommand("noclip")]
        private void CCmdNoclip(ConsoleSystem.Arg arg) => HandleMovementMode(new CommandContext(this, arg?.Player(), arg), GetConsoleArgs(arg), "Noclip", "noclip", false);

        [ChatCommand("vanish")]
        private void CmdVanish(BasePlayer player, string cmd, string[] args) => HandleToggleState(new CommandContext(this, player), args, "Vanish", "vanish", state => { state.Vanish = !state.Vanish; }, state => state.Vanish, "VanishOn", "VanishOff", "VanishOnOther", "VanishOffOther", applyFlags: (target, state) => SyncVanishState(target, state));

        [ConsoleCommand("vanish")]
        private void CCmdVanish(ConsoleSystem.Arg arg) => HandleToggleState(new CommandContext(this, arg?.Player(), arg), GetConsoleArgs(arg), "Vanish", "vanish", state => { state.Vanish = !state.Vanish; }, state => state.Vanish, "VanishOn", "VanishOff", "VanishOnOther", "VanishOffOther", applyFlags: (target, state) => SyncVanishState(target, state));

        private delegate void StateMutation(PlayerState state);
        private delegate bool StateGetter(PlayerState state);
        private delegate void StateFlagApplier(BasePlayer target, bool enabled);

        private void HandleMovementMode(CommandContext context, string[] args, string configName, string permissionSuffix, bool isFlyCommand)
        {
            if (!IsPluginEnabled() || !IsCommandEnabled(configName))
                return;

            if (!HasPermission(context, PermissionBase + permissionSuffix))
            {
                Reply(context, Lang("NoPermission", context.UserId), "Error");
                return;
            }

            if (args == null) args = Array.Empty<string>();

            BasePlayer target = context.Player;
            if (args.Length > 0)
            {
                BasePlayer possibleTarget = FindPlayer(args[0]);
                if (possibleTarget != null && possibleTarget != context.Player)
                {
                    if (context.Player != null && !CanTargetOthers(context.Player))
                    {
                        Reply(context, Lang("NoTargetPermission", context.UserId), "Error");
                        return;
                    }

                    target = possibleTarget;
                }
            }

            if (target == null)
            {
                Reply(context, Lang("PlayerNotFound", context.UserId), "Error");
                return;
            }

            PlayerState state = GetState(target);
            bool previousEnabled = state.Fly || state.Noclip;

            if (isFlyCommand)
                state.Fly = !state.Fly;
            else
                state.Noclip = !state.Noclip;

            bool currentEnabled = state.Fly || state.Noclip;

            if (target.IsConnected && previousEnabled != currentEnabled)
            {
                rust.RunClientCommand(target, "noclip");
                state.MovementSynced = true;
            }
            else if (!currentEnabled)
            {
                state.MovementSynced = false;
            }

            SaveData();

            bool enabled = isFlyCommand ? state.Fly : state.Noclip;
            if (target == context.Player)
                Reply(context, Lang(enabled ? (isFlyCommand ? "FlyOn" : "NoclipOn") : (isFlyCommand ? "FlyOff" : "NoclipOff"), context.UserId), enabled ? "Success" : "Info");
            else
                Reply(context, Lang(enabled ? (isFlyCommand ? "FlyOnOther" : "NoclipOnOther") : (isFlyCommand ? "FlyOffOther" : "NoclipOffOther"), context.UserId, target.displayName), enabled ? "Success" : "Info");

            LogToConsole($"{configName} {(enabled ? "ON" : "OFF")} for {target.displayName} by {context.Name}");
        }

        private void HandleToggleState(CommandContext context, string[] args, string configName, string permissionSuffix, StateMutation mutate, StateGetter getter, string selfOnKey, string selfOffKey, string otherOnKey, string otherOffKey, StateFlagApplier applyFlags = null)
        {
            if (!IsPluginEnabled() || !IsCommandEnabled(configName))
                return;

            if (!HasPermission(context, PermissionBase + permissionSuffix))
            {
                Reply(context, Lang("NoPermission", context.UserId), "Error");
                return;
            }

            if (args == null) args = Array.Empty<string>();

            BasePlayer target = context.Player;
            if (args.Length > 0)
            {
                BasePlayer possibleTarget = FindPlayer(args[0]);
                if (possibleTarget != null && possibleTarget != context.Player)
                {
                    if (context.Player != null && !CanTargetOthers(context.Player))
                    {
                        Reply(context, Lang("NoTargetPermission", context.UserId), "Error");
                        return;
                    }

                    target = possibleTarget;
                }
            }

            if (target == null)
            {
                Reply(context, Lang("PlayerNotFound", context.UserId), "Error");
                return;
            }

            PlayerState state = GetState(target);
            mutate(state);

            bool enabled = getter(state);
            if (applyFlags != null)
                applyFlags(target, enabled);

            if (configName == "Vanish")
                target.SendNetworkUpdateImmediate();

            if (configName == "Fly" || configName == "Noclip" || configName == "Vanish")
                target.SendNetworkUpdateImmediate();

            SaveData();

            if (target == context.Player)
                Reply(context, Lang(enabled ? selfOnKey : selfOffKey, context.UserId), enabled ? "Success" : "Info");
            else
                Reply(context, Lang(enabled ? otherOnKey : otherOffKey, context.UserId, target.displayName), enabled ? "Success" : "Info");

            LogToConsole($"{configName} {(enabled ? "ON" : "OFF")} for {target.displayName} by {context.Name}");
        }

        [ChatCommand("speed")]
        private void CmdSpeed(BasePlayer player, string cmd, string[] args) => HandleSpeed(new CommandContext(this, player), args);

        [ConsoleCommand("speed")]
        private void CCmdSpeed(ConsoleSystem.Arg arg) => HandleSpeed(new CommandContext(this, arg?.Player(), arg), GetConsoleArgs(arg));

        private void HandleSpeed(CommandContext context, string[] args)
        {
            if (!IsPluginEnabled() || !IsCommandEnabled("Speed"))
                return;

            if (!HasPermission(context, PermissionBase + "speed"))
            {
                Reply(context, Lang("NoPermission", context.UserId), "Error");
                return;
            }

            if (args == null) args = Array.Empty<string>();
            if (args.Length == 0)
            {
                Reply(context, Lang("InvalidSyntax", context.UserId, "/speed <1-10> or /speed <target> <1-10>"), "Error");
                return;
            }

            BasePlayer target = context.Player;
            int valueIndex = 0;

            if (args.Length >= 2)
            {
                BasePlayer possibleTarget = FindPlayer(args[0]);
                if (possibleTarget != null && possibleTarget != context.Player)
                {
                    if (context.Player != null && !CanTargetOthers(context.Player))
                    {
                        Reply(context, Lang("NoTargetPermission", context.UserId), "Error");
                        return;
                    }

                    target = possibleTarget;
                    valueIndex = 1;
                }
            }

            if (target == null)
            {
                Reply(context, Lang("PlayerNotFound", context.UserId), "Error");
                return;
            }

            float speed;
            bool reset = false;

            if (valueIndex >= args.Length)
            {
                reset = true;
                speed = 1f;
            }
            else
            {
                reset = string.Equals(args[valueIndex], "reset", StringComparison.OrdinalIgnoreCase);

                if (!reset)
                {
                    if (!TryParseFloat(args[valueIndex], out speed))
                    {
                        Reply(context, Lang("InvalidNumber", context.UserId), "Error");
                        return;
                    }

                    speed = Mathf.Clamp(speed, 1f, 10f);
                }
                else
                {
                    speed = 1f;
                }
            }

            PlayerState state = GetState(target);
            state.Speed = speed;
            // Rust 2026 no longer exposes writable player walk/run speed members here.
            // The chosen value is retained in plugin state so the command remains consistent.
            target.SendNetworkUpdateImmediate();
            SaveData();

            if (reset)
            {
                if (target == context.Player)
                    Reply(context, Lang("SpeedReset", context.UserId), "Success");
                else
                    Reply(context, Lang("SpeedResetOther", context.UserId, target.displayName), "Success");
            }
            else
            {
                if (target == context.Player)
                    Reply(context, Lang("SpeedSet", context.UserId, speed.ToString(CultureInfo.InvariantCulture)), "Success");
                else
                    Reply(context, Lang("SpeedSetOther", context.UserId, speed.ToString(CultureInfo.InvariantCulture), target.displayName), "Success");
            }

            LogToConsole($"Speed {(reset ? "reset" : speed.ToString(CultureInfo.InvariantCulture))} for {target.displayName} by {context.Name}");
        }

        [ChatCommand("tp")]
        private void CmdTp(BasePlayer player, string cmd, string[] args) => HandleTp(new CommandContext(this, player), args, false);

        [ConsoleCommand("tp")]
        private void CCmdTp(ConsoleSystem.Arg arg) => HandleTp(new CommandContext(this, arg?.Player(), arg), GetConsoleArgs(arg), false);

        [ChatCommand("tphere")]
        private void CmdTpHere(BasePlayer player, string cmd, string[] args) => HandleTp(new CommandContext(this, player), args, true);

        [ConsoleCommand("tphere")]
        private void CCmdTpHere(ConsoleSystem.Arg arg) => HandleTp(new CommandContext(this, arg?.Player(), arg), GetConsoleArgs(arg), true);

        [ChatCommand("tploc")]
        private void CmdTpLoc(BasePlayer player, string cmd, string[] args) => HandleTpLoc(new CommandContext(this, player), args);

        [ConsoleCommand("tploc")]
        private void CCmdTpLoc(ConsoleSystem.Arg arg) => HandleTpLoc(new CommandContext(this, arg?.Player(), arg), GetConsoleArgs(arg));

        private void HandleTp(CommandContext context, string[] args, bool here)
        {
            if (!IsPluginEnabled() || !IsCommandEnabled(here ? "TPHere" : "TP"))
                return;

            if (!HasPermission(context, PermissionBase + (here ? "tphere" : "tp")))
            {
                Reply(context, Lang("NoPermission", context.UserId), "Error");
                return;
            }

            if (args == null) args = Array.Empty<string>();
            if (args.Length < 1)
            {
                Reply(context, Lang("InvalidSyntax", context.UserId, here ? "/tphere <player>" : "/tp <player>"), "Error");
                return;
            }

            BasePlayer target = FindPlayer(args[0]);
            if (target == null)
            {
                Reply(context, Lang("PlayerNotFound", context.UserId), "Error");
                return;
            }

            if (!here)
            {
                if (context.Player == null)
                {
                    Reply(context, Lang("InvalidSyntax", context.UserId, "/tp <player>"), "Error");
                    return;
                }

                if (target == context.Player)
                {
                    Reply(context, "You cannot teleport to yourself.", "Warning");
                    return;
                }

                TeleportPlayer(context.Player, target.transform.position, target.eyes?.rotation);
                Reply(context, Lang("TeleportToTarget", context.UserId, target.displayName), "Success");
                LogToConsole($"{context.Name} teleported to {target.displayName}");
                return;
            }

            if (target == context.Player)
            {
                Reply(context, "You cannot teleport yourself to yourself.", "Warning");
                return;
            }

            if (context.Player != null && !CanTargetOthers(context.Player) && target != context.Player)
            {
                Reply(context, Lang("NoTargetPermission", context.UserId), "Error");
                return;
            }

            if (context.Player == null)
            {
                Reply(context, Lang("InvalidSyntax", context.UserId, "/tphere <player>"), "Error");
                return;
            }

            TeleportPlayer(target, context.Player.transform.position, context.Player.eyes?.rotation);
            Reply(context, Lang("TeleportHere", context.UserId, target.displayName), "Success");
            LogToConsole($"{context.Name} teleported {target.displayName} to their location");
        }

        private void HandleTpLoc(CommandContext context, string[] args)
        {
            if (!IsPluginEnabled() || !IsCommandEnabled("TPLoc"))
                return;

            if (!HasPermission(context, PermissionBase + "tploc"))
            {
                Reply(context, Lang("NoPermission", context.UserId), "Error");
                return;
            }

            if (args == null) args = Array.Empty<string>();
            if (args.Length < 3)
            {
                Reply(context, Lang("InvalidSyntax", context.UserId, "/tploc <x> <y> <z>"), "Error");
                return;
            }

            if (!TryParseFloat(args[0], out float x) || !TryParseFloat(args[1], out float y) || !TryParseFloat(args[2], out float z))
            {
                Reply(context, Lang("InvalidCoordinates", context.UserId), "Error");
                return;
            }

            BasePlayer target = context.Player;
            if (args.Length >= 4)
            {
                BasePlayer possibleTarget = FindPlayer(args[3]);
                if (possibleTarget != null && possibleTarget != context.Player)
                {
                    if (context.Player != null && !CanTargetOthers(context.Player))
                    {
                        Reply(context, Lang("NoTargetPermission", context.UserId), "Error");
                        return;
                    }

                    target = possibleTarget;
                }
            }

            if (target == null)
            {
                Reply(context, Lang("PlayerNotFound", context.UserId), "Error");
                return;
            }

            Vector3 position = new Vector3(x, y, z);
            TeleportPlayer(target, position);
            Reply(context, Lang("TeleportToLoc", context.UserId, position.ToString()), "Success");

            LogToConsole($"{context.Name} teleported {target.displayName} to {position}");
        }

        [ChatCommand("heal")]
        private void CmdHeal(BasePlayer player, string cmd, string[] args) => HandleHealth(new CommandContext(this, player), args, "heal");

        [ConsoleCommand("heal")]
        private void CCmdHeal(ConsoleSystem.Arg arg) => HandleHealth(new CommandContext(this, arg?.Player(), arg), GetConsoleArgs(arg), "heal");

        [ChatCommand("eat")]
        private void CmdEat(BasePlayer player, string cmd, string[] args) => HandleHealth(new CommandContext(this, player), args, "eat");

        [ConsoleCommand("eat")]
        private void CCmdEat(ConsoleSystem.Arg arg) => HandleHealth(new CommandContext(this, arg?.Player(), arg), GetConsoleArgs(arg), "eat");

        [ChatCommand("clear")]
        private void CmdClear(BasePlayer player, string cmd, string[] args) => HandleClear(new CommandContext(this, player), args);

        [ConsoleCommand("clear")]
        private void CCmdClear(ConsoleSystem.Arg arg) => HandleClear(new CommandContext(this, arg?.Player(), arg), GetConsoleArgs(arg));

        [ChatCommand("give")]
        private void CmdGive(BasePlayer player, string cmd, string[] args) => HandleGive(new CommandContext(this, player), args);

        [ConsoleCommand("give")]
        private void CCmdGive(ConsoleSystem.Arg arg) => HandleGive(new CommandContext(this, arg?.Player(), arg), GetConsoleArgs(arg));

        [ChatCommand("repair")]
        private void CmdRepair(BasePlayer player, string cmd, string[] args) => HandleRepair(new CommandContext(this, player), args, false);

        [ConsoleCommand("repair")]
        private void CCmdRepair(ConsoleSystem.Arg arg) => HandleRepair(new CommandContext(this, arg?.Player(), arg), GetConsoleArgs(arg), false);

        [ChatCommand("repairall")]
        private void CmdRepairAll(BasePlayer player, string cmd, string[] args) => HandleRepair(new CommandContext(this, player), args, true);

        [ConsoleCommand("repairall")]
        private void CCmdRepairAll(ConsoleSystem.Arg arg) => HandleRepair(new CommandContext(this, arg?.Player(), arg), GetConsoleArgs(arg), true);

        [ChatCommand("kick")]
        private void CmdKick(BasePlayer player, string cmd, string[] args) => HandleKick(new CommandContext(this, player), args);

        [ConsoleCommand("kick")]
        private void CCmdKick(ConsoleSystem.Arg arg) => HandleKick(new CommandContext(this, arg?.Player(), arg), GetConsoleArgs(arg));

        [ChatCommand("list")]
        private void CmdList(BasePlayer player, string cmd, string[] args) => HandleList(new CommandContext(this, player), args);

        [ChatCommand("online")]
        private void CmdOnline(BasePlayer player, string cmd, string[] args) => HandleList(new CommandContext(this, player), args);

        [ChatCommand("players")]
        private void CmdPlayers(BasePlayer player, string cmd, string[] args) => HandleList(new CommandContext(this, player), args);

        [ConsoleCommand("list")]
        private void CCmdList(ConsoleSystem.Arg arg) => HandleList(new CommandContext(this, arg?.Player(), arg), GetConsoleArgs(arg));

        [ConsoleCommand("online")]
        private void CCmdOnline(ConsoleSystem.Arg arg) => HandleList(new CommandContext(this, arg?.Player(), arg), GetConsoleArgs(arg));

        [ConsoleCommand("players")]
        private void CCmdPlayers(ConsoleSystem.Arg arg) => HandleList(new CommandContext(this, arg?.Player(), arg), GetConsoleArgs(arg));

        [ChatCommand("ping")]
        private void CmdPing(BasePlayer player, string cmd, string[] args) => HandlePing(new CommandContext(this, player), args);

        [ConsoleCommand("ping")]
        private void CCmdPing(ConsoleSystem.Arg arg) => HandlePing(new CommandContext(this, arg?.Player(), arg), GetConsoleArgs(arg));

        [ChatCommand("time")]
        private void CmdTime(BasePlayer player, string cmd, string[] args) => HandleTime(new CommandContext(this, player), false);

        [ConsoleCommand("time")]
        private void CCmdTime(ConsoleSystem.Arg arg) => HandleTime(new CommandContext(this, arg?.Player(), arg), false);

        [ChatCommand("day")]
        private void CmdDay(BasePlayer player, string cmd, string[] args) => HandleTime(new CommandContext(this, player), true);

        [ConsoleCommand("day")]
        private void CCmdDay(ConsoleSystem.Arg arg) => HandleTime(new CommandContext(this, arg?.Player(), arg), true);

        [ChatCommand("night")]
        private void CmdNight(BasePlayer player, string cmd, string[] args) => HandleTime(new CommandContext(this, player), false, true);

        [ConsoleCommand("night")]
        private void CCmdNight(ConsoleSystem.Arg arg) => HandleTime(new CommandContext(this, arg?.Player(), arg), false, true);

        [ChatCommand("rules")]
        private void CmdRules(BasePlayer player, string cmd, string[] args) => HandleRules(new CommandContext(this, player));

        [ConsoleCommand("rules")]
        private void CCmdRules(ConsoleSystem.Arg arg) => HandleRules(new CommandContext(this, arg?.Player(), arg));

        [ChatCommand("cwe")]
        private void CmdCwe(BasePlayer player, string cmd, string[] args) => HandleCwe(new CommandContext(this, player), args);

        [ConsoleCommand("cwe")]
        private void CCmdCwe(ConsoleSystem.Arg arg) => HandleCwe(new CommandContext(this, arg?.Player(), arg), GetConsoleArgs(arg));

        [ChatCommand("cweversion")]
        private void CmdCweVersion(BasePlayer player, string cmd, string[] args) => HandleVersion(new CommandContext(this, player));

        [ConsoleCommand("cweversion")]
        private void CCmdCweVersion(ConsoleSystem.Arg arg) => HandleVersion(new CommandContext(this, arg?.Player(), arg));

        [ConsoleCommand("cwe.reload")]
        private void CCmdCweReload(ConsoleSystem.Arg arg) => HandleReload(new CommandContext(this, arg?.Player(), arg));

        [ConsoleCommand("cwe.version")]
        private void CCmdCweVersionDot(ConsoleSystem.Arg arg) => HandleVersion(new CommandContext(this, arg?.Player(), arg));

        [ConsoleCommand("cwe.help")]
        private void CCmdCweHelp(ConsoleSystem.Arg arg) => HandleHelp(new CommandContext(this, arg?.Player(), arg), GetConsoleArgs(arg));

        [ChatCommand("playerinfo")]
        private void CmdPlayerInfo(BasePlayer player, string cmd, string[] args) => HandlePlayerInfo(new CommandContext(this, player), args, false);

        [ConsoleCommand("playerinfo")]
        private void CCmdPlayerInfo(ConsoleSystem.Arg arg) => HandlePlayerInfo(new CommandContext(this, arg?.Player(), arg), GetConsoleArgs(arg), false);

        [ChatCommand("playerinfoall")]
        private void CmdPlayerInfoAll(BasePlayer player, string cmd, string[] args) => HandlePlayerInfo(new CommandContext(this, player), args, true);

        [ConsoleCommand("playerinfoall")]
        private void CCmdPlayerInfoAll(ConsoleSystem.Arg arg) => HandlePlayerInfo(new CommandContext(this, arg?.Player(), arg), GetConsoleArgs(arg), true);

        private void HandleHealth(CommandContext context, string[] args, string mode)
        {
            if (!IsPluginEnabled() || !IsCommandEnabled(mode == "heal" ? "Heal" : "Eat"))
                return;

            if (!HasPermission(context, PermissionBase + mode))
            {
                Reply(context, Lang("NoPermission", context.UserId), "Error");
                return;
            }

            if (args == null) args = Array.Empty<string>();
            BasePlayer target = context.Player;

            if (args.Length > 0)
            {
                BasePlayer possibleTarget = FindPlayer(args[0]);
                if (possibleTarget != null && possibleTarget != context.Player)
                {
                    if (context.Player != null && !CanTargetOthers(context.Player))
                    {
                        Reply(context, Lang("NoTargetPermission", context.UserId), "Error");
                        return;
                    }

                    target = possibleTarget;
                }
            }

            if (target == null)
            {
                Reply(context, Lang("PlayerNotFound", context.UserId), "Error");
                return;
            }

            if (mode == "heal")
            {
                target.health = target.MaxHealth();
                if (target.metabolism != null)
                {
                    target.metabolism.radiation_poison.value = 0f;
                    target.metabolism.poison.value = 0f;
                    target.metabolism.bleeding.value = 0f;
                }

                target.SendNetworkUpdateImmediate();
                Reply(context, target == context.Player ? Lang("HealDone", context.UserId) : Lang("HealOther", context.UserId, target.displayName), "Success");
                LogToConsole($"{context.Name} healed {target.displayName}");
            }
            else
            {
                if (target.metabolism != null)
                {
                    target.metabolism.calories.value = target.metabolism.calories.max;
                    target.metabolism.hydration.value = target.metabolism.hydration.max;
                }

                target.SendNetworkUpdateImmediate();
                Reply(context, target == context.Player ? Lang("EatDone", context.UserId) : Lang("EatOther", context.UserId, target.displayName), "Success");
                LogToConsole($"{context.Name} fed and hydrated {target.displayName}");
            }
        }

        private void HandleClear(CommandContext context, string[] args)
        {
            if (!IsPluginEnabled() || !IsCommandEnabled("Clear"))
                return;

            if (!HasPermission(context, PermissionBase + "clear"))
            {
                Reply(context, Lang("NoPermission", context.UserId), "Error");
                return;
            }

            if (args == null) args = Array.Empty<string>();
            BasePlayer target = context.Player;
            if (args.Length > 0)
            {
                BasePlayer possibleTarget = FindPlayer(args[0]);
                if (possibleTarget != null && possibleTarget != context.Player)
                {
                    if (context.Player != null && !CanTargetOthers(context.Player))
                    {
                        Reply(context, Lang("NoTargetPermission", context.UserId), "Error");
                        return;
                    }

                    target = possibleTarget;
                }
            }

            if (target == null)
            {
                Reply(context, Lang("PlayerNotFound", context.UserId), "Error");
                return;
            }

            target.inventory?.Strip();
            Reply(context, target == context.Player ? Lang("ClearDone", context.UserId) : Lang("ClearOther", context.UserId, target.displayName), "Success");
            LogToConsole($"{context.Name} cleared inventory for {target.displayName}");
        }

        private void HandleGive(CommandContext context, string[] args)
        {
            if (!IsPluginEnabled() || !IsCommandEnabled("Give"))
                return;

            if (!HasPermission(context, PermissionBase + "give"))
            {
                Reply(context, Lang("NoPermission", context.UserId), "Error");
                return;
            }

            if (args == null) args = Array.Empty<string>();
            if (args.Length < 2)
            {
                Reply(context, Lang("InvalidSyntax", context.UserId, "/give <item> <amount> [target]"), "Error");
                return;
            }

            string itemName = args[0];
            if (!TryParseInt(args[1], out int amount) || amount <= 0)
            {
                Reply(context, Lang("InvalidNumber", context.UserId), "Error");
                return;
            }

            BasePlayer target = context.Player;
            if (args.Length >= 3)
            {
                BasePlayer possibleTarget = FindPlayer(args[2]);
                if (possibleTarget != null && possibleTarget != context.Player)
                {
                    if (context.Player != null && !CanTargetOthers(context.Player))
                    {
                        Reply(context, Lang("NoTargetPermission", context.UserId), "Error");
                        return;
                    }

                    target = possibleTarget;
                }
            }

            if (target == null)
            {
                Reply(context, Lang("PlayerNotFound", context.UserId), "Error");
                return;
            }

            ItemDefinition definition = ItemManager.FindItemDefinition(itemName);
            if (definition == null)
            {
                Reply(context, Lang("GiveNotFound", context.UserId, itemName), "Error");
                return;
            }

            Item item = ItemManager.Create(definition, amount);
            if (item == null)
            {
                Reply(context, "Failed to create the item.", "Error");
                return;
            }

            bool given = target.inventory != null && target.inventory.GiveItem(item);
            if (!given)
            {
                item.Drop(target.transform.position + Vector3.up * 2f, Vector3.zero);
                Reply(context, Lang("GiveDropped", context.UserId, target.displayName), "Warning");
            }

            string displayName = definition.displayName?.english ?? definition.shortname;
            if (target == context.Player)
                Reply(context, Lang("GiveSelf", context.UserId, displayName, amount), "Success");
            else
                Reply(context, Lang("GiveOther", context.UserId, displayName, amount, target.displayName), "Success");

            LogToConsole($"{context.Name} gave {amount} x {definition.shortname} to {target.displayName}");
        }

        private void HandleRepair(CommandContext context, string[] args, bool repairAll)
        {
            if (!IsPluginEnabled() || !IsCommandEnabled(repairAll ? "RepairAll" : "Repair"))
                return;

            string permissionName = PermissionBase + (repairAll ? "repairall" : "repair");
            if (!HasPermission(context, permissionName))
            {
                Reply(context, Lang("NoPermission", context.UserId), "Error");
                return;
            }

            if (args == null) args = Array.Empty<string>();
            BasePlayer target = context.Player;
            if (args.Length > 0)
            {
                BasePlayer possibleTarget = FindPlayer(args[0]);
                if (possibleTarget != null && possibleTarget != context.Player)
                {
                    if (context.Player != null && !CanTargetOthers(context.Player))
                    {
                        Reply(context, Lang("NoTargetPermission", context.UserId), "Error");
                        return;
                    }

                    target = possibleTarget;
                }
            }

            if (target == null)
            {
                Reply(context, Lang("PlayerNotFound", context.UserId), "Error");
                return;
            }

            int repaired = 0;
            if (repairAll)
            {
                IEnumerable<Item> items = EnumeratePlayerItems(target);
                foreach (Item item in items)
                {
                    if (item == null || !item.hasCondition)
                        continue;

                    item.condition = item.maxCondition;
                    repaired++;
                }

                target.inventory?.SendSnapshot();
                Reply(context, target == context.Player ? Lang("RepairAllDone", context.UserId) : Lang("RepairAllOther", context.UserId, target.displayName), "Success");
            }
            else
            {
                Item active = target.GetActiveItem();
                if (active == null)
                {
                    Reply(context, "No active item found.", "Warning");
                    return;
                }

                if (active.hasCondition)
                {
                    active.condition = active.maxCondition;
                    repaired = 1;
                }

                target.inventory?.SendSnapshot();
                Reply(context, target == context.Player ? Lang("RepairDone", context.UserId) : Lang("RepairOther", context.UserId, target.displayName), "Success");
            }

            LogToConsole($"{context.Name} repaired {(repairAll ? "all items" : "the active item")} for {target.displayName} ({repaired} item(s))");
        }

        private IEnumerable<Item> EnumeratePlayerItems(BasePlayer player)
        {
            if (player == null || player.inventory == null)
                yield break;

            foreach (Item item in player.inventory.containerBelt?.itemList ?? new List<Item>())
                yield return item;

            foreach (Item item in player.inventory.containerMain?.itemList ?? new List<Item>())
                yield return item;

            foreach (Item item in player.inventory.containerWear?.itemList ?? new List<Item>())
                yield return item;
        }

        private void HandleKick(CommandContext context, string[] args)
        {
            if (!IsPluginEnabled() || !IsCommandEnabled("Kick"))
                return;

            if (!HasPermission(context, PermissionBase + "kick"))
            {
                Reply(context, Lang("NoPermission", context.UserId), "Error");
                return;
            }

            if (args == null) args = Array.Empty<string>();
            if (args.Length < 1)
            {
                Reply(context, Lang("InvalidSyntax", context.UserId, "/kick <player> [reason]"), "Error");
                return;
            }

            BasePlayer target = FindPlayer(args[0]);
            if (target == null)
            {
                Reply(context, Lang("PlayerNotFound", context.UserId), "Error");
                return;
            }

            if (context.Player != null && target == context.Player)
            {
                Reply(context, Lang("KickSelf", context.UserId), "Warning");
                return;
            }

            string reason = args.Length > 1 ? string.Join(" ", args.Skip(1).ToArray()) : "Kicked by an administrator.";
            target.Kick(reason);

            Reply(context, Lang("KickSuccess", context.UserId, target.displayName, reason), "Success");
            LogToConsole($"{context.Name} kicked {target.displayName} for: {reason}");
        }

        private void HandleList(CommandContext context, string[] args)
        {
            if (!IsPluginEnabled() || !IsCommandEnabled("List"))
                return;

            if (!HasPermission(context, PermissionBase + "list"))
            {
                Reply(context, Lang("NoPermission", context.UserId), "Error");
                return;
            }

            List<BasePlayer> players = BasePlayer.activePlayerList
                .Where(p => p != null && !HasSavedVanishState(p) && !IsVanishEnabled(p))
                .OrderBy(p => p.displayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (players.Count == 0)
            {
                Reply(context, Lang("ListHeader", context.UserId, 0), "Highlight", true);
                Reply(context, "No players are currently online.", "Info");
                return;
            }

            List<string> entries = players
                .Select(player => Lang("ListEntry", context.UserId, player.displayName, player.UserIDString))
                .ToList();

            if (players.Count <= 30)
            {
                Reply(context, $"{Lang("ListHeader", context.UserId, players.Count)}\n{string.Join(", ", entries)}", "Highlight", true);
            }
            else
            {
                int splitIndex = (players.Count + 1) / 2;
                string firstHalf = string.Join(", ", entries.Take(splitIndex));
                string secondHalf = string.Join(", ", entries.Skip(splitIndex));

                Reply(context, $"{Lang("ListHeader", context.UserId, players.Count)} (1/2)\n{firstHalf}", "Highlight", true);
                Reply(context, $"{Lang("ListHeader", context.UserId, players.Count)} (2/2)\n{secondHalf}", "Info");
            }

            LogToConsole($"{context.Name} requested the online player list ({players.Count} players).");
        }

        private void HandlePing(CommandContext context, string[] args)
        {
            if (!IsPluginEnabled() || !IsCommandEnabled("Ping"))
                return;

            if (!HasPermission(context, PermissionBase + "ping"))
            {
                Reply(context, Lang("NoPermission", context.UserId), "Error");
                return;
            }

            if (args == null) args = Array.Empty<string>();
            BasePlayer target = context.Player;

            if (args.Length > 0)
            {
                BasePlayer possibleTarget = FindPlayer(args[0]);
                if (possibleTarget != null && possibleTarget != context.Player)
                {
                    if (context.Player != null && !CanTargetOthers(context.Player))
                    {
                        Reply(context, Lang("NoTargetPermission", context.UserId), "Error");
                        return;
                    }

                    target = possibleTarget;
                }
            }

            if (target == null)
            {
                Reply(context, Lang("PlayerNotFound", context.UserId), "Error");
                return;
            }

            int ping = 0;
            try
            {
                if (target.net?.connection != null)
                    ping = Network.Net.sv.GetAveragePing(target.net.connection);
            }
            catch
            {
                ping = 0;
            }

            if (target == context.Player)
                Reply(context, Lang("PingResult", context.UserId, ping), "Info");
            else
                Reply(context, Lang("PingOtherResult", context.UserId, target.displayName, ping), "Info");

            LogToConsole($"{context.Name} checked the ping for {target.displayName}: {ping} ms");
        }

        private void HandleTime(CommandContext context, bool setDay = false, bool setNight = false)
        {
            bool isSetCommand = setDay || setNight;

            if (!IsPluginEnabled() || !(isSetCommand ? IsCommandEnabled("DayNight") : IsCommandEnabled("Time")))
                return;

            string permissionName = isSetCommand ? PermissionBase + "time.set" : PermissionBase + "time";
            if (!HasPermission(context, permissionName))
            {
                Reply(context, Lang("NoPermission", context.UserId), "Error");
                return;
            }

            if (isSetCommand)
            {
                if (TOD_Sky.Instance == null || TOD_Sky.Instance.Cycle == null)
                {
                    Reply(context, Lang("TimeSystemUnavailable", context.UserId), "Error");
                    return;
                }

                TOD_Sky.Instance.Cycle.Hour = setNight ? 0f : 12f;
                Reply(context, setNight ? Lang("NightSet", context.UserId) : Lang("DaySet", context.UserId), "Success");
                LogToConsole($"{context.Name} set the time to {(setNight ? "night" : "day")}.");
                return;
            }

            if (TOD_Sky.Instance == null || TOD_Sky.Instance.Cycle == null)
            {
                Reply(context, Lang("TimeSystemUnavailable", context.UserId), "Error");
                return;
            }

            float hour = TOD_Sky.Instance.Cycle.Hour;
            Reply(context, Lang("TimeResult", context.UserId, hour.ToString("F2", CultureInfo.InvariantCulture)), "Info");
        }

        private void HandleRules(CommandContext context)
        {
            if (!IsPluginEnabled() || !IsCommandEnabled("Rules"))
                return;

            if (!HasPermission(context, PermissionBase + "rules"))
            {
                Reply(context, Lang("NoPermission", context.UserId), "Error");
                return;
            }

            string path = GetRulesFilePath();
            if (!File.Exists(path))
            {
                Reply(context, Lang("RulesNotFound", context.UserId), "Error");
                return;
            }

            string[] lines;
            try
            {
                lines = File.ReadAllLines(path);
            }
            catch (Exception ex)
            {
                Reply(context, Lang("RulesReadFailed", context.UserId), "Error");
                PrintError($"Failed to read rules file '{path}': {ex}");
                return;
            }

            List<string> rules = lines
                .Select(line => line?.TrimEnd('\r'))
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => line.Trim())
                .ToList();

            if (rules.Count == 0)
            {
                Reply(context, Lang("NoRulesDefined", context.UserId), "Warning");
                return;
            }

            Reply(context, Lang("RulesHeader", context.UserId), "Highlight", true);

            const int maxCharsPerMessage = 700;
            string currentChunk = string.Empty;

            foreach (string rule in rules)
            {
                string nextLine = string.IsNullOrEmpty(currentChunk) ? rule : currentChunk + "\n" + rule;

                if (nextLine.Length > maxCharsPerMessage && !string.IsNullOrEmpty(currentChunk))
                {
                    Reply(context, currentChunk, "Info");
                    currentChunk = rule;
                    continue;
                }

                currentChunk = nextLine;
            }

            if (!string.IsNullOrEmpty(currentChunk))
                Reply(context, currentChunk, "Info");
        }

        private void HandleCwe(CommandContext context, string[] args)
        {
            if (args == null) args = Array.Empty<string>();
            if (args.Length == 0)
            {
                HandleHelp(context, Array.Empty<string>());
                return;
            }

            switch (args[0].ToLowerInvariant())
            {
                case "help":
                    HandleHelp(context, args.Skip(1).ToArray());
                    return;
                case "reload":
                    HandleReload(context);
                    return;
                case "version":
                    HandleVersion(context);
                    return;
                default:
                    Reply(context, Lang("UnknownCommand", context.UserId), "Error");
                    return;
            }
        }

        private void HandleHelp(CommandContext context, string[] args)
        {
            if (!IsPluginEnabled())
                return;

            if (!HasPermission(context, PermissionBase + "help"))
            {
                Reply(context, Lang("NoPermission", context.UserId), "Error");
                return;
            }

            int page = 1;
            if (args != null && args.Length > 0)
                int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out page);

            var entries = BuildHelpEntries(context);
            if (entries.Count == 0)
            {
                Reply(context, Lang("HelpNoCommands", context.UserId), "Info");
                return;
            }

            const int perPage = 8;
            int totalPages = Mathf.CeilToInt(entries.Count / (float)perPage);
            page = Mathf.Clamp(page, 1, totalPages);

            int start = (page - 1) * perPage;
            int end = Math.Min(start + perPage, entries.Count);

            Reply(context, Lang("HelpHeader", context.UserId), "Highlight", true);
            Reply(context, Lang("HelpPage", context.UserId, page, totalPages), "Info");

            for (int i = start; i < end; i++)
                Reply(context, Lang("HelpLine", context.UserId, entries[i].Command, entries[i].Description), "Info");
        }

        private List<HelpEntry> BuildHelpEntries(CommandContext context)
        {
            var items = new List<HelpEntry>
            {
                new HelpEntry("maintenance", "Toggle maintenance mode and manage its whitelist.", "Maintenance"),
                new HelpEntry("god", "Toggle invulnerability for yourself or another player.", "God"),
                new HelpEntry("fly", "Toggle flight mode for yourself or another player.", "Fly"),
                new HelpEntry("noclip", "Toggle noclip mode for yourself or another player.", "Noclip"),
                new HelpEntry("vanish", "Toggle invisibility for yourself or another player.", "Vanish"),
                new HelpEntry("speed", "Set movement speed for yourself or another player.", "Speed"),
                new HelpEntry("tp", "Teleport to a player.", "TP"),
                new HelpEntry("tphere", "Teleport a player to your location.", "TPHere"),
                new HelpEntry("tploc", "Teleport to coordinates.", "TPLoc"),
                new HelpEntry("heal", "Restore health for yourself or another player.", "Heal"),
                new HelpEntry("eat", "Restore food and water for yourself or another player.", "Eat"),
                new HelpEntry("clear", "Clear inventory for yourself or another player.", "Clear"),
                new HelpEntry("give", "Give an item to yourself or another player.", "Give"),
                new HelpEntry("repair", "Repair the active item.", "Repair"),
                new HelpEntry("repairall", "Repair all items in inventory.", "RepairAll"),
                new HelpEntry("kick", "Kick a player from the server.", "Kick"),
                new HelpEntry("list", "List online players.", "List"),
                new HelpEntry("online", "List online players.", "List"),
                new HelpEntry("players", "List online players.", "List"),
                new HelpEntry("ping", "Show ping for yourself or another player.", "Ping"),
                new HelpEntry("time", "Show the current in-game time.", "Time"),
                new HelpEntry("day", "Set the world time to day.", "DayNight"),
                new HelpEntry("night", "Set the world time to night.", "DayNight"),
                new HelpEntry("rules", "Show server rules from file.", "Rules"),
                new HelpEntry("playerinfo", "Show brief information about a player.", "PlayerInfo"),
                new HelpEntry("playerinfoall", "Show diagnostic/engine information about a player.", "PlayerInfoAll"),
                new HelpEntry("cwe help", "Show this help page.", "Help"),
                new HelpEntry("cwe reload", "Reload the plugin configuration and data.", "Reload"),
                new HelpEntry("cwe version", "Show the plugin version.", "Version")
            };

            var output = new List<HelpEntry>();
            foreach (HelpEntry entry in items)
            {
                if (!IsCommandEnabled(entry.ConfigName))
                    continue;

                if (context.Player != null)
                {
                    string permissionName = GetHelpPermissionName(entry.ConfigName);
                    if (!HasPermission(context.Player, permissionName) && !IsAdmin(context.Player))
                        continue;
                }

                output.Add(entry);
            }

            output.Sort((a, b) => string.Compare(a.Command, b.Command, StringComparison.OrdinalIgnoreCase));
            return output;
        }

        private void HandleReload(CommandContext context)
        {
            if (!IsCommandEnabled("Reload"))
                return;

            if (!HasPermission(context, PermissionBase + "reload"))
            {
                Reply(context, Lang("NoPermission", context.UserId), "Error");
                return;
            }

            try
            {
                LoadConfig();
                LoadData();
                RegisterPermissions();
                RefreshMaintenanceTimer();

                if (IsPluginEnabled() && _config?.Maintenance?.Enabled == true)
                    KickPlayersForMaintenance();

                Reply(context, Lang("ReloadSuccess", context.UserId), "Success");
                LogToConsole($"{context.Name} reloaded the plugin.");
            }
            catch (Exception ex)
            {
                Reply(context, Lang("ReloadFailed", context.UserId), "Error");
                PrintError($"Reload failed: {ex}");
            }
        }

        private void HandleVersion(CommandContext context)
        {
            if (!IsCommandEnabled("Version"))
                return;

            if (!HasPermission(context, PermissionBase + "version"))
            {
                Reply(context, Lang("NoPermission", context.UserId), "Error");
                return;
            }

            Reply(context, Lang("VersionMessage", context.UserId, PluginVersion), "Info");
        }

        private void HandlePlayerInfo(CommandContext context, string[] args, bool detailed)
        {
            string cmdName = detailed ? "PlayerInfoAll" : "PlayerInfo";
            string permSuffix = detailed ? "playerinfoall" : "playerinfo";

            if (!IsPluginEnabled() || !IsCommandEnabled(cmdName))
                return;

            if (!HasPermission(context, PermissionBase + permSuffix))
            {
                Reply(context, Lang("NoPermission", context.UserId), "Error");
                return;
            }

            BasePlayer target = context.Player;

            if (args != null && args.Length > 0)
            {
                BasePlayer possibleTarget = FindPlayer(args[0]);
                if (possibleTarget != null)
                {
                    if (context.Player != null && !CanTargetOthers(context.Player) && possibleTarget != context.Player)
                    {
                        Reply(context, Lang("NoTargetPermission", context.UserId), "Error");
                        return;
                    }
                    target = possibleTarget;
                }
                else
                {
                    Reply(context, Lang("PlayerNotFound", context.UserId), "Error");
                    return;
                }
            }

            if (target == null)
            {
                Reply(context, Lang("MustSpecifyPlayer", context.UserId), "Error");
                return;
            }

            PlayerState state = GetState(target);
            string pingStr = "0";
            string ipStr = "Local/RCON";

            if (target.net?.connection != null)
            {
                ipStr = target.net.connection.ipaddress;
                try
                {
                    pingStr = Network.Net.sv.GetAveragePing(target.net.connection).ToString();
                }
                catch
                {
                    pingStr = "N/A";
                }
            }

            string activeItemStr = target.GetActiveItem()?.info?.shortname ?? "None";

            // Получаем Oxide группы и пермишены игрока
            string[] userGroups = permission.GetUserGroups(target.UserIDString) ?? Array.Empty<string>();
            string groupsStr = userGroups.Length > 0 ? string.Join(", ", userGroups) : "None";

            string[] userPerms = permission.GetUserPermissions(target.UserIDString) ?? Array.Empty<string>();
            string permsStr = userPerms.Length > 0 ? string.Join(", ", userPerms) : "None";

            if (!detailed)
            {
                // Краткая сводка (playerinfo)
                string briefHeader = Lang("PlayerInfoBriefHeader", context.UserId, target.displayName);
                
                string message = $"{briefHeader}\n" +
                                $"• SteamID (OwnerID): {target.UserIDString} | IsAdmin: {(target.IsAdmin ? "YES" : "NO")}\n" +
                                $"• IP: {ipStr} | Ping: {pingStr}ms\n" +
                                $"• Position: X: {target.transform.position.x:F2}, Y: {target.transform.position.y:F2}, Z: {target.transform.position.z:F2}\n" +
                                $"• Health: {target.health:F1}/{target.MaxHealth():F1} | Active: {activeItemStr}\n" +
                                $"• Oxide Groups: {groupsStr}\n" +
                                $"• CWE States: God: {(state?.God == true ? "ON" : "OFF")}, Vanish: {(state?.Vanish == true ? "ON" : "OFF")}, Fly: {(state?.Fly == true ? "ON" : "OFF")}, Noclip: {(state?.Noclip == true ? "ON" : "OFF")}, Speed: {state?.Speed ?? 1f:F1}x";

                Reply(context, message, "Info");
            }
            else
            {
                // Подробная сводка (playerinfoall)
                string detailedHeader = Lang("PlayerInfoDetailedHeader", context.UserId, target.displayName);

                string teamIdStr = "None";
                if (RelationshipManager.ServerInstance != null)
                {
                    var team = RelationshipManager.ServerInstance.FindPlayersTeam(target.userID);
                    if (team != null)
                        teamIdStr = team.teamID.ToString();
                }

                float cal = target.metabolism?.calories?.value ?? 0f;
                float maxCal = target.metabolism?.calories?.max ?? 0f;
                float hyd = target.metabolism?.hydration?.value ?? 0f;
                float maxHyd = target.metabolism?.hydration?.max ?? 0f;
                float temp = target.metabolism?.temperature?.value ?? 0f;
                float rad = target.metabolism?.radiation_poison?.value ?? 0f;

                int mainCount = target.inventory?.containerMain?.itemList?.Count ?? 0;
                int beltCount = target.inventory?.containerBelt?.itemList?.Count ?? 0;
                int wearCount = target.inventory?.containerWear?.itemList?.Count ?? 0;

                Vector3 rot = target.eyes?.rotation.eulerAngles ?? Vector3.zero;

                string message = $"{detailedHeader}\n" +
                                $"<color=#ffaa00>[Identity, Connection & Oxide]</color>\n" +
                                $"  - SteamID (OwnerID): {target.UserIDString}\n" +
                                $"  - IP Address: {ipStr} | Avg Ping: {pingStr} ms\n" +
                                $"  - Auth Level: {target.net?.connection?.authLevel ?? 0} (IsAdmin: {target.IsAdmin})\n" +
                                $"  - Oxide Groups: {groupsStr}\n" +
                                $"  - Oxide Perms: {permsStr}\n" +
                                $"<color=#ffaa00>[Game States]</color>\n" +
                                $"  - Alive: {target.IsAlive()} | Sleeping: {target.IsSleeping()} | Wounded: {target.IsWounded()}\n" +
                                $"  - Building Blocked: {target.IsBuildingBlocked()} | Team ID: {teamIdStr}\n" +
                                $"<color=#ffaa00>[Stats & Metabolism]</color>\n" +
                                $"  - Health: {target.health:F1} / {target.MaxHealth():F1}\n" +
                                $"  - Calories: {cal:F0} / {maxCal:F0} | Hydration: {hyd:F0} / {maxHyd:F0}\n" +
                                $"  - Temperature: {temp:F1}°C | Radiation: {rad:F1}\n" +
                                $"<color=#ffaa00>[Inventory & Loadout]</color>\n" +
                                $"  - Container Main: {mainCount} items | Belt: {beltCount} items | Wear: {wearCount} items\n" +
                                $"  - Active Item: {activeItemStr}\n" +
                                $"<color=#ffaa00>[Location]</color>\n" +
                                $"  - Position: X: {target.transform.position.x:F4}, Y: {target.transform.position.y:F4}, Z: {target.transform.position.z:F4}\n" +
                                $"  - Rotation: Pitch: {rot.x:F2}, Yaw: {rot.y:F2}, Roll: {rot.z:F2}\n" +
                                $"<color=#ffaa00>[CWE Essentials States]</color>\n" +
                                $"  - God Mode: {(state?.God == true ? "ON" : "OFF")} | Vanish: {(state?.Vanish == true ? "ON" : "OFF")}\n" +
                                $"  - Flight Mode: {(state?.Fly == true ? "ON" : "OFF")} | Noclip: {(state?.Noclip == true ? "ON" : "OFF")}\n" +
                                $"  - Speed Multiplier: {state?.Speed ?? 1f:F1}x | Movement Synced: {(state?.MovementSynced == true ? "ON" : "OFF")}";

                Reply(context, message, "Info");
            }
        }

        private string GetHelpPermissionName(string configName)
        {
            if (string.IsNullOrEmpty(configName))
                return PermissionBase;

            switch (configName)
            {
                case "DayNight":
                    return PermissionBase + "time.set";
                case "Help":
                    return PermissionBase + "help";
                case "Reload":
                    return PermissionBase + "reload";
                case "Version":
                    return PermissionBase + "version";
                case "PlayerInfo":
                    return PermissionBase + "playerinfo";
                case "PlayerInfoAll":
                    return PermissionBase + "playerinfoall";
                default:
                    return PermissionBase + configName.ToLowerInvariant();
            }
        }

        private class HelpEntry
        {
            public HelpEntry(string command, string description, string configName)
            {
                Command = command;
                Description = description;
                ConfigName = configName;
            }

            public string Command { get; }
            public string Description { get; }
            public string ConfigName { get; }
        }

        #endregion

        #region Hooks

        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (!IsPluginEnabled() || entity == null || info == null || _config?.Commands?.God?.Enabled != true)
                return null;

            BasePlayer player = entity as BasePlayer;
            if (player == null)
                return null;

            PlayerState state = GetState(player);
            return state != null && state.God ? (object)false : null;
        }

        private object OnPlayerTick(BasePlayer player, PlayerTick msg, bool wasStalled)
        {
            if (!IsPluginEnabled() || player == null || !player.IsConnected)
                return null;

            PlayerState state = GetState(player);
            if (state == null)
                return null;

            if (state.Fly || state.Noclip)
                SyncMovementMode(player, state);

            if (state.Vanish && !IsVanishEnabled(player))
                SyncVanishState(player, true);
            else if (!state.Vanish && (IsVanishEnabled(player) || player.limitNetworking || player.isInvisible))
                SyncVanishState(player, false);

            return null;
        }

        private void OnPlayerSleepEnded(BasePlayer player)
        {
            if (!IsPluginEnabled() || player == null)
                return;

            PlayerState state = GetState(player);
            if (state == null)
                return;

            state.MovementSynced = false;

            if (state.Vanish)
                SyncVanishState(player, true);

            if (state.Fly || state.Noclip)
                SyncMovementMode(player, state);
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (!IsPluginEnabled() || player == null || _config?.Maintenance?.Enabled != true)
                return;

            if (!HasMaintenanceBypass(player))
                NextTick(() => player.Kick(_config.Settings.MaintenanceMessage));
        }

        #endregion
    }
}