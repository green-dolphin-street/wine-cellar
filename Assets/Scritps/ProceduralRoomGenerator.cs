using UnityEngine;

/// <summary>
/// This script procedurally generates the geometry for a room (walls, floor, ceiling).
/// It creates and manages its own child object ("RoomGeometry") to avoid
/// destroying other objects (like Lighting or Racks) that are children of this GameObject.
/// </summary>
[ExecuteInEditMode]
public class ProceduralRoomGenerator : MonoBehaviour
{
    [Header("Room Dimensions")]
    [Tooltip("Room width on the X-axis.")]
    public float roomWidth = 10f;
    [Tooltip("Room height on the Y-axis.")]
    public float roomHeight = 3f;
    [Tooltip("Room depth on the Z-axis.")]
    public float roomDepth = 15f;

    [Header("Construction Settings")]
    [Tooltip("The material to apply to all room surfaces.")]
    public Material roomMaterial;
    [Tooltip("The thickness of the walls, floor, and ceiling.")]
    public float structureThickness = 0.1f;
    
    // --- Private ---
    // We will now manage all geometry in this container
    private Transform geometryContainer;
    
    // Use a "dirty" flag to safely update
    [HideInInspector]
    private bool isDirty = true; 

    private void OnValidate()
    {
        // Clamp values to be reasonable
        roomWidth = Mathf.Max(0.1f, roomWidth);
        roomHeight = Mathf.Max(0.1f, roomHeight);
        roomDepth = Mathf.Max(0.1f, roomDepth);
        structureThickness = Mathf.Max(0.01f, structureThickness);
        
        // Flag that we need to regenerate
        isDirty = true;
    }

    private void Update()
    {
        // Only run if the flag is raised
        if (isDirty)
        {
            GenerateRoom();
            isDirty = false;
        }
    }

    /// <summary>
    /// Finds or creates the "RoomGeometry" container and populates it.
    /// </summary>
    public void GenerateRoom()
    {
        // Find or create the geometry container
        geometryContainer = transform.Find("RoomGeometry");
        if (geometryContainer == null)
        {
            GameObject geoGO = new GameObject("RoomGeometry");
            geometryContainer = geoGO.transform;
            geometryContainer.SetParent(this.transform);
            geometryContainer.localPosition = Vector3.zero;
            geometryContainer.localRotation = Quaternion.identity;
            geometryContainer.localScale = Vector3.one;
        }

        // Clear the *old geometry*
        ClearRoom();

        // Create new room parts.
        // These calculations place the (0,0,0) point at the
        // internal "south-west" corner of the room floor.

        // Floor
        Vector3 floorPos = new Vector3(roomWidth / 2f, -structureThickness / 2f, roomDepth / 2f);
        Vector3 floorScale = new Vector3(roomWidth, structureThickness, roomDepth);
        CreateRoomPart("Floor", floorPos, floorScale);

        // Ceiling
        Vector3 ceilPos = new Vector3(roomWidth / 2f, roomHeight + structureThickness / 2f, roomDepth / 2f);
        Vector3 ceilScale = new Vector3(roomWidth, structureThickness, roomDepth);
        CreateRoomPart("Ceiling", ceilPos, ceilScale);

        // Wall West (-X)
        Vector3 westPos = new Vector3(-structureThickness / 2f, roomHeight / 2f, roomDepth / 2f);
        Vector3 westScale = new Vector3(structureThickness, roomHeight, roomDepth);
        CreateRoomPart("Wall_West", westPos, westScale);

        // Wall East (+X)
        Vector3 eastPos = new Vector3(roomWidth + structureThickness / 2f, roomHeight / 2f, roomDepth / 2f);
        Vector3 eastScale = new Vector3(structureThickness, roomHeight, roomDepth);
        CreateRoomPart("Wall_East", eastPos, eastScale);

        // Wall South (-Z)
        Vector3 southPos = new Vector3(roomWidth / 2f, roomHeight / 2f, -structureThickness / 2f);
        Vector3 southScale = new Vector3(roomWidth + (structureThickness * 2), roomHeight, structureThickness);
        CreateRoomPart("Wall_South", southPos, southScale);

        // Wall North (+Z)
        Vector3 northPos = new Vector3(roomWidth / 2f, roomHeight / 2f, roomDepth + structureThickness / 2f);
        Vector3 northScale = new Vector3(roomWidth + (structureThickness * 2), roomHeight, structureThickness);
        CreateRoomPart("Wall_North", northPos, northScale);
    }

    /// <summary>
    /// Clears all previously generated room parts.
    /// </summary>
    private void ClearRoom()
    {
        if (geometryContainer == null) return;

        for (int i = geometryContainer.childCount - 1; i >= 0; i--)
        {
            GameObject child = geometryContainer.GetChild(i).gameObject;
            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }
    }

    /// <summary>
    /// Creates a single part of the room (e.g., a wall).
    /// </summary>
    private void CreateRoomPart(string name, Vector3 position, Vector3 scale)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name;
        
        // --- MODIFIED: Parent to the container ---
        part.transform.SetParent(geometryContainer);

        part.transform.localPosition = position;
        part.transform.localScale = scale;

        // Manage collider
        Collider col = part.GetComponent<Collider>();
        if (col != null)
        {
            if (Application.isPlaying)
                Destroy(col);
            else
                // We are in Update(), so this is safe.
                DestroyImmediate(col);
        }

        // Apply material
        Renderer rend = part.GetComponent<Renderer>();
        if (rend != null && roomMaterial != null)
        {
            rend.material = roomMaterial;
        }
    }
}

