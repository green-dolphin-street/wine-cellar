using UnityEngine;

/// <summary>
/// Gaussian beam mesh collider generation based on PDF Equation 6: w(z) = w₀ * √(1 + (z/z_R)²)
/// </summary>
[RequireComponent(typeof(MeshCollider))]
public class GaussianBeamCollider : MonoBehaviour
{
    [Header("Gaussian Beam Parameters")]
    [SerializeField] private float beamWaist = 0.002f; // w₀ = 2mm
    [SerializeField] private float wavelength = 1550e-9f; // λ = 1550nm
    [SerializeField] private float beamLength = 10f; // Beam length [meters]

    [Header("Mesh Generation Settings")]
    [SerializeField] private int axialSegments = 20; // Number of segments along the beam axis (reduced for performance)
    [SerializeField] private int radialSegments = 12; // Number of segments around the beam axis
    [SerializeField] private bool updateOnValidate = true;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;

    private MeshCollider meshCollider;
    private Mesh colliderMesh;
    private float rayleighRange;

    void Awake()
    {
        meshCollider = GetComponent<MeshCollider>();
        if (meshCollider == null)
            meshCollider = gameObject.AddComponent<MeshCollider>();
        
        // Wait until the beam is generated before creating the collider.
        // UpdateBeamParameters will handle the collider creation.
    }

    /// <summary>
    /// Generate the Gaussian beam collider mesh
    /// PDF Equation 6 based conical mesh
    /// </summary>
    public void GenerateGaussianCollider()
    {
        // Rayleigh range calculation (PDF Equation 5)
        rayleighRange = GaussianBeamCalculator.CalculateRayleighRange(beamWaist, wavelength);

        if (showDebugInfo)
        {
            Debug.Log($"=== Gaussian Beam Collider ===");
            Debug.Log($"Beam Waist (w₀): {beamWaist * 1000f:F3} mm");
            Debug.Log($"Wavelength (λ): {wavelength * 1e9f:F1} nm");
            Debug.Log($"Rayleigh Range (z_R): {rayleighRange:F3} m");
            Debug.Log($"Beam Length: {beamLength:F2} m");
        }

        if (colliderMesh != null)
        {
            Destroy(colliderMesh);
        }
        
        colliderMesh = new Mesh();
        colliderMesh.name = "GaussianBeamCollider";

        // Generate vertices (conical shape)
        Vector3[] vertices = GenerateVertices();
        int[] triangles = GenerateTriangles();

        colliderMesh.vertices = vertices;
        colliderMesh.triangles = triangles;
        colliderMesh.RecalculateNormals();
        colliderMesh.RecalculateBounds();

        // Apply the mesh to the MeshCollider
        if (meshCollider == null)
            meshCollider = GetComponent<MeshCollider>();

        meshCollider.sharedMesh = colliderMesh;
        meshCollider.convex = true;
        meshCollider.isTrigger = true;

        if (showDebugInfo)
        {
            float endRadius = GaussianBeamCalculator.CalculateBeamRadius(beamWaist, rayleighRange, beamLength);
            Debug.Log($"Start Radius: {beamWaist * 1000f:F3} mm");
            Debug.Log($"End Radius (at {beamLength}m): {endRadius * 1000f:F3} mm");
            Debug.Log($"Expansion Factor: {endRadius / beamWaist:F2}x");
        }
    }

