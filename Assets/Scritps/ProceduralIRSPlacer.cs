using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Procedurally generates an Intelligent Reflecting Surface (IRS) array.
/// This creates a backplane (the "board") and populates it with a grid
/// of "elements" (the small square units).
/// Runs in the editor and updates on value change.
/// </summary>
[ExecuteInEditMode]
public class ProceduralIRSPlacer : MonoBehaviour
{
    [Header("Required Links")]
    [Tooltip("The generator script that defines the room's size.")]
    public ProceduralRoomGenerator roomGenerator;

    [Header("IRS Array Configuration")]
    [Tooltip("How far below the ceiling to place the array (Y-axis offset).")]
    public float ceilingOffset = -0.01f; // 1cm below ceiling

    [Tooltip("Number of elements in the X-dimension (width)")]
    public int elementCountX = 7;
    [Tooltip("Number of elements in the Z-dimension (depth)")]
    public int elementCountZ = 7;

    [Header("Element Properties")]
    [Tooltip("The size (width and depth) of a single square element in meters")]
    public float elementSize = 0.5f; // 10cm
    [Tooltip("The spacing between adjacent elements in meters")]
    public float elementSpacing = 0.5f; // 1cm

    [Header("Physical Properties")]
    [Tooltip("The padding/margin from the edge of the element grid to the edge of the backplane.")]
    public float backplaneMargin = 0.05f; // 5cm margin
    [Tooltip("The thickness of the main backplane in meters")]
    public float backplaneThickness = 0.1f; // 10cm
    [Tooltip("The thickness of the small elements in meters")]
    public float elementThickness = 0.03f; // 3cm

    [Header("Materials")]
    public Material backplaneMaterial;
    public Material elementMaterial;

    // --- Private ---
    private Transform geometryContainer;
    
    // Flag to trigger regeneration
    // Made public to be triggered by other scripts
    [HideInInspector]
    public bool isDirty = true; 

#if UNITY_EDITOR
    // Called when a value is changed in the Inspector
    private void OnValidate()
    {
        isDirty = true;
    }

    private void Update()
    {
        if (Application.isPlaying) return;

        // Check if we need to regenerate and are in the editor
        if (isDirty)
        {
            GenerateIRSArray();
            isDirty = false;
        }
    }
#endif

    /// <summary>
    /// Clears all previously generated IRS geometry.
    /// </summary>
    private void ClearIRS()
    {
        if (geometryContainer == null)
        {
            // Try to find an existing container
            geometryContainer = transform.Find("IRS_Array_Geometry");
            if (geometryContainer == null)
            {
                return; // Nothing to clear
            }
        }

        // Destroy all children of the container
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
    /// Generates the full IRS array (backplane and elements).
    /// </summary>
    public void GenerateIRSArray()
    {
        // --- NEW: Safety check for Room Generator ---
        if (roomGenerator == null)
        {
            // Don't log a warning, it might not be assigned yet
            return;
        }

        // --- NEW: Calculate Center Position from Room ---
        Vector3 arrayCenterPosition = new Vector3(
            roomGenerator.roomWidth / 2.0f,
            roomGenerator.roomHeight + ceilingOffset,
            roomGenerator.roomDepth / 2.0f
        );

        // Find or create the container for our geometry
        geometryContainer = transform.Find("IRS_Array_Geometry");
        if (geometryContainer == null)
        {
            geometryContainer = new GameObject("IRS_Array_Geometry").transform;
            geometryContainer.SetParent(this.transform);
            geometryContainer.localPosition = Vector3.zero;
            geometryContainer.localRotation = Quaternion.identity;
        }

        // Clear any old geometry first
        ClearIRS();

        // --- 1. Calculate Total Dimensions ---
        // First, calculate the size of the *element grid*
        float totalGridWidth = (elementCountX * elementSize) + (Mathf.Max(0, elementCountX - 1) * elementSpacing);
        float totalGridDepth = (elementCountZ * elementSize) + (Mathf.Max(0, elementCountZ - 1) * elementSpacing);

        // Now, calculate the size of the *backplane* by adding the margin
        float totalBackplaneWidth = totalGridWidth + (backplaneMargin * 2);
        float totalBackplaneDepth = totalGridDepth + (backplaneMargin * 2);

        // --- 2. Create the Backplane ---
        GameObject backplane = GameObject.CreatePrimitive(PrimitiveType.Cube);
        backplane.name = "IRS_Backplane";
        backplane.transform.SetParent(geometryContainer);
        // Use the new backplane dimensions
        backplane.transform.localScale = new Vector3(totalBackplaneWidth, backplaneThickness, totalBackplaneDepth);
        
        // Position the backplane at the center, with its top surface at the center's Y
        backplane.transform.position = arrayCenterPosition - new Vector3(0, backplaneThickness / 2.0f, 0);

        backplane.tag = "Obstacle";

        if (backplaneMaterial != null)
        {
            backplane.GetComponent<Renderer>().material = backplaneMaterial;
        }

        // --- 3. Create the Elements ---
        
        // Find the starting corner to begin placing elements
        // This is based on the *grid* size, not the backplane size, to keep it centered.
        Vector3 startOffset = new Vector3(-totalGridWidth / 2.0f, 0, -totalGridDepth / 2.0f);
        // We only care about X and Z for the start, Y will be set manually.
        Vector3 elementStartPos = arrayCenterPosition + startOffset + new Vector3(elementSize / 2.0f, 0, elementSize / 2.0f);

        // --- NEW: Calculate the Y-position for all elements ---
        // The top of the backplane is at 'arrayCenterPosition.y'.
        // The bottom of the backplane is 'backplaneThickness' below that.
        // The element's center is 'elementThickness / 2.0f' below its own top.
        // So, we place the element's center at the backplane bottom, minus half its own thickness.
        float elementYPos = (arrayCenterPosition.y - backplaneThickness) - (elementThickness / 2.0f);

        for (int x = 0; x < elementCountX; x++)
        {
            for (int z = 0; z < elementCountZ; z++)
            {
                GameObject element = GameObject.CreatePrimitive(PrimitiveType.Cube);
                element.name = $"Element_{x}_{z}";
                element.transform.SetParent(geometryContainer);

                // Set scale
                element.transform.localScale = new Vector3(elementSize, elementThickness, elementSize);

                // Set position
                float xPos = elementStartPos.x + x * (elementSize + elementSpacing);
                float zPos = elementStartPos.z + z * (elementSize + elementSpacing);
                element.transform.position = new Vector3(xPos, elementYPos, zPos);

                element.tag = "IRS_Element";
                // Apply material
                if (elementMaterial != null)
                {
                    element.GetComponent<Renderer>().material = elementMaterial;
                }
            }
        }
    }

    /// <summary>
    /// Retrieves the IRS element at the specified linear index.
    /// Elements are generated in row-major order (X then Z).
    /// </summary>
    public Transform GetIRSElement(int index)
    {
        if (geometryContainer == null)
        {
            geometryContainer = transform.Find("IRS_Array_Geometry");
        }
        
        // Filter for just the elements, as the backplane is also a child
        List<Transform> elements = new List<Transform>();
        foreach(Transform child in geometryContainer)
        {
            if (child.tag == "IRS_Element")
            {
                elements.Add(child);
            }
        }

        if (index >= 0 && index < elements.Count)
        {
            return elements[index];
        }
        
        return null;
    }
}