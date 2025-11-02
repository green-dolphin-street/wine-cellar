using UnityEngine;

/// <summary>
/// This script procedurally places a grid of Realtime Lights on the ceiling.
/// It MUST have a reference to the ProceduralRoomGenerator to know the room's dimensions.
/// 
/// Attach this script to a child GameObject of your main room (e.g., "Lighting").
/// </summary>
[ExecuteInEditMode]
public class ProceduralLightPlacer : MonoBehaviour
{
    [Header("Required Links")]
    [Tooltip("The generator script that defines the room's size.")]
    public ProceduralRoomGenerator roomGenerator;

    [Tooltip("The REALTIME light prefab (e.g., a Point Light) to be placed. MUST be a prefab.")]
    public GameObject lightPrefab; // Renamed from areaLightPrefab

    [Header("Grid Layout")]
    [Tooltip("Number of lights to place along the room's width (X-axis).")]
    public int lightsAlongWidth = 3;

    [Tooltip("Number of lights to place along the room's depth (Z-axis).")]
    public int lightsAlongDepth = 3;

    [Header("Placement Settings")]
    [Tooltip("How far below the ceiling to place the lights.")]
    public float heightOffset = -0.1f;
    
    [Tooltip("How far from the side walls to start the grid (X-axis).")]
    public float paddingWidth = 2.0f;

    [Tooltip("How far from the front/back walls to start the grid (Z-axis).")]
    public float paddingDepth = 2.0f;


    // Use a "dirty" flag to safely update
    [HideInInspector]
    private bool isDirty = true; 

    private void OnValidate()
    {
        // Clamp values to be reasonable
        lightsAlongWidth = Mathf.Max(1, lightsAlongWidth);
        lightsAlongDepth = Mathf.Max(1, lightsAlongDepth);
        paddingWidth = Mathf.Max(0, paddingWidth);
        paddingDepth = Mathf.Max(0, paddingDepth);
        
        isDirty = true;
    }

    private void Update()
    {
        // Only run if the flag is raised
        if (isDirty)
        {
            GenerateLights();
            isDirty = false;
        }
    }

    /// <summary>
    /// Clears all previously generated lights.
    /// </summary>
    private void ClearLights()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }
    }

    /// <summary>
    /// Generates and places all lights based on the layout parameters.
    /// This new logic places them along the edges only.
    /// </summary>
    public void GenerateLights()
    {
        ClearLights();

        // --- Safety Checks ---
        if (roomGenerator == null)
        {
            Debug.LogWarning("Light Placer: 'Room Generator' is not assigned.", this);
            return;
        }

        if (lightPrefab == null) // Renamed from areaLightPrefab
        {
            Debug.LogWarning("Light Placer: 'Light Prefab' is not assigned.", this);
            return;
        }

        // --- Read Room Dimensions ---
        float roomW = roomGenerator.roomWidth;
        float roomH = roomGenerator.roomHeight;
        float roomD = roomGenerator.roomDepth;

        // --- Calculate Grid Spacing ---
        // Get the total usable space *after* padding
        float usableWidth = roomW - (paddingWidth * 2);
        float usableDepth = roomD - (paddingDepth * 2);

        // Calculate spacing. If count is 1, spacing is 0 (it will be centered).
        float spacingX = (lightsAlongWidth > 1) ? usableWidth / (lightsAlongWidth - 1) : 0;
        float spacingZ = (lightsAlongDepth > 1) ? usableDepth / (lightsAlongDepth - 1) : 0;

        // --- Place Lights ---
        float posY = roomH + heightOffset; // Just below the ceiling

        // --- 1. Place Lights on South and North Edges ---
        float posZ_South = paddingDepth;
        float posZ_North = roomD - paddingDepth;

        for (int x = 0; x < lightsAlongWidth; x++)
        {
            float posX = paddingWidth + (x * spacingX);
            // Handle single light case (center it)
            if (lightsAlongWidth == 1) posX = paddingWidth + usableWidth / 2.0f;
            
            // Place South light
            PlaceLight($"EdgeLight_South_{x}", new Vector3(posX, posY, posZ_South));
            
            // Place North light
            PlaceLight($"EdgeLight_North_{x}", new Vector3(posX, posY, posZ_North));
        }

        // --- 2. Place Lights on West and East Edges (Fillers) ---
        // We loop from 1 to (Count - 1) to AVOID placing lights
        // in the corners, since the loops above already did.
        float posX_West = paddingWidth;
        float posX_East = roomW - paddingWidth;

        for (int z = 1; z < lightsAlongDepth - 1; z++)
        {
            float posZ = paddingDepth + (z * spacingZ);
            
            // Place West light
            PlaceLight($"EdgeLight_West_{z}", new Vector3(posX_West, posY, posZ));
            
            // Place East light
            PlaceLight($"EdgeLight_East_{z}", new Vector3(posX_East, posY, posZ));
        }
    }

    /// <summary>
    /// Helper function to create and position a single light instance.
    /// </summary>
    private void PlaceLight(string name, Vector3 localPosition)
    {
        if (lightPrefab == null) return;

        GameObject newLight = Instantiate(lightPrefab, this.transform);
        newLight.name = name;
        newLight.transform.localPosition = localPosition;
        
        // No need to set light mode, Realtime Point Lights are the default.
    }
}