    /// <summary>
    /// Generate vertices based on PDF Equation 6: w(z) = w₀ * √(1 + (z/z_R)²)
    /// w(z) = w₀ * √(1 + (z/z_R)²)
    /// </summary>
    private Vector3[] GenerateVertices()
    {
        int totalVertices = (axialSegments + 1) * radialSegments + 2;
        Vector3[] vertices = new Vector3[totalVertices];

        int vertexIndex = 0;
        vertices[vertexIndex++] = Vector3.zero;

        // Generate vertices along the beam axis 
        for (int z = 0; z <= axialSegments; z++)
        {
            float axialDistance = (float)z / axialSegments * beamLength;

            float w_z = GaussianBeamCalculator.CalculateBeamRadius(beamWaist, rayleighRange, axialDistance);

            // Generate vertices around the beam axis
            for (int r = 0; r < radialSegments; r++)
            {
                float angle = 2f * Mathf.PI * r / radialSegments;

                float x = w_z * Mathf.Cos(angle);
                float y = w_z * Mathf.Sin(angle);
                // Unity's Cylinder is Y-axis is the length direction, but
                // we use GameObject's Z-axis(forward) as the length.
                float z_pos = axialDistance; 

                vertices[vertexIndex++] = new Vector3(x, y, z_pos);
            }
        }

        vertices[vertexIndex++] = new Vector3(0, 0, beamLength);

        if (vertexIndex != totalVertices)
        {
            Debug.LogWarning($"Vertex mismatch. Expected {totalVertices}, got {vertexIndex}");
        }

        return vertices;
    }

    /// <summary>
    /// Generate triangles for the conical mesh
    /// </summary>
    private int[] GenerateTriangles()
    {
        int totalTriangles = (axialSegments * radialSegments * 2) + (radialSegments * 2); // side + caps
        int[] triangles = new int[totalTriangles * 3];
        int triIndex = 0;

        int startCapCenter = 0;
        int endCapCenter = (axialSegments + 1) * radialSegments + 1;

        // Start cap triangles
        for (int r = 0; r < radialSegments; r++)
        {
            int v1 = 1 + r;
            int v2 = 1 + (r + 1) % radialSegments;

            triangles[triIndex++] = startCapCenter;
            triangles[triIndex++] = v2;
            triangles[triIndex++] = v1;
        }

        // Side triangles
        for (int z = 0; z < axialSegments; z++)
        {
            for (int r = 0; r < radialSegments; r++)
            {
                int baseVertex = 1 + z * radialSegments;

                int v1 = baseVertex + r;
                int v2 = baseVertex + (r + 1) % radialSegments;
                int v3 = baseVertex + radialSegments + r;
                int v4 = baseVertex + radialSegments + (r + 1) % radialSegments;
                
                // Triangle 1
                triangles[triIndex++] = v1;
                triangles[triIndex++] = v3;
                triangles[triIndex++] = v2;

                // Triangle 2
                triangles[triIndex++] = v2;
                triangles[triIndex++] = v3;
                triangles[triIndex++] = v4;
            }
        }

        // End cap triangles
        int lastRingStart = 1 + axialSegments * radialSegments;
        for (int r = 0; r < radialSegments; r++)
        {
            int v1 = lastRingStart + r;
            int v2 = lastRingStart + (r + 1) % radialSegments;

            triangles[triIndex++] = endCapCenter;
            triangles[triIndex++] = v1;
            triangles[triIndex++] = v2;
        }
        
        return triangles;
    }


    /// <summary>
    /// Update beam parameters (called from Manager)
    /// </summary>
    public void UpdateBeamParameters(float newWaist, float newWavelength, float newLength)
    {
        beamWaist = newWaist;
        wavelength = newWavelength;
        beamLength = newLength;

        GenerateGaussianCollider();
    }

    /// <summary>
    /// Get beam radius at a specific distance
    /// </summary>
    public float GetRadiusAtDistance(float distance)
    {
        if (distance < 0 || distance > beamLength)
            return 0f;

        // Recalculate rayleighRange if it's not initialized
        if (rayleighRange == 0)
            rayleighRange = GaussianBeamCalculator.CalculateRayleighRange(beamWaist, wavelength);

        return GaussianBeamCalculator.CalculateBeamRadius(beamWaist, rayleighRange, distance);
    }

    void OnValidate()
    {
        // Update collider on runtime if updateOnValidate is enabled
        if (updateOnValidate && Application.isPlaying)
        {
            GenerateGaussianCollider();
        }
    }
}