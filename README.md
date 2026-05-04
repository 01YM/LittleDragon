# LittleDragon

## Overview

**LittleDragon** is a 2D platformer game prototype developed in Unity. The game features a dragon character as the playable protagonist, navigating through various environments including land, air, and underwater settings. Players control the dragon to explore, fight enemies, and overcome obstacles in a side-scrolling adventure.

This project serves as a prototype demonstrating advanced 2D character movement mechanics, combat systems, enemy AI, and environmental interactions using Unity's Universal Render Pipeline (URP) and Input System.

## Features

### Core Gameplay
- **Multi-Terrain Movement**: Seamless transitions between walking/running on land, flying in the air, and swimming underwater.
- **Combat System**: Attack and kick mechanics with cooldowns, damage dealing, and animation triggers.
- **Enemy AI**: Intelligent mushroom enemies that patrol, detect players, chase, and attack.
- **Physics-Based Interactions**: Realistic movement using Unity's 2D physics, including gravity, acceleration, and collision detection.

### Character Abilities (Dragon)
- **Ground Movement**: Walking, running, crawling, and jumping with coyote time and jump buffering.
- **Flight Mechanics**: Double-jump to enter flight mode, flap controls for upward bursts, and gliding.
- **Underwater Swimming**: Surface sticking, buoyancy control, and directional swimming with damping.
- **Combat**: Trigger-based attacks and kicks with visual feedback.

### Enemy Behaviors (Mushroom)
- **Patrol Pattern**: Alternates between walking and idling, flipping direction at edges or boundaries.
- **Detection and Chase**: Detects player within a radius and switches to chase mode.
- **Attack**: Melee attacks with cooldown and contact damage.

### Technical Features
- **Unity Input System**: Modern input handling for keyboard/mouse and potential controller support.
- **Universal Render Pipeline (URP)**: High-quality 2D rendering with post-processing effects.
- **Animator Integration**: State-driven animations for characters and enemies.
- **Layer-Based Collision**: Separate layers for ground, water, players, and enemies.
- **Debug Tools**: Optional debug logging and gizmos for development.

## Project Structure

```
LittleDragon/
├── Assets/
│   ├── Art/                 # Sprites, textures, and visual assets
│   ├── Audio/               # Sound effects and music
│   ├── Materials/           # Unity materials for rendering
│   ├── Prefabs/             # Reusable game objects
│   ├── Scenes/              # Unity scene files
│   │   ├── Level_Prototype.unity      # Main land level prototype
│   │   └── Underwater_Prototype.unity # Underwater level prototype
│   ├── Scripts/             # C# scripts
│   │   ├── Characters/      # Character-related scripts
│   │   │   ├── Dragon/      # Player dragon scripts
│   │   │   │   ├── DragonAnimDriver.cs    # Animation driver
│   │   │   │   ├── DragonCombat.cs        # Combat mechanics
│   │   │   │   └── DragonMover.cs         # Movement and physics
│   │   │   └── Mushroom/    # Enemy mushroom scripts
│   │   │       ├── EnemyHealth.cs         # Health and damage
│   │   │       └── MushroomAI.cs          # AI behavior
│   │   ├── Managers/        # Game management scripts (currently empty)
│   │   ├── Systems/         # Core systems (currently empty)
│   │   └── InputSystem_Actions.cs  # Generated input actions
│   ├── Settings/            # Game settings and configurations
│   ├── Sprites/             # 2D sprite assets
│   ├── UI/                  # User interface elements
│   └── UniversalRenderPipelineGlobalSettings.asset  # URP settings
├── Packages/                # Unity package dependencies
├── ProjectSettings/         # Unity project configuration
│   ├── ProjectSettings.asset # Main project settings
│   ├── InputManager.asset    # Legacy input (if used)
│   ├── Physics2DSettings.asset # 2D physics configuration
│   └── ...                   # Other settings files
├── Library/                 # Unity-generated library files (ignored in git)
├── Temp/                    # Temporary files (ignored in git)
├── Logs/                    # Unity logs
├── UserSettings/            # User-specific settings
├── .vscode/                 # VS Code workspace settings
├── .gitignore               # Git ignore rules
├── LittleDragon.sln         # Visual Studio solution
└── *.csproj                 # C# project files for Unity assemblies
```

## Installation and Setup

### Prerequisites
- **Unity Version**: Unity 2021.3 or later (recommended: Unity 2022+ for full URP support).
- **Operating System**: Windows, macOS, or Linux.
- **Hardware**: A computer capable of running Unity Editor.

### Steps
1. **Clone or Download the Project**:
   - Download the project zip or clone from repository.
   - Extract to a folder (e.g., `C:\Users\YM\Unity\LittleDragon`).

2. **Open in Unity**:
   - Launch Unity Hub.
   - Add the project folder.
   - Open the project in Unity Editor.

3. **Install Dependencies**:
   - Unity will automatically resolve packages from `Packages/manifest.json`.
   - Ensure URP is installed via Package Manager if not present.

4. **Configure Input System**:
   - The project uses `InputSystem_Actions.inputactions`.
   - If needed, regenerate C# class via Window > Input System > Generate C# Class.

5. **Build Settings**:
   - Go to File > Build Settings.
   - Select platform (e.g., PC, Mac & Linux Standalone).
   - Add scenes: `Assets/Scenes/Level_Prototype.unity` and `Assets/Scenes/Underwater_Prototype.unity`.

## How to Run

1. **In Unity Editor**:
   - Open a scene (e.g., `Level_Prototype`).
   - Press Play button in the toolbar.

2. **Build and Run**:
   - Build the project (File > Build and Run).
   - Launch the executable.

### Controls
- **Movement**: WASD or Arrow Keys (Horizontal movement).
- **Jump/Fly**: Space (Tap for jump, double-tap for flight, hold for glide).
- **Run**: Left Shift (while moving).
- **Crawl/Hide**: Down Arrow (with horizontal for crawl, alone for hide).
- **Attack**: Left Mouse Button or assigned key.
- **Kick**: Right Mouse Button or assigned key.

## Development Notes

### Key Scripts Breakdown
- **DragonMover.cs**: Handles all movement logic, including state management for ground, air, and water.
- **DragonCombat.cs**: Manages combat inputs, animations, and damage.
- **MushroomAI.cs**: Finite state machine for enemy behavior (Patrol, Idle, Chase).
- **InputSystem_Actions.cs**: Auto-generated from input actions asset.

### Customization
- Adjust movement speeds, jump forces, and cooldowns in script inspectors.
- Modify animator parameters for new animations.
- Add new enemies by extending the AI pattern.

### Known Issues
- Prototype stage: Some features may be incomplete.
- Debug logs can be toggled in scripts for performance.

## Contributing

This is a personal prototype project. For contributions:
- Fork the repository.
- Make changes in a branch.
- Submit a pull request with detailed descriptions.

## License

This project is for educational and prototyping purposes. No specific license applied.

## Credits

- Developed using Unity Engine.
- Assets and scripts created for this prototype.
- Inspired by classic 2D platformers.

For questions or feedback, contact the developer.