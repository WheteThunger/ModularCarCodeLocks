**Modular Car Code Locks** allows players to deploy code locks to Modular Cars.

Players can use a command to a deploy a code lock to the car they are aiming at. A lock will be consumed from the player's inventory if available, or else a lock will be automatically purchased for the configured price. Locks are free for players with additional permission. Players that do not have authorization to the car's code lock cannot access any of the car's features.

Requirements to place a code lock:
- The player must have the `carcodelocks.use` permission.
- The player must not be building blocked.
- By default, the car must be on a lift. This is configurable.
- The car must have a cockpit module (i.e., driver seat). The code lock will deploy to the front-most cockpit module if there are multiple.

Notes:
- When the cockpit module that the lock is deployed to is removed, such as at a lift or by another plugin, the lock will automatically be moved to another cockpit module. If all cockpit modules are removed, the lock is removed as well. This behavior is consistent with key locks.
- Code locks can be removed by anyone while unlocked. Unauthorized players can only remove a car's code lock by removing all cockpit modules. That can be blocked with a configuration option to make it imposible for unauthorized players to edit the vehicle or remove the code lock while it's locked.
- If you have both a key lock and code lock on a car, you will need both the key and the code to access the car. This is not recommended.

## Commands

- `carcodelock` (or `ccl`) -- Deploy a code lock to the car you are aiming at. You must be within several meters of the car. You can also aim at a lift to target the car currently on it.

## Permissions

- `carcodelocks.use` -- Allows the player to deploy code locks to cars.
- `carcodelocks.free` -- Allows the player to deploy code locks to cars without consuming locks or resources from their inventory.

## Configuration
```json
{
  "AllowDeployOffLift": false,
  "AllowEditingWhileLockedOut": true,
  "CodeLockCost": {
    "Amount": 100,
    "ItemShortName": "metal.fragments"
  },
  "CooldownSeconds": 10.0
}
```

- `AllowDeployOffLift` (`true` or `false`) -- Whether to allow players to deploy code locks to cars that are not currently on a lift. This is `false` by default to be consistent with how key locks work.
- `AllowEditingWhileLockedOut` (`true` or `false`) -- Whether to allow players to edit a car at a lift when they are locked out of the code lock (i.e., when they are not authorized to the lock). This is `true` by default to be consistent with how key locks work. Setting this to `false` will make it impossible for unauthorized players to edit the car.
- `CodeLockCost` -- The amount to charge a player when crafting a code lock automatically.
- `CooldownSeconds` -- Cooldown to prevent players from using this plugin to make locks faster than the game naturally allows with crafting. Configure this based on the crafting speed of locks on your server.

## Localization

```json
{
  "Error.NoPermission": "You don't have permission to use this command.",
  "Error.BuildingBlocked": "Error: Cannot do that while building blocked.",
  "Error.NoCarFound": "Error: No car found.",
  "Error.CarDead": "Error: That car is dead.",
  "Error.NotOnLift": "Error: That car must be on a lift to receive a lock.",
  "Error.HasLock": "Error: That car already has a lock.",
  "Error.NoCockpit": "Error: That car needs a driver seat to receive a lock.",
  "Error.InsufficientResources": "Error: You need <color=red>{0} {1}</color> to craft a lock.",
  "Error.Cooldown": "Please wait <color=red>{0}s</color> and try again.",
  "Error.CarLocked": "That vehicle is locked."
}
```

## Developer API

#### API_DeployCodeLock

Plugins can call this API to deploy a code lock to a modular car. The `BasePlayer` parameter is optional, but providing it is recommended as it allows for potential compatibility with auto-lock plugins. Deploying a lock this way will not consume any of the player's items.

```csharp
CodeLock API_DeployCodeLock(ModularCar car, BasePlayer player)
```

The return value will be the newly deployed lock, or `null` if a lock was not deployed for any of the following reasons.
- The car was destroyed or is "dead"
- The car has no cockpits
- The car already has a code lock
- Another plugin blocked it with a hook

## Hooks

#### CanDeployCarCodeLock

- Called when a player or a plugin tries to deploy a code lock to a modular car.
- Returning `false` will prevent the code lock from being deployed. If attempted by a player, none of their items will be consumed.
- Returning `null` results in the default behavior.

```csharp
bool CanDeployCarCodeLock(ModularCar car, BasePlayer player)
```

Note: The `BasePlayer` parameter may be `null` if another plugin initiated the code lock deployment without specifying a player.

#### OnItemDeployed

This is an Oxide hook that is normally called when deploying a code lock or other deployable. It's also explicitly called by this plugin to allow for compatibility with other plugins.

```csharp
void OnItemDeployed(Deployer deployer, BaseEntity entity)
```

Note: The `BaseEntity` parameter will be the `ModularCar` instance.
