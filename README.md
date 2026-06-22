# Third-Person Shooters Prototype Game

Prototype game third-person shooter berbasis Unity dengan sistem senjata, melee combat, enemy AI, inventory grid, loot, status effect, dan health/shield.

## Requirements

- Unity `6000.4.0f1`
- Input System
- Cinemachine
- TextMeshPro
- Lean Pool

## Features

- Third-person camera, aiming, scope, recoil, dan IK tangan saat membidik.
- Senjata data-driven menggunakan ScriptableObject: handgun, revolver, shotgun, AK-47, dan sniper.
- Sistem magazine, ammo inventory, reload, chamber round, shotgun shell reload, dan weapon switching.
- Melee combat untuk unarmed, one-hand, dan two-hand weapon.
- Player health, delayed regeneration, stamina, shield, block, knockback, dan death state.
- Inventory grid dengan item berukuran lebih dari satu slot, drag and drop, quick slot, pickup, dan drop item.
- Consumable item, status effect, buff, debuff, poison, regeneration, dan shield stack.
- Enemy AI untuk patrol, chase, melee, ranged combat, support, shield behavior, aura, loot, dan death ritual.
- Enemy health bar, regeneration bar, shield bar, serta status icon di world space.
- Object pooling dengan Lean Pool untuk enemy, projectile effect, bullet shell, dan item drop.

## Project Structure

```text
Assets/
  Scenes/             Main gameplay scene
  Scripts/            Gameplay, UI, AI, combat, inventory
  Resource/           Animator controller, masks, prefabs, effects
  Scripts/
    ScripTableObject/ Weapon, ammo, enemy, loot, and status-effect data
Packages/             Unity package dependencies
ProjectSettings/      Unity project configuration
