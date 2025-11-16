using UnityEngine;

/// <summary>
/// Represents a single Gaussian FSO link.
/// This script handles the physics (collider) and visuals (line renderer)
/// for one beam, and detects blockage.
/// </summary>
[RequireComponent(typeof(GaussianBeamCollider))]
[RequireComponent(typeof(LineRenderer))]
public class GaussianBeam : MonoBehaviour
{
    [Header("Link Status")]
    [SerializeField] private bool isBlocked = false;
    [SerializeField] private Collider blockingObject = null;

    // --- Public visual parameters ---
    [Header("Visuals")]
    [Tooltip("The speed of the blink (blinks per second).")]
    public float blinkSpeed = 5.0f;

    // --- Private References ---
    private Transform receiverRoot; // The root of the intended receiver
    private GaussianBeamCollider beamCollider;
    private LineRenderer lineRenderer;
    private Material lineMaterial; // To change color

    // --- NEW: We'll store your material's original color ---
    private Color defaultBeamColor;

    // Parameters passed from the manager
    private float beamWaist_w0;
    private float wavelength_lambda;
    private float linkDistance;

    void Awake()
    {
        // Get the components that MUST exist on this prefab
        beamCollider = GetComponent<GaussianBeamCollider>();
        lineRenderer = GetComponent<LineRenderer>();
        
        // Store the original material to reset the color
        // We create a new instance so each beam can change color independently
        lineMaterial = new Material(lineRenderer.material);
        
        // --- NEW: Store the original color from your prefab ---
        defaultBeamColor = lineMaterial.color; 
        
        lineRenderer.material = lineMaterial;
    }

    /// <summary>
    /// Initializes this beam instance with parameters from the manager.
    /// </summary>
    public void Initialize(Vector3 startPos, Vector3 endPos, Transform rxRoot, float waist, float wavelength)
    {
        this.receiverRoot = rxRoot;
        this.beamWaist_w0 = waist;
        this.wavelength_lambda = wavelength;

        // --- 1. Position and Aim the Beam ---
        linkDistance = Vector3.Distance(startPos, endPos);

        transform.position = startPos;
        transform.LookAt(endPos);

        // --- 2. Update the Physics Collider ---
        // This tells the collider to build its custom Gaussian mesh
        beamCollider.UpdateBeamParameters(beamWaist_w0, wavelength_lambda, linkDistance);

        // --- 3. Update the Visuals ---
        // Set positions in *world space* for the LineRenderer
        lineRenderer.useWorldSpace = true;
        lineRenderer.SetPosition(0, startPos); // Start
        lineRenderer.SetPosition(1, endPos); // End
        
        // We can also set the width to match the Gaussian spread
        float endRadius = beamCollider.GetRadiusAtDistance(linkDistance);
        lineRenderer.startWidth = beamWaist_w0 * 2;
        lineRenderer.endWidth = endRadius * 2;
        
        ResetStatus();
    }

    /// <summary>
    /// Resets the blockage status of this link.
    /// </summary>
    public void ResetStatus()
    {
        isBlocked = false;
        blockingObject = null;
        if (lineMaterial != null)
        {
            // --- MODIFIED: Use the stored default color instead of hard-coding green ---
            lineMaterial.color = defaultBeamColor;
        }
    }

    /// <summary>
    /// This is the core collision logic.
    /// </summary>
    void OnTriggerEnter(Collider other)
    {
        // If we are already blocked, don't do anything else
        if (isBlocked) return;
        
        // If we hit ourselves (which can happen at the start), ignore it
        if (other.transform.root == this.transform.root) return;

        // Check if the object we hit is our intended receiver
        // We check the root, as the receiver prefab may have many child colliders
        if (other.transform.root == receiverRoot)
        {
            // This is a successful hit on the receiver, ignore it.
            return;
        }

        // If it's NOT our receiver, it's a blockage.
        isBlocked = true;
        blockingObject = other;

        // --- MODIFIED BLINK LOGIC ---
        // We check the time to create a 50/50 on/off cycle.
        // This works because this function is re-triggered constantly by the FSOLinkManager.
        float blinkCycle = (Time.time * blinkSpeed) % 1.0f;
        if (blinkCycle > 0.5f)
        {
            // "On" state: Use the link's default color (yellow-ish or red)
            lineMaterial.color = defaultBeamColor;
        }
        else
        {
            // "Off" state: Use a dim, semi-transparent version of the default color
            Color darkDefaultColor = defaultBeamColor * 0.2f;
            darkDefaultColor.a = 0.5f;
            lineMaterial.color = darkDefaultColor;
        }

        Debug.Log($"Link '{name}' was blocked by '{other.name}'!");
        
    }
}