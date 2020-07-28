using System;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Modular Car Code Locks", "WhiteThunder", "1.0.0")]
    [Description("Allows players to deploy code locks to Modular Cars.")]
    internal class CarCodeLocks : CovalencePlugin
    {
        #region Fields

        private PluginConfig pluginConfig;

        private const string PermissionUse = "carcodelocks.use";
        private const string PermissionFreeLock = "carcodelocks.free";

        private const string CodeLockPrefab = "assets/prefabs/locks/keypad/lock.code.prefab";
        private const string CodeLockDeployedEffectPrefab = "assets/prefabs/locks/keypad/effects/lock-code-deploy.prefab";

        private const int CodeLockItemId = 1159991980;

        private readonly Vector3 CodeLockPosition = new Vector3(-0.9f, 0.35f, -0.5f);

        private CooldownManager Cooldowns;

        #endregion

        #region Hooks

        private void Init()
        {
            pluginConfig = Config.ReadObject<PluginConfig>();

            permission.RegisterPermission(PermissionUse, this);
            permission.RegisterPermission(PermissionFreeLock, this);

            Cooldowns = new CooldownManager(pluginConfig.CooldownSeconds);
        }

        private void OnEntityKill(VehicleModuleSeating seatingModule)
        {
            var car = seatingModule.Vehicle as ModularCar;
            if (car == null) return;

            var codeLock = seatingModule.GetComponentInChildren<CodeLock>();
            if (codeLock == null) return;

            // Try to move the lock to another cockpit module
            codeLock.SetParent(null);
            NextTick(() =>
            {
                var driverModule = car != null ? FindFirstDriverModule(car) : null;
                if (driverModule != null)
                    codeLock.SetParent(driverModule);
                else
                    codeLock.Kill();
            });
        }

        object CanMountEntity(BasePlayer player, BaseVehicleMountPoint entity)
        {
            var car = entity?.GetVehicleParent() as ModularCar;
            return CanPlayerInteractWithCar(player, car);
        }

        object CanLootEntity(BasePlayer player, StorageContainer container)
        {
            var parent = container.GetParentEntity();
            var car = parent as ModularCar ?? (parent as VehicleModuleStorage)?.Vehicle as ModularCar;
            return CanPlayerInteractWithCar(player, car);
        }

        object CanLootEntity(BasePlayer player, LiquidContainer container)
        {
            var car = (container.GetParentEntity() as VehicleModuleStorage)?.Vehicle as ModularCar;
            return CanPlayerInteractWithCar(player, car);
        }

        object CanLootEntity(BasePlayer player, ModularCarGarage carLift)
        {
            if (pluginConfig.AllowEditingWhileLockedOut || !carLift.PlatformIsOccupied) return null;
            return CanPlayerInteractWithCar(player, carLift.carOccupant);
        }

        private object CanPlayerInteractWithCar(BasePlayer player, ModularCar car)
        {
            if (car == null) return null;

            var codeLock = GetCarCodeLock(car);
            if (codeLock == null || IsPlayerAuthorizedToCodeLock(player, codeLock)) return null;

            Effect.server.Run(codeLock.effectDenied.resourcePath, codeLock, 0, Vector3.zero, Vector3.forward);
            player.ChatMessage(GetMessage(player.IPlayer, "Error.CarLocked"));

            return false;
        }

        #endregion

        #region API

        private CodeLock API_DeployCodeLock(ModularCar car, BasePlayer player)
        {
            if (car == null || car.IsDead() || DeployWasBlocked(car, player) || GetCarCodeLock(car) != null) return null;

            if (player != null)
                return DeployCodeLockForPlayer(car, player, isFree: true);

            var driverModule = FindFirstDriverModule(car);
            if (driverModule == null) return null;

            return DeployCodeLock(car, driverModule);
        }

        #endregion

        #region Commands

        [Command("carcodelock", "ccl")]
        private void CarCodeLockCommand(IPlayer player, string cmd, string[] args)
        {
            if (player.IsServer) return;

            ModularCar car;
            if (!VerifyPermissionAny(player, PermissionUse) ||
                !VerifyNotBuildingBlocked(player) ||
                !VerifyCarFound(player, out car) ||
                !VerifyCarIsNotDead(player, car) ||
                !VerifyCarHasNoLock(player, car) ||
                !VerifyCarCanHaveALock(player, car) ||
                !VerifyPlayerHasLockOrResources(player) ||
                !VerifyOffCooldown(player))
                return;

            var basePlayer = player.Object as BasePlayer;
            if (DeployWasBlocked(car, basePlayer)) return;

            var codeLock = DeployCodeLockForPlayer(car, basePlayer, isFree: player.HasPermission(PermissionFreeLock));
            if (codeLock == null) return;

            Cooldowns.UpdateLastUsedForPlayer(player.Id);
        }

        #endregion

        #region Helpers

        private bool DeployWasBlocked(ModularCar car, BasePlayer player)
        {
            object hookResult = Interface.CallHook("CanDeployCarCodeLock", car, player);
            return (hookResult is bool && (bool)hookResult == false);
        }

        private bool VerifyPermissionAny(IPlayer player, params string[] permissionNames)
        {
            foreach (var perm in permissionNames)
            {
                if (!permission.UserHasPermission(player.Id, perm))
                {
                    ReplyToPlayer(player, "Error.NoPermission");
                    return false;
                }
            }
            return true;
        }

        private bool VerifyNotBuildingBlocked(IPlayer player)
        {
            if (!(player.Object as BasePlayer).IsBuildingBlocked()) return true;
            ReplyToPlayer(player, "Error.BuildingBlocked");
            return false;
        }

        private bool VerifyCarFound(IPlayer player, out ModularCar car)
        {
            var basePlayer = player.Object as BasePlayer;
            var entity = GetLookEntity(basePlayer);

            if (pluginConfig.AllowDeployOffLift)
            {
                car = entity as ModularCar;
                if (car != null) return true;

                // Check for a lift as well since sometimes it blocks the ray
                car = (entity as ModularCarGarage)?.carOccupant;
                if (car != null) return true;

                ReplyToPlayer(player, "Error.NoCarFound");
                return false;
            }

            car = (entity as ModularCarGarage)?.carOccupant;
            if (car != null) return true;

            var carEnt = entity as ModularCar;
            if (carEnt == null)
            {
                ReplyToPlayer(player, "Error.NoCarFound");
                return false;
            }

            if (!IsCarOnLift(carEnt))
            {
                ReplyToPlayer(player, "Error.NotOnLift");
                return false;
            }

            car = carEnt;
            return true;
        }

        private bool VerifyCarIsNotDead(IPlayer player, ModularCar car)
        {
            if (!car.IsDead()) return true;
            ReplyToPlayer(player, "Error.CarDead");
            return false;
        }

        private bool VerifyCarHasNoLock(IPlayer player, ModularCar car)
        {
            if (GetCarCodeLock(car) == null) return true;
            ReplyToPlayer(player, "Error.HasLock");
            return false;
        }

        private bool VerifyCarCanHaveALock(IPlayer player, ModularCar car)
        {
            if (car.carLock.CanHaveALock()) return true;
            ReplyToPlayer(player, "Error.NoCockpit");
            return false;
        }

        private bool VerifyPlayerHasLockOrResources(IPlayer player)
        {
            if (player.HasPermission(PermissionFreeLock)) return true;

            var playerInventory = (player.Object as BasePlayer).inventory;
            if (playerInventory.FindItemID(CodeLockItemId) != null) return true;

            var itemCost = pluginConfig.CodeLockCost;
            var itemDefinition = itemCost.GetItemDefinition();
            if (playerInventory.GetAmount(itemDefinition.itemid) >= itemCost.Amount) return true;

            ReplyToPlayer(player, "Error.InsufficientResources", itemCost.Amount, itemDefinition.displayName.translated);
            return false;
        }

        private bool VerifyOffCooldown(IPlayer player)
        {
            var secondsRemaining = Cooldowns.GetSecondsRemaining(player.Id);
            if (secondsRemaining <= 0) return true;
            ReplyToPlayer(player, "Error.Cooldown", Math.Ceiling(secondsRemaining));
            return false;
        }

        private BaseEntity GetLookEntity(BasePlayer player)
        {
            RaycastHit hit;
            if (!Physics.Raycast(player.eyes.HeadRay(), out hit, 3)) return null;
            return hit.GetEntity();
        }

        private bool IsCarOnLift(ModularCar car)
        {
            RaycastHit hitInfo;
            // This isn't perfect as it can hit other deployables such as rugs
            if (!Physics.SphereCast(car.transform.position + Vector3.up, 1f, Vector3.down, out hitInfo, 1f)) return false;

            var lift = RaycastHitEx.GetEntity(hitInfo) as ModularCarGarage;
            return lift != null && lift.carOccupant == car;
        }

        private CodeLock DeployCodeLockForPlayer(ModularCar car, BasePlayer player, bool isFree = true)
        {
            var driverModule = FindFirstDriverModule(car);
            if (driverModule == null) return null;

            var codeLockItem = player.inventory.FindItemID(CodeLockItemId);
            if (codeLockItem == null && !isFree)
            {
                var itemCost = pluginConfig.CodeLockCost;
                if (itemCost.Amount > 0)
                    player.inventory.Take(null, itemCost.GetItemID(), itemCost.Amount);
            }

            var codeLock = DeployCodeLock(car, driverModule, player.userID);
            if (codeLock == null) return null;

            // Allow other plugins to detect the lock being deployed (e.g., auto lock)
            if (codeLockItem != null)
            {
                Interface.CallHook("OnItemDeployed", codeLockItem.GetHeldEntity(), car);
                if (!isFree)
                    player.inventory.Take(null, CodeLockItemId, 1);
            }
            else
            {
                // Temporarily increase the player inventory capacity to ensure there is enough space
                player.inventory.containerMain.capacity++;
                var temporaryLockItem = ItemManager.CreateByItemID(CodeLockItemId);
                if (player.inventory.GiveItem(temporaryLockItem))
                {
                    Interface.CallHook("OnItemDeployed", temporaryLockItem.GetHeldEntity(), car);
                    temporaryLockItem.RemoveFromContainer();
                }
                temporaryLockItem.Remove();
                player.inventory.containerMain.capacity--;
            }

            return codeLock;
        }

        private CodeLock DeployCodeLock(ModularCar car, VehicleModuleSeating driverModule, ulong ownerID = 0)
        {
            var codeLock = GameManager.server.CreateEntity(CodeLockPrefab, CodeLockPosition, Quaternion.identity) as CodeLock;
            if (codeLock == null) return null;

            if (ownerID != 0)
                codeLock.OwnerID = ownerID;

            codeLock.SetParent(driverModule);
            codeLock.Spawn();
            car.SetSlot(BaseEntity.Slot.Lock, codeLock);

            Effect.server.Run(CodeLockDeployedEffectPrefab, codeLock.transform.position);

            return codeLock;
        }

        private CodeLock GetCarCodeLock(ModularCar car) =>
            car.GetSlot(BaseEntity.Slot.Lock) as CodeLock;

        private bool IsPlayerAuthorizedToCodeLock(BasePlayer player, CodeLock codeLock) =>
            !codeLock.IsLocked() ||
            codeLock.whitelistPlayers.Contains(player.userID) ||
            codeLock.guestPlayers.Contains(player.userID);

        private VehicleModuleSeating FindFirstDriverModule(ModularCar car)
        {
            for (int socketIndex = 0; socketIndex < car.TotalSockets; socketIndex++)
            {
                BaseVehicleModule module;
                if (car.TryGetModuleAt(socketIndex, out module))
                {
                    var seatingModule = module as VehicleModuleSeating;
                    if (seatingModule != null && seatingModule.HasADriverSeat())
                        return seatingModule;
                }
            }
            return null;
        }

        internal class CooldownManager
        {
            private readonly Dictionary<string, float> CooldownMap = new Dictionary<string, float>();
            private readonly float CooldownDuration;

            public CooldownManager(float duration)
            {
                CooldownDuration = duration;
            }

            public void UpdateLastUsedForPlayer(string userID)
            {
                if (CooldownMap.ContainsKey(userID))
                    CooldownMap[userID] = Time.realtimeSinceStartup;
                else
                    CooldownMap.Add(userID, Time.realtimeSinceStartup);
            }

            public float GetSecondsRemaining(string userID)
            {
                if (!CooldownMap.ContainsKey(userID)) return 0;
                return CooldownMap[userID] + CooldownDuration - Time.realtimeSinceStartup;
            }
        }

        #endregion

        #region Configuration

        internal class PluginConfig
        {
            [JsonProperty("AllowDeployOffLift")]
            public bool AllowDeployOffLift = false;

            [JsonProperty("AllowEditingWhileLockedOut")]
            public bool AllowEditingWhileLockedOut = true;

            [JsonProperty("CooldownSeconds")]
            public float CooldownSeconds = 10.0f;

            [JsonProperty("CodeLockCost")]
            public ItemCost CodeLockCost = new ItemCost
            {
                ItemShortName = "metal.fragments",
                Amount = 100,
            };
        }

        internal class ItemCost
        {
            [JsonProperty("ItemShortName")]
            public string ItemShortName;

            [JsonProperty("Amount")]
            public int Amount;

            public ItemDefinition GetItemDefinition() =>
                ItemManager.FindItemDefinition(ItemShortName);

            public int GetItemID() =>
                GetItemDefinition().itemid;
        }

        private PluginConfig GetDefaultConfig() =>
            new PluginConfig();

        protected override void LoadDefaultConfig() =>
            Config.WriteObject(GetDefaultConfig(), true);

        #endregion

        #region Localization

        private void ReplyToPlayer(IPlayer player, string messageName, params object[] args) =>
            player.Reply(string.Format(GetMessage(player, messageName), args));

        private string GetMessage(IPlayer player, string messageName, params object[] args)
        {
            var message = lang.GetMessage(messageName, this, player.Id);
            return args.Length > 0 ? string.Format(message, args) : message;
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Error.NoPermission"] = "You don't have permission to use this command.",
                ["Error.BuildingBlocked"] = "Error: Cannot do that while building blocked.",
                ["Error.NoCarFound"] = "Error: No car found.",
                ["Error.CarDead"] = "Error: That car is dead.",
                ["Error.NotOnLift"] = "Error: That car must be on a lift to receive a lock.",
                ["Error.HasLock"] = "Error: That car already has a lock.",
                ["Error.NoCockpit"] = "Error: That car needs a driver seat to receive a lock.",
                ["Error.InsufficientResources"] = "Error: You need <color=red>{0} {1}</color> to craft a lock.",
                ["Error.Cooldown"] = "Please wait <color=red>{0}s</color> and try again.",
                ["Error.CarLocked"] = "That vehicle is locked.",
            }, this, "en");
        }

        #endregion
    }
}
