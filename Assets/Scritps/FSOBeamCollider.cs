using UnityEngine;

/// <summary>
/// 가우시안 빔의 동적 Mesh Collider
/// PDF Equation 6: w(z) = w₀ * √(1 + (z/z_R)²) 기반으로 distance에 따라 변하는 collider 생성
/// </summary>
[RequireComponent(typeof(MeshCollider))]
public class GaussianBeamCollider : MonoBehaviour
{
    [Header("Gaussian Beam Parameters")]
    [SerializeField] private float beamWaist = 0.002f; // w₀ = 2mm
    [SerializeField] private float wavelength = 1550e-9f; // λ = 1550nm
    [SerializeField] private float beamLength = 10f; // 빔 길이 (m)

    [Header("Mesh Generation Settings")]
    [SerializeField] private int axialSegments = 20; // 축 방향 세그먼트 수 (성능을 위해 줄임)
    [SerializeField] private int radialSegments = 12; // 원주 방향 세그먼트 수
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
        
        // 빔이 생성될 때 바로 콜라이더를 만들지 않고, 
        // UpdateBeamParameters가 호출될 때 생성하도록 대기합니다.
    }

    /// <summary>
    /// 가우시안 빔 collider 메시 생성
    /// PDF Equation 6 기반 원뿔형 메시
    /// </summary>
    public void GenerateGaussianCollider()
    {
        // Rayleigh range 계산 (PDF Equation 5)
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

        // 정점 생성 (원뿔 모양)
        Vector3[] vertices = GenerateVertices();
        int[] triangles = GenerateTriangles();

        colliderMesh.vertices = vertices;
        colliderMesh.triangles = triangles;
        colliderMesh.RecalculateNormals();
        colliderMesh.RecalculateBounds();

        // MeshCollider에 적용
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
    /// PDF Equation 6 기반 정점 생성
    /// w(z) = w₀ * √(1 + (z/z_R)²)
    /// </summary>
    private Vector3[] GenerateVertices()
    {
        // 원뿔 모양의 메시: 축 방향 + 1 * 원주 + 시작/끝 캡
        int totalVertices = (axialSegments + 1) * radialSegments + 2;
        Vector3[] vertices = new Vector3[totalVertices];

        int vertexIndex = 0;

        // 시작점 중심 (cap)
        vertices[vertexIndex++] = Vector3.zero;

        // 축 방향으로 세그먼트 생성
        for (int z = 0; z <= axialSegments; z++)
        {
            float axialDistance = (float)z / axialSegments * beamLength;

            // PDF Equation 6: w(z) 계산
            float w_z = GaussianBeamCalculator.CalculateBeamRadius(beamWaist, rayleighRange, axialDistance);

            // 원주 방향 정점 생성
            for (int r = 0; r < radialSegments; r++)
            {
                float angle = 2f * Mathf.PI * r / radialSegments;

                float x = w_z * Mathf.Cos(angle);
                float y = w_z * Mathf.Sin(angle);
                // Unity의 Cylinder는 Y축이 길이 방향이지만,
                // 우리는 GameObject의 Z축(forward)을 길이로 사용합니다.
                float z_pos = axialDistance; 

                vertices[vertexIndex++] = new Vector3(x, y, z_pos);
            }
        }

        // 끝점 중심 (cap)
        // 끝점은 마지막 링의 중심입니다.
        vertices[vertexIndex++] = new Vector3(0, 0, beamLength);

        // 정점 수 검사
        if (vertexIndex != totalVertices)
        {
            Debug.LogWarning($"Vertex mismatch. Expected {totalVertices}, got {vertexIndex}");
        }

        return vertices;
    }

    /// <summary>
    /// 삼각형 인덱스 생성 (원뿔)
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
    /// 빔 파라미터 업데이트 (Manager에서 호출)
    /// </summary>
    public void UpdateBeamParameters(float newWaist, float newWavelength, float newLength)
    {
        beamWaist = newWaist;
        wavelength = newWavelength;
        beamLength = newLength;

        GenerateGaussianCollider();
    }

    /// <summary>
    /// 특정 거리에서의 빔 반경 조회
    /// </summary>
    public float GetRadiusAtDistance(float distance)
    {
        if (distance < 0 || distance > beamLength)
            return 0f;

        // rayleighRange가 0이면 다시 계산
        if (rayleighRange == 0)
            rayleighRange = GaussianBeamCalculator.CalculateRayleighRange(beamWaist, wavelength);

        return GaussianBeamCalculator.CalculateBeamRadius(beamWaist, rayleighRange, distance);
    }

    void OnValidate()
    {
        // OnValidate에서 런타임에 자동 업데이트 활성화
        if (updateOnValidate && Application.isPlaying)
        {
            GenerateGaussianCollider();
        }
    }
}