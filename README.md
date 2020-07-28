**Modular Car Code Locks** allows players to deploy code locks to Modular Cars.

- Similar to key locks, players that do not have authorization to the code lock cannot access any of the car's features.
- By default, you can only deploy a code lock to a car that is on a lift. This is configurable.
- You cannot deploy a code lock to a car while building blocked.
- If you have a code lock in your inventory, it will be used, or else one will be crafted if you have the resources. Locks can be free with additional permissions.
- When the module that the lock is deployed to is removed at a lift, the lock will automatically be moved to another driver seat module. If all driver seat modules are removed, the lock is removed as well, which is consistent with key lock behavior.
- If you have both a key lock and code lock on a car, you will need both the key and the code to access the car. This is not recommended because it defeats the purpose of having the code lock.

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
- `AllowEditingWhileLockedOut` (`true` or `false`) -- Whether to allow players to edit cars at lifts when they are not authorized to the car's code lock. This is `true` by default to be consistent with how key locks work.
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
- The car has no driver seats
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

Note: The `BasePlayer` parameter may be `null` if another plugin initiated the code lock deployment without providing a player.

#### OnItemDeployed

This is an Oxide hook that is normally called when deploying a code lock or other deployable. It's also explicitly called by this plugin to allow for compatibility with other plugins.

```csharp
void OnItemDeployed(Deployer deployer, BaseEntity entity)
```

Note: The `BaseEntity` parameter will be the `ModularCar` instance.
