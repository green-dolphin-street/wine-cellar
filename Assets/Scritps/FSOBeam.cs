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

    private Transform receiverRoot;
    private Transform transmitterRoot;
    private GaussianBeamCollider beamCollider;
    private LineRenderer lineRenderer;
    private Material lineMaterial;

    private Color defaultBeamColor;

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
        defaultBeamColor = lineMaterial.color; 
        lineRenderer.material = lineMaterial;
    }

    /// <summary>
    /// Initializes beam instance with parameters from FSOLinkManager.
    /// </summary>
    public void Initialize(Vector3 startPos, Vector3 endPos, Transform txRoot, Transform rxRoot, float waist, float wavelength)
    {
        this.receiverRoot = rxRoot;
        this.transmitterRoot = txRoot;
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

    public void ResetStatus()
    {
        isBlocked = false;
        blockingObject = null;
        if (lineMaterial != null)
        {
            lineMaterial.color = defaultBeamColor;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        // If we are already blocked, don't do anything else
        if (isBlocked) return;
        
        // If we hit ourselves (which can happen at the start), ignore it
        if (other.transform.root == this.transform.root) return;

        // If we hit the transmitter, ignore it
        if (other.transform.IsChildOf(transmitterRoot)) return;

        // Check if the object we hit is our intended receiver
        if (other.transform.IsChildOf(receiverRoot))
        {
            return;
        }

        isBlocked = true;
        blockingObject = other;
        lineMaterial.color = Color.black;

        Debug.Log($"'{name}' was blocked by '{other.transform.parent.parent.parent.name}'!");
    }
}