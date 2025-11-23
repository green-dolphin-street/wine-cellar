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
        [Tooltip("The index of the reflecting IRS element (e.g., 0 for the first element in the X-Z plane). Set this to -1 if using s-WINE.")]
        public int reflectingElementIndex = -1;
    }

    [Header("Link Setup")]
    [Tooltip("The prefab that contains the GaussianBeam and LineRenderer components.")]
    public GameObject beamPrefab;
    
    [Header("Dependencies")]
    [Tooltip("The script that contains all the generated transceiver objects.")]
    public ProceduralTransceiverPlacer transceiverPlacer;
    public ProceduralIRSPlacer irsPlacer;
    
    [Tooltip("A list of all transmitter/receiver pairs to create links for.")]
    public List<LinkDefinition> links = new List<LinkDefinition>();

    [Header("Gaussian Beam Parameters")]
    [Tooltip("w₀ = Beam waist at z=0 (in meters)")]
    public float beamWaist = 0.002f; // 2mm
    
    [Tooltip("λ = Wavelength of the light (in meters)")]
    public float wavelength = 1550e-9f; // 1550nm

    private List<GaussianBeam> activeLinks = new List<GaussianBeam>();

    private bool isDirty = false;

    // OnValidate is called when a value is changed in the Inspector.
    private void OnValidate()
    {
        // OnValidate is "unsafe" for Instantiate/Destroy.
        // Instead of doing work, we just "raise a flag"
        // to tell the Update loop to do the work.
        isDirty = true;
    }

    private void Update()
    {
        if (isDirty)
        {
            if (Application.isPlaying)
            {
                GenerateLinks();
            }
            isDirty = false;
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

        if (irsPlacer == null)
        {
            Debug.LogError("IRS Placer script is not assigned! Please drag the [IRS] GameObject into the slot.", this);
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
        Transform irsContainer = irsPlacer.transform;

        // Create new links
        foreach (LinkDefinition link in links)
        {
            if(link.reflectingElementIndex==-1)
            {
                link.name = $"sWINE_Tx{link.transmitterIndex}_Rx{link.receiverIndex}";
            }
            else
            {
                link.name = $"rWINE_Tx{link.transmitterIndex}_Refl{link.reflectingElementIndex}_Rx{link.receiverIndex}";
            }

            if (link.transmitterIndex >= transceiverContainer.childCount || 
                link.receiverIndex >= transceiverContainer.childCount ||
                link.transmitterIndex < 0 || link.receiverIndex < 0)
            {
                Debug.LogWarning($"Skipping link '{link.name}'. Index is out of range. Max index is {transceiverContainer.childCount - 1}.");
                continue;
            }

            Transform transmitter = transceiverContainer.GetChild(link.transmitterIndex);
            Transform receiver = transceiverContainer.GetChild(link.receiverIndex);
            
            if (link.reflectingElementIndex == -1) // s-WINE
            {
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

                // --- 2. AIM THE TRANSCEIVERS ---
                // We must aim *before* getting the lens positions,
                // as aiming moves the lenses.
                
                // Aim Tx at the Rx's current lens position (and then vice versa)
                AimTransceiver(transmitter, rxLens.position); 
                AimTransceiver(receiver, txLens.position);

                // Now that both are aimed, get their *final* world positions
                Vector3 startPos = txLens.position;
                Vector3 endPos = rxLens.position;

                // --- 3. Create the Beam ---
                GameObject beamObj = Instantiate(beamPrefab, this.transform);
                beamObj.name = $"{link.name}";

                GaussianBeam beam = beamObj.GetComponent<GaussianBeam>();
                
                // Initialize the beam with the correct, final start/end positions
                beam.Initialize(
                    startPos,
                    endPos,
                    transmitter,
                    receiver,
                    beamWaist,
                    wavelength
                );
                
                activeLinks.Add(beam);
            }
            else // r-WINE
            {
                if (link.reflectingElementIndex >= irsPlacer.elementCountX*irsPlacer.elementCountZ || 
                link.reflectingElementIndex < 0)
                {
                    Debug.LogWarning($"Skipping link '{link.name}'. Reflecting element index is out of range. Max index is {irsPlacer.elementCountX*irsPlacer.elementCountZ - 1}.");
                    continue;
                }
                // --- 1. Get references to the lens transforms ---
                Transform txLens = transmitter.FindDeepChild("Lens_Aperture_Mesh");
                Transform rxLens = receiver.FindDeepChild("Lens_Aperture_Mesh");
                Transform reflectingElement = irsPlacer.GetIRSElement(link.reflectingElementIndex);
                // Debug.Log($"reflectingElement: {reflectingElement.transform.name}");
                
                // --- 2. AIM THE TRANSCEIVERS ---
                AimTransceiver(transmitter, reflectingElement.position); 
                AimTransceiver(receiver, reflectingElement.position);
                Vector3 startPos = txLens.position;
                Vector3 endPos = rxLens.position;
                
                // --- 3. Create the Beam ---
                GameObject beamObj = Instantiate(beamPrefab, this.transform);
                beamObj.name = $"rWINE_Tx{link.transmitterIndex}_Refl{link.reflectingElementIndex}";
                GaussianBeam beam = beamObj.GetComponent<GaussianBeam>();
                beam.Initialize(
                    startPos,
                    reflectingElement.position,
                    transmitter,
                    reflectingElement,
                    beamWaist,
                    wavelength
                );

                GameObject beamObj2 = Instantiate(beamPrefab, this.transform);
                beamObj2.name = $"rWINE_Refl{link.reflectingElementIndex}_Rx{link.receiverIndex}";
                GaussianBeam beam2 = beamObj2.GetComponent<GaussianBeam>();
                beam2.Initialize(
                    reflectingElement.position,
                    endPos,
                    reflectingElement,
                    receiver,
                    beamWaist,
                    wavelength
                );
                
                beamObj.GetComponent<LineRenderer>().material.color = Color.red;
                beamObj2.GetComponent<LineRenderer>().material.color = Color.red;
                activeLinks.Add(beam);
                activeLinks.Add(beam2);
            }
        }
    }

    /// <summary>
    /// Rotates the gimbal components of a transceiver to aim at a target.
    /// </summary>
    private void AimTransceiver(Transform transceiverRoot, Vector3 targetPosition)
    {
        Transform basePivot = transceiverRoot.Find("Gimbal_Base_Pivot");
        Transform yokePivot = basePivot?.Find("Gimbal_Yoke_Pivot");

        if (basePivot == null || yokePivot == null)
        {
            Debug.LogWarning($"Could not find pivots in '{transceiverRoot.name}'. Cannot aim.");
            return;
        }

        // --- YAW (Left/Right) ---
        // 1. Get the target direction from the base pivot's point of view
        Vector3 targetDir = targetPosition - basePivot.position;

        // 2. Project this direction onto the *root's* horizontal plane (which is aligned with the rack)
        // We use the root's "up" vector as the plane's normal
        Vector3 horizontalDir = Vector3.ProjectOnPlane(targetDir, transceiverRoot.up).normalized;

        // 3. Create a world-space rotation that looks in this horizontal direction
        // We use the root's "up" as the up-vector for this rotation
        Quaternion targetYaw = Quaternion.LookRotation(horizontalDir, transceiverRoot.up);
        
        // 4. Apply this world rotation directly to the base pivot.
        //    This correctly overrides any parent rotation (like the 180-deg rack).
        basePivot.rotation = targetYaw;

        
        // --- PITCH (Up/Down) ---
        // 1. Get the target direction from the yoke pivot's point of view
        targetDir = targetPosition - yokePivot.position;

        // 2. Project the direction onto the yoke's *local vertical plane*
        //    The plane's normal is the yoke's "right" vector (the axis we pivot around)
        Vector3 yokeRight = yokePivot.right;
        Vector3 pitchDir = Vector3.ProjectOnPlane(targetDir, yokeRight).normalized;

        // 3. Create a world-space rotation that looks in this new direction
        //    We use the yoke's "up" as the up-vector
        Quaternion targetPitch = Quaternion.LookRotation(pitchDir, yokePivot.up);

        // 4. Apply this world rotation directly to the yoke pivot.
        yokePivot.rotation = targetPitch;
    }
}

/// <summary>
/// Helper class to find a child Transform by name, even if it's deep in the hierarchy.
/// (This is already in the repo, but including for completeness)
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