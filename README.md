# WINE cellar: HPC Digital Twin for the WINE framework

This Unity project provides a high-fidelity digital twin wireless environment of a High-Performance Computing (HPC) facility. It is a simulation environment designed to be a realistic and rigorous platform for wireless interconnection network (WINE) framework research. It is designed to investigate high-throughput, low-latency wireless link performance within data center environments.

The primary purpose is to model and analyze how key infrastructure design choices, such as 3D room dimensions, rack placement, and Intelligent Reflecting Surfaces (IRS) distribution, dynamically influence wireless propagation. The environment supports the analysis of two WINE link types:
*   **s-WINE**: Direct Line-of-Sight (LoS) links between the transceivers mounted on the racks.
*   **r-WINE**: Non-Line-of-Sight (NLoS) links reflected via IRS elements mounted on the ceiling.

## Workflow

To set up and run a simulation, follow these steps:

1.  **Environment Setup**:
    *   Select the **`MachineRoom`** object to configure the overall room dimensions (`roomWidth`, `roomHeight`, `roomDepth`).
    *   Select the **`Racks`** object to configure rack layout (rows, aisles, counts).
    *   Select the **`Lighting`** object to adjust procedural lighting.

2.  **IRS Configuration**:
    *   Select the **`[IRS]`** object (which uses `ProceduralIRSPlacer.cs`).
    *   Adjust parameters like `elementCountX`, `elementCountZ`, `elementSize`, and `ceilingOffset` to generate the IRS array on the ceiling.
    *   The array will automatically regenerate in the Editor when parameters change.

3.  **Link Configuration**:
    *   Select the **`[LinkManager]`** object (which uses `FSOLinkManager.cs`).
    *   Add items to the **`Links`** list to define your communication paths.
    *   Assign the `Transmitter Index` and `Receiver Index` (corresponding to the rack indices).
    *   **Important**: Configure the type of link (s-WINE vs r-WINE) using the `Reflecting Element Index` (see Configuration section below).

4.  **Simulation**:
    *   You can visualize the beams directly in the Editor by right-clicking the `FSOLinkManager` component and selecting **"Generate/Update All Links"**.
    *   Alternatively, enter **Play Mode**. The links will generate automatically.
    *   The beams (green/red lines) represent the optical paths. If a beam is blocked by an obstacle (rack, wall, etc.), it will turn black, and a blockage message will be logged to the Console.

## Link configuration: s-WINE and r-WINE

The type of wireless link is determined by the `Reflecting Element Index` in the `LinkDefinition` of the `FSOLinkManager`:

### s-WINE (straight-WINE Link)
*   **Configuration**: Set **`Reflecting Element Index` to `-1`**.
*   **Behavior**: The system creates a direct Gaussian beam from the Transmitter's lens to the Receiver's lens.

### r-WINE (reflected-WINE Link)
*   **Configuration**: Set **`Reflecting Element Index` to a valid index** (e.g., `0`, `10`, `48`).
    *   Valid indices range from `0` to `(elementCountX * elementCountZ) - 1`.
*   **Behavior**: The system creates two beam segments:
    1.  Transmitter -> IRS Element (Red beam)
    2.  IRS Element -> Receiver (Red beam)
*   The IRS element acts as an active reflector, redirecting the signal to the intended receiver.

## Scripts Description

*   **`FSOLinkManager.cs`**: The central manager for defining and generating FSO links. It handles the logic for distinguishing between s-WINE and r-WINE and instantiates the beam prefabs.
*   **`FSOBeam.cs`**: Attached to each beam prefab. It handles the visual rendering (LineRenderer) and physics (Collider) of a single beam segment. It detects collisions with obstacles and updates the beam status (e.g., changing color on blockage).
*   **`ProceduralIRSPlacer.cs`**: Procedurally generates the Intelligent Reflecting Surface (IRS) array on the ceiling. It creates the backplane and the grid of individual reflecting elements based on configurable parameters.
*   **`FSOBeamCalculator.cs`**: A static helper class containing the physics equations for Gaussian beams (Rayleigh range, beam radius, divergence, intensity).
*   **`FSOBeamCollider.cs`**: Generates a custom mesh collider for the beam that accurately follows the Gaussian beam profile (expanding with distance). This ensures precise blockage detection.
