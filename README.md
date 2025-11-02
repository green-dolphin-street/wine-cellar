# HPC Digital Twin - Procedural Infrastructure Generator

This Unity project provides a high-fidelity digital twin of a High-Performance Computing (HPC) data center. It is a simulation environment designed to be a realistic and rigorous platform for wireless framework research.

## Research Background

This project serves as a second-generation, high-fidelity simulation testbed for the WINE (Wireless Infrastructure for Network Emulation) framework. The initial concepts and testbed were introduced in our prior work:
> Self et al., "WINE: A Wireless Infrastructure for Network Emulation of Future Data Centers," in *IEEE Communications*, vol. 31, no. 6, pp. 126-134, Dec. 2024.
>
> **DOI:** 10.1109/MWC.018.2300524
>
> **Link:**
>   - https://ieeexplore.ieee.org/document/10787349/
>   - https://arxiv.org/abs/2409.13281

This new, more realistic and rigorous digital twin is designed to investigate high-throughput, low-latency wireless link performance within complex data center environments. The primary purpose of this tool is to model and analyze how key infrastructure design choices dynamically influence wireless propagation. This includes parameters such as the 3D room dimensions, rack placement (which creates complex LOS/NLOS paths), and the spatial distribution of Intelligent Reflecting Surfaces (IRSs) on the ceiling. The environment allows for the analysis of various communication paths (e.g., s-WINE and r-WINE) by providing a geometrically accurate and parametrically-controlled simulation space.

## Features

* **`ProceduralRoomGenerator.cs`**: Creates the main room structure (floor, walls, ceiling) based on `roomWidth`, `roomHeight`, and `roomDepth`.
* **`ProceduralRackPlacer.cs`**: Automatically populates the room with a specified number of racks in a configurable "hot aisle / cold aisle" layout.
* **`ProceduralLightPlacer.cs`**: Dynamically adds realtime Point Lights along the ceiling edges, leaving the central ceiling area open for other equipment (like IRS arrays).
* **Safe Editor-Time Generation**: All procedural scripts use `[ExecuteInEditMode]` combined with an `isDirty` flag pattern. This provides instant, real-time updates in the editor as you change parameters, while avoiding common Unity errors.

## Getting Started & Scene Hierarchy

This project is designed to be used directly from the Unity Editor after cloning. The core logic is pre-configured in the main scene.

1.  **Prerequisites**: Unity (tested with 2022.3 LTS or later) with the Universal Render Pipeline (URP).
2.  **Open Scene**: Open the project and load the main scene from the `Assets/Scenes/` folder.
3.  **Configure**: Select the `GameObject`s in the Hierarchy window to view and adjust their parameters in the Inspector.

The scene is organized around a parent-child structure:

* **`MachineRoom`** (Root Object)
    * **Script**: `ProceduralRoomGenerator.cs`
    * **Purpose**: This is the "brain" of the digital twin. Adjust parameters here to control the overall room dimensions.
* **`RoomGeometry`** (Child of `MachineRoom`)
    * **Purpose**: This object is auto-generated and managed by the `ProceduralRoomGenerator`. It holds the physical wall, floor, and ceiling meshes. Do not modify directly.
* **`Racks`** (Child of `MachineRoom`)
    * **Script**: `ProceduralRackPlacer.cs`
    * **Purpose**: Controls all rack layout parameters (total count, rows, aisle spacing, etc.). The required `Rack_Prefab_Base` and `Rack_Material` are assigned here.
* **`Lighting`** (Child of `MachineRoom`)
    * **Script**: `ProceduralLightPlacer.cs`
    * **Purpose**: Controls the placement of the perimeter lights. The `Point Light` prefab is assigned here.

## Configuration & Key Parameters

All parameters can be adjusted live in the Unity Inspector. The environment will regenerate automatically.

### Key Asset Links

* **Rack Prefab & Material**: Located in `Assets/Prefabs/` and `Assets/Materials/`. These are assigned to the slots on the `Racks` GameObject.
* **Light Prefab**: Located in `Assets/Prefabs/`. Assigned to the slot on the `Lighting` GameObject.

### Key Adjustable Parameters (Metric)

* **Room** (on `MachineRoom`):
    * `roomWidth`
    * `roomDepth`
    * `roomHeight`
    * (e.g., 16.0m, 12.8m, 3.5m)
* **Racks** (on `Racks`):
    * `rackWidth`
    * `rackDepth`
    * `rackHeight`
    * (e.g., 0.6m, 1.2m, 2.0m)
* **Layout** (on `Racks`):
    * `totalRacks`
    * `racksPerRow`
    * `coldAisleWidth`
    * `hotAisleWidth`
    * `layoutStartOffset`
    * (e.g., 100, 20, 1.2m, 1.2m, 2.0m)
