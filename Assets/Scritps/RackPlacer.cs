using UnityEngine;

/// <summary>
/// This script procedurally places racks within a room in a
/// hot/cold aisle configuration. It's designed to run in the editor
/// and uses a "dirty" flag to avoid OnValidate errors.
/// 
/// Attach this to its own empty GameObject (e.g., "Racks") parented
/// to the main MachineRoom.
/// </summary>
[ExecuteInEditMode]
public class ProceduralRackPlacer : MonoBehaviour
{
    [Header("Rack Definition")]
    [Tooltip("The prefab to use for each rack. A simple cube is fine.")]
    public GameObject rackPrefab;

    [Tooltip("The material to apply to the racks.")]
    public Material rackMaterial;

    [Header("Rack Dimensions (Meters)")]
    public float rackWidth = 0.6f; // This is the most common standard for a single rack. (800mm wide racks also exist, but 600mm is the baseline).
    public float rackHeight = 2.0f; // This is the height of a standard 42U (42-rack-unit) cabinet. This is the "usable space" inside; the external chassis is slightly taller.
    public float rackDepth = 1.2f; // For high-performance servers (HPC), you need deep racks to fit the servers, power supplies, and internal cabling. 1000mm (1.0m) is also common, but 1.2m is a safer bet for a digital twin.

    [Header("Layout Parameters")]
    [Tooltip("The total number of racks to place.")]
    public int totalRacks = 100;

    [Tooltip("The number of racks to place side-by-side in a single row. (The 'cage row')")]
    public int racksPerRow = 10;

    [Header("Aisle Dimensions (Meters)")]
    [Tooltip("Width of the 'Cold' aisle (where air intakes face).")]
    public float coldAisleWidth = 1.2f; // a very common standard, about 4ft

    [Tooltip("Width of the 'Hot' aisle (where exhausts face).")]
    public float hotAisleWidth = 1.2f; // a very common standard, about 4ft

    [Header("Layout Offset (Meters)")]
    [Tooltip("Padding from the room's (0,0,0) corner to start placing racks.")]
    public Vector3 layoutStartOffset = new Vector3(2.0f, 0, 2.0f);

    // "Dirty" flag to safely update from OnValidate
    [HideInInspector]
    private bool isDirty = true;

    // Called in the editor when a value is changed
    private void OnValidate()
    {
        // Clamp values to be reasonable
        rackWidth = Mathf.Max(0.1f, rackWidth);
        rackHeight = Mathf.Max(0.1f, rackHeight);
        rackDepth = Mathf.Max(0.1f, rackDepth);
        totalRacks = Mathf.Max(1, totalRacks);
        racksPerRow = Mathf.Max(1, racksPerRow);
        coldAisleWidth = Mathf.Max(0.1f, coldAisleWidth);
        hotAisleWidth = Mathf.Max(0.1f, hotAisleWidth);
        layoutStartOffset.y = Mathf.Max(0, layoutStartOffset.y);
        
        // Flag that we need to regenerate
        isDirty = true;
    }

    // Update is called in the editor (and play mode)
    private void Update()
    {
        // Only run if the flag is raised
        if (isDirty)
        {
            GenerateRacks();
            isDirty = false;
        }
    }

    /// <summary>
    /// Clears all previously generated racks (children of this object).
    /// </summary>
    private void ClearRacks()
    {
        // This loop is now safe because it's called from Update()
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
    /// Generates and places all racks based on the layout parameters.
    /// </summary>
    public void GenerateRacks()
    {
        ClearRacks();

        if (rackPrefab == null)
        {
            Debug.LogWarning("Rack Placer: No rack prefab assigned.", this);
            return;
        }

        // --- Layout Calculation ---
        float currentX = 0;
        float currentZ = layoutStartOffset.z; // Start Z at the offset
        int rowCount = 0;
        
        // Place the racks *on top* of the floor (Y=0)
        float yPos = layoutStartOffset.y + (rackHeight / 2.0f);

        for (int i = 0; i < totalRacks; i++)
        {
            // --- Position Calculation ---
            int col = i % racksPerRow;
            // X position is offset + (column * width)
            currentX = layoutStartOffset.x + (col * rackWidth);
            
            // Rack's pivot is in its center, so offset by half-dimensions
            Vector3 position = new Vector3(currentX + (rackWidth / 2.0f), yPos, currentZ + (rackDepth / 2.0f));
            Quaternion rotation = Quaternion.identity; // Facing forward (+Z)

            // Every even row (0, 2, 4...) faces "forward"
            // Every odd row (1, 3, 5...) faces "backward" (180 deg)
            if (rowCount % 2 != 0)
            {
                rotation = Quaternion.Euler(0, 180, 0);
            }

            // --- Instantiate & Configure Rack ---
            GameObject newRack = Instantiate(rackPrefab, this.transform);
            newRack.name = $"Rack_{i:D3}"; // e.g., Rack_001
            newRack.transform.localPosition = position;
            newRack.transform.localRotation = rotation;
            
            // Set scale from our variables
            newRack.transform.localScale = new Vector3(rackWidth*0.94f, rackHeight, rackDepth); // Slightly narrower widths to distinguish each rack

            // Apply material
            Renderer rend = newRack.GetComponent<Renderer>();
            if (rend != null && rackMaterial != null)
            {
                rend.material = rackMaterial;
            }

            // --- Advance to next position ---
            bool isLastRackInRow = (i + 1) % racksPerRow == 0;

            if (isLastRackInRow)
            {
                // We've finished a row. Move Z to the next row position.
                rowCount++;

                // Add the depth of the row we just placed
                currentZ += rackDepth;

                // Now add the correct aisle width
                if (rowCount % 2 != 0)
                {
                    // Just finished row 0, 2, 4... Add a HOT aisle.
                    currentZ += hotAisleWidth;
                }
                else
                {
                    // Just finished row 1, 3, 5... Add a COLD aisle.
                    currentZ += coldAisleWidth;
                }
            }
        }
    }
}