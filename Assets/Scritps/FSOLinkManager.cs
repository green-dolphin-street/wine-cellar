using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages the creation and updating of all FSO links in the scene.
/// Attach this to a central manager object (e.g., "[LinkManager]").
/// </summary>
public class FSOLinkManager : MonoBehaviour
{
    [System.Serializable]
    public class LinkDefinition
    {
        public string name;
        [Tooltip("The index of the transmitting rack (e.g., 0 for the first rack).")]
        public int transmitterIndex;
        [Tooltip("The index of the receiving rack (e.g., 5 for the sixth rack).")]
        public int receiverIndex;
    }

    [Header("Link Setup")]
    [Tooltip("The prefab that contains the GaussianBeam and LineRenderer components.")]
    public GameObject beamPrefab;
    
    [Header("Dependencies")]
    [Tooltip("The script that contains all the generated transceiver objects.")]
    public ProceduralTransceiverPlacer transceiverPlacer;
    
    [Tooltip("A list of all transmitter/receiver pairs to create links for.")]
    public List<LinkDefinition> links = new List<LinkDefinition>();

    [Header("Gaussian Beam Parameters")]
    [Tooltip("w₀ = Beam waist at z=0 (in meters)")]
    public float beamWaist = 0.002f; // 2mm
    
    [Tooltip("λ = Wavelength of the light (in meters)")]
    public float wavelength = 1550e-9f; // 1550nm

    // Store a reference to the beams we create
    private List<GaussianBeam> activeLinks = new List<GaussianBeam>();

    // We use OnValidate to see changes live in the editor
    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            GenerateLinks();
        }
    }

    /// <summary>
    /// This is the main function. It clears all old beams and builds new ones.
    /// You can call this from a UI button.
    /// </summary>
    [ContextMenu("Generate/Update All Links")]
    public void GenerateLinks()
    {
        if (beamPrefab == null)
        {
            Debug.LogError("Beam Prefab is not assigned!", this);
            return;
        }

        if (transceiverPlacer == null)
        {
            Debug.LogError("Transceiver Placer script is not assigned! Please drag the [Transceivers] GameObject into the slot.", this);
            return;
        }

        // Clear old links
        foreach (GaussianBeam oldLink in activeLinks)
        {
            if (oldLink != null)
                Destroy(oldLink.gameObject);
        }
        activeLinks.Clear();

        Transform transceiverContainer = transceiverPlacer.transform;

        // Create new links
        foreach (LinkDefinition link in links)
        {
            if (link.transmitterIndex >= transceiverContainer.childCount || 
                link.receiverIndex >= transceiverContainer.childCount ||
                link.transmitterIndex < 0 || link.receiverIndex < 0)
            {
                Debug.LogWarning($"Skipping link '{link.name}'. Index is out of range. Max index is {transceiverContainer.childCount - 1}.");
                continue;
            }

            Transform transmitter = transceiverContainer.GetChild(link.transmitterIndex);
            Transform receiver = transceiverContainer.GetChild(link.receiverIndex);
            
            if (transmitter == null || receiver == null)
            {
                Debug.LogWarning($"Skipping link '{link.name}' due to missing Tx or Rx.");
                continue;
            }

            // --- 1. Get references to the lens transforms ---
            Transform txLens = transmitter.FindDeepChild("Lens_Aperture_Mesh");
            Transform rxLens = receiver.FindDeepChild("Lens_Aperture_Mesh");

            if (txLens == null || rxLens == null)
            {
                Debug.LogError($"Could not find 'Lens_Aperture_Mesh' in {link.name}. Make sure prefabs are correct.");
                continue;
            }

            // --- 2. Get INITIAL positions ---
            // Get the receiver's current lens position to use as a target
            Vector3 endPos_v1 = rxLens.position; 

            // --- 3. AIM THE TRANSMITTER ---
            AimTransceiver(transmitter, endPos_v1);
            
            // --- 4. Get the transmitter's NEW position ---
            // (It has moved because it's now aimed)
            Vector3 startPos_v2 = txLens.position; 

            // --- 5. AIM THE RECEIVER ---
            // Aim the receiver at the transmitter's new, aimed position
            AimTransceiver(receiver, startPos_v2);

            // --- 6. GET THE RECEIVER'S NEW POSITION (THE FIX) ---
            // (It has also moved because it's now aimed)
            Vector3 endPos_v2 = rxLens.position;

            // --- 7. Create the Beam ---
            GameObject beamObj = Instantiate(beamPrefab, this.transform);
            beamObj.name = $"Beam_{link.name}";

            GaussianBeam beam = beamObj.GetComponent<GaussianBeam>();
            
            // Initialize the beam with the *correct, final* start/end positions
            beam.Initialize(
                startPos_v2,    // The *new* lens position
                endPos_v2,      // The *new* lens position
                receiver,       // The *root* of the receiver, for collision ignoring
                beamWaist,
                wavelength
            );
            
            activeLinks.Add(beam);
        }
    }

    /// <summary>
    /// Rotates the gimbal components of a transceiver to aim at a target.
    /// </summary>
    /// <param name="transceiverRoot">The root FSO_Trcansceiver_Prefab</param>
    /// <param name="targetPosition">The world-space point to aim at</param>
    private void AimTransceiver(Transform transceiverRoot, Vector3 targetPosition)
    {
        // Find the two pivot points in the prefab's hierarchy
        Transform basePivot = transceiverRoot.Find("Gimbal_Base_Pivot");
        Transform yokePivot = basePivot?.Find("Gimbal_Yoke_Pivot");

        if (basePivot == null || yokePivot == null)
        {
            Debug.LogWarning($"Could not find pivots in '{transceiverRoot.name}'. Cannot aim.");
            return;
        }

        // --- YAW (Left/Right) CALCULATION ---
        Vector3 worldTargetDir = targetPosition - basePivot.position;
        Vector3 worldPlaneNormal = transceiverRoot.up;
        Vector3 yawTargetDir_Projected = Vector3.ProjectOnPlane(worldTargetDir, worldPlaneNormal).normalized;
        Vector3 rootWorldForward = transceiverRoot.forward;
        float yawAngle = Vector3.SignedAngle(rootWorldForward, yawTargetDir_Projected, worldPlaneNormal);
        basePivot.localRotation = Quaternion.Euler(0, yawAngle, 0);

        // --- PITCH (Up/Down) CALCULATION ---
        worldTargetDir = targetPosition - yokePivot.position;
        Vector3 yokeWorldRight = yokePivot.right;
        Vector3 pitchTargetDir_Projected = Vector3.ProjectOnPlane(worldTargetDir, yokeWorldRight).normalized;
        Vector3 yokeWorldForward = yokePivot.forward;
        float pitchAngle = Vector3.SignedAngle(yokeWorldForward, pitchTargetDir_Projected, yokeWorldRight);
        yokePivot.localRotation = Quaternion.Euler(pitchAngle, 0, 0);
    }
}

/// <summary>
/// Helper class to find a child Transform by name, even if it's deep in the hierarchy.
/// </summary>
public static class TransformExtensions
{
    public static Transform FindDeepChild(this Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name)
                return child;
            
            Transform result = child.FindDeepChild(name);
            if (result != null)
                return result;
        }
        return null;
    }
}