**Modular Car Code Locks** allows players to deploy code locks to Modular Cars with a car lift UI or using a command.

Deploying a code lock to a car will consume a lock from the player's inventory if available, or else a lock will be automatically purchased for the configured price. Locks are free for players with additional permission.

Notes:
- Players that do not have authorization to a car's code lock cannot access any of the car's features, including seats, engine parts, storage, and fuel. Entering either the code or guest code grants full access until the code is changed.
  - Authorization may be shared with the lock owner's team, friends, or clanmates based on the plugin configuration, or via compatible sharing plugins. Sharing allows other players to access the car's features without ever entering the code.
- A car must have a cockpit module (i.e., driver seat) to receive a lock. The code lock will deploy to the front-most cockpit module if there are multiple.
- If the lock's parent cockpit is removed, the lock is moved to another cockpit module if present, else destroyed.
- Unauthorized players can remove a car's code lock at a lift via the UI button or by removing all cockpit modules. That can be blocked with a configuration option to make it imposible for unauthorized players to edit the vehicle.
- Removing a code lock from a car via the UI will add the lock to your inventory, unless you have the `carcodelocks.free` permission.
- If a car has both a key lock and code lock, players will need both the key and the code to access the car. This is not recommended.

## UI Screenshots

![Add Code Lock Button](https://i.imgur.com/Xk91dHF.png)
![Remove Code Lock Button](https://i.imgur.com/IT1xsrZ.png)

## Commands

- `carcodelock` (or `ccl`) -- Deploy a code lock to the car you are aiming at. You must be within several meters of the car. You can also aim at a lift to target the car currently on it.
  - You must not be building blocked.
  - By default, the car must be on a lift. This is configurable.

## Permissions

- `carcodelocks.use` -- Allows the player to use the `carcodelocks` command.
- `carcodelocks.ui` -- Allows the player to deploy and remove code locks via UI buttons while editing a car at a lift. This can be granted by itself to only allow deploying code locks this way.
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
  "CooldownSeconds": 10.0,
  "SharingSettings": {
    "Clan": false,
    "ClanOrAlly": false,
    "Friends": false,
    "Team": false
  },
  "UISettings": {
    "AddButtonColor": "0.44 0.54 0.26 1",
    "AnchorMax": "1 0",
    "AnchorMin": "1 0",
    "ButtonTextColor": "0.97 0.92 0.88 1",
    "OffsetMax": "-68 377",
    "OffsetMin": "-255 349",
    "RemoveButtonColor": "0.7 0.3 0 1"
  }
}
```

- `AllowDeployOffLift` (`true` or `false`) -- Whether to allow players to deploy code locks to cars that are not currently on a lift, via the `carcodelock` command. This is `false` by default to be consistent with how key locks work.
- `AllowEditingWhileLockedOut` (`true` or `false`) -- Whether to allow players to edit a car at a lift while they are not authorized to the car's code lock. This is `true` by default to be consistent with how key locks work. Setting this to `false` will make it impossible for unauthorized players to edit the car.
- `CodeLockCost` -- The amount to charge a player when crafting a code lock automatically.
- `CooldownSeconds` -- Cooldown for players to purchase locks, to prevent players from making locks faster than they can craft them. Configure this based on the crafting speed of locks on your server.
- `SharingSettings` (each `true` or `false`) -- Whether to allow players to bypass locks placed by their clanmates, ally clanmates, friends, or teammates. More advanced sharing (such as player's being in control of these settings) can be achieved via compatible sharing plugins.
- `UISettings` -- (Advanced) Control the display of the UI buttons.

## Localization

```json
{
  "UI.AddCodeLock": "Add Code Lock",
  "UI.RemoveCodeLock": "REMOVE Code Lock",
  "Error.NoPermission": "You don't have permission to use this command.",
  "Error.BuildingBlocked": "Error: Cannot do that while building blocked.",
  "Error.NoCarFound": "Error: No car found.",
  "Error.CarDead": "Error: That car is dead.",
  "Error.NotOnLift": "Error: That car must be on a lift to receive a lock.",
  "Error.HasLock": "Error: That car already has a lock.",
  "Error.NoCockpit": "Error: That car needs a cockpit module to receive a lock.",
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
- The car has no cockpit modules
- The car already has a code lock
- Another plugin blocked it with a hook

## Hooks

#### CanDeployCarCodeLock

- Called when a player or a plugin tries to deploy a code lock to a modular car.
- Returning `false` will prevent the code lock from being deployed. If attempted by a player, none of their items will be consumed.
- Returning `null` will result in the default behavior.

```csharp
object CanDeployCarCodeLock(ModularCar car, BasePlayer player)
```

Note: The `BasePlayer` parameter may be `null` if another plugin initiated the code lock deployment without specifying a player.

#### OnItemDeployed

This is an Oxide hook that is normally called when deploying a code lock or other deployable. To allow for compatibility with other plugins, this plugin calls this hook whenever a code lock is deployed to a car for a player.

Note: This is not called when a lock is deployed via another plugin without specifying a player.

```csharp
void OnItemDeployed(Deployer deployer, BaseEntity entity)
{
    // Example: Check if the lock was deployed to a car
    var car = entity as ModularCar;
    if (car == null) return;
    var codeLock = car.GetSlot(BaseEntity.Slot.Lock) as CodeLock;
    if (codeLock != null)
        Puts("A code lock was deployed to a car!");
}
```

#### CanUseLockedEntity

This is an Oxide hook that is normally called when a player attempts to use a locked entity such as a door or box. To allow for compabitility with other plugins, especially sharing plugins, this plugin calls this hook whenever a player tries to access any of a locked car's features, including seats, storage or fuel. This is also called when attempting to edit the vehicle at a lift if the plugin is configured with `AllowEditingWhileLockedOut: false`.

- Not called if the code lock is currently unlocked. This deviates slightly from the Oxide hook which is called for unlocked doors/boxes/cupboards.
- Returning `true` will allow the player to use the car, regardless of whether they are authorized to the code lock. Unless you know what you are doing, you should return `null` instead to avoid potential hook conflicts.
- Returning `false` will prevent the player from using the car, regardless of whether they are authorized to the code lock.
- Returning `null` results in the default behavior.

```csharp
object CanUseLockedEntity(BasePlayer player, CodeLock codeLock)
{
    // Example: Only let the lock owner access the car (not even players who know the code)
    if (codeLock == null) return null;
    var car = (codeLock.GetParentEntity() as VehicleModuleSeating)?.Vehicle as ModularCar;
    if (car == null || car.OwnerID == 0 || car.OwnerID == player.userID) return null;
    return false;
}
```
