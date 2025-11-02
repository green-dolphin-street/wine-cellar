using UnityEngine;

/// <summary>
/// Finds all generated racks from the ProceduralRackPlacer and places
/// a transceiver prefab on each one with a specific offset.
/// 
/// This script should be on its own GameObject (e.g., "Transceivers")
/// and be triggered by the ProceduralRackPlacer.
/// </summary>
[ExecuteInEditMode]
public class ProceduralTransceiverPlacer : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("The transceiver model to be placed.")]
    public GameObject transceiverPrefab;

    [Tooltip("The main rack placer script. This is used to find where the racks are.")]
    public ProceduralRackPlacer rackPlacerScript;

    [Header("Placement Settings")]
    [Tooltip("The local X,Y,Z offset from the rack's center to place the transceiver.")]
    // public Vector3 placementOffset = new Vector3(0, 1.0f, 0.6f); // Top-front by default
    public Vector3 placementOffset = new Vector3(0, 1.0f, 0); // Top-center by default

    // The "dirty" flag. We make this public so the RackPlacer can set it.
    [HideInInspector]
    public bool isDirty = false;

    // We only need OnValidate to set the dirty flag when WE change a value
    // in *this* component.
    private void OnValidate()
    {
        isDirty = true;
    }

    private void Update()
    {
        // Only run in the editor, not in play mode, if dirty.
        if (isDirty && !Application.isPlaying)
        {
            GenerateTransceivers();
            isDirty = false;
        }
    }

    /// <summary>
    /// This function is called by the rack placer to tell us we need to update.
    /// </summary>
    public void SetDirty()
    {
        isDirty = true;
    }

    [ContextMenu("Generate Transceivers")]
    public void GenerateTransceivers()
    {
        ClearTransceivers();

        if (transceiverPrefab == null)
        {
            Debug.LogWarning("Transceiver Placer: No transceiver prefab assigned.", this);
            return;
        }

        if (rackPlacerScript == null)
        {
            Debug.LogWarning("Transceiver Placer: No Rack Placer script assigned.", this);
            return;
        }

        Transform rackContainer = rackPlacerScript.rackContainer;
        if (rackContainer == null)
        {
            Debug.LogWarning("Transceiver Placer: Rack Placer's 'rackContainer' is missing.", this);
            return;
        }

        // --- Loop through all racks and place a transceiver ---
        
        // We iterate through the rackContainer's children
        foreach (Transform rackTransform in rackContainer)
        {
            // Get the rack's position and rotation
            Vector3 rackPosition = rackTransform.position;
            Quaternion rackRotation = rackTransform.rotation;

            // --- Calculate the transceiver's position and rotation ---
            
            // 1. Calculate the offset *relative* to the rack's rotation
            // This ensures the offset (0, 1.0, 0.6) is always "top-front"
            // even if the rack is rotated 180 degrees.
            Vector3 rotatedOffset = rackRotation * placementOffset;
            
            // 2. Add the rotated offset to the rack's world position
            Vector3 transceiverPosition = rackPosition + rotatedOffset;
            
            // 3. The transceiver should have the same rotation as the rack
            Quaternion transceiverRotation = rackRotation;

            // --- Instantiate the transceiver ---
            GameObject newTransceiver = Instantiate(transceiverPrefab, this.transform);
            newTransceiver.name = $"{rackTransform.name}_Transceiver";
            newTransceiver.transform.position = transceiverPosition;
            newTransceiver.transform.rotation = transceiverRotation;
        }
    }

    [ContextMenu("Clear Transceivers")]
    public void ClearTransceivers()
    {
        // We only destroy children of THIS GameObject's transform
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }
    }
}
