using UnityEngine;

public static class GaussianBeamCalculator
{
    /// <summary>
    /// 레일리 거리 계산 (Edmund Optics Equation 5, Page 2)
    /// </summary>
    /// <param name="beamWaist">빔 웨이스트 반경 (미터)</param>
    /// <param name="wavelength">파장 (미터)</param>
    /// <returns>레일리 거리 (미터)</returns>
    /// 
    public static float CalculateRayleighRange(float beamWaist, float wavelength)
    {
        return Mathf.PI * beamWaist * beamWaist / wavelength;
    }

    /// <summary>
    /// 발산 각도 계산 (Edmund Optics Equation 3, Page 2)
    /// </summary>
    /// <param name="wavelength">파장 (미터)</param>
    /// <param name="beamWaist">빔 웨이스트 반경 (미터)</param>
    /// <returns>발산 각도 (라디안)</returns>

    public static float CalculateDivergenceAngle(float wavelength, float beamWaist)
    {
        return wavelength / (Mathf.PI * beamWaist);
    }

    /// <summary>
    /// 전파 거리에 따른 빔 반경 계산 (Edmund Optics Equation 6, Page 2)
    /// w(z) = w_0 * √(1 + (z/z_R)^2)
    /// </summary>
    /// <param name="beamWaist">빔 웨이스트 반경 (미터)</param>
    /// <param name="rayleighRange">레일리 거리 (미터)</param>
    /// <param name="propagationDistance">빔 웨이스트로부터의 전파 거리 (미터)</param>
    /// <returns>현재 위치의 빔 반경 (미터)</returns>
    
    public static float CalculateBeamRadius(float beamWaist, float rayleighRange, float propagationDistance)
    {
        return beamWaist * Mathf.Sqrt(1.0f + (propagationDistance * propagationDistance) / (rayleighRange * rayleighRange));
    }

    /// <summary>
    /// 가우시안 강도 분포 계산 (Edmund Optics Equation 1, Page 1)
    /// I(r,z) = I_0(z) * exp(-2r^2/w(z)^2)
    /// </summary>
    /// <param name="radialDistance">빔 중심축으로부터의 거리 (미터)</param>
    /// <param name="beamRadius">현재 위치의 빔 반경 (미터)</param>
    /// <returns>강도 (0~1 범위로 정규화)</returns>
    public static float CalculateIntensity(float radialDistance, float beamRadius)
    {
        if (beamRadius <= 0) return 0f;
        
        float exponent = -2.0f * radialDistance * radialDistance / (beamRadius * beamRadius);
        return Mathf.Exp(exponent);
    }

    /// <summary>
    /// 빔 웨이스트에서 특정 거리까지의 빔 반경 변화를 샘플링
    /// </summary>
    /// <param name="beamWaist">빔 웨이스트 반경 (미터)</param>
    /// <param name="rayleighRange">레일리 거리 (미터)</param>
    /// <param name="maxDistance">최대 전파 거리 (미터)</param>
    /// <param name="sampleCount">샘플 개수</param>
    /// <returns>거리와 빔 반경의 쌍 배열</returns>

    public static (float distance, float radius)[] SampleBeamProfile(float beamWaist, float rayleighRange, float maxDistance, int sampleCount)
    {
        var samples = new (float distance, float radius)[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float distance = (float)i / (sampleCount - 1) * maxDistance;
            float radius = CalculateBeamRadius(beamWaist, rayleighRange, distance);
            samples[i] = (distance, radius);
        }

        return samples;
    }

    /// <summary>
    /// 점이 가우시안 빔 내부에 있는지 확인하고 radial/axial 거리 반환
    /// (Edmund Optics - Beam waist에서 1/e² (13.5%) intensity까지를 빔 경계로 정의)
    /// </summary>
    /// <param name="point">확인할 점 (월드 좌표)</param>
    /// <param name="beamOrigin">빔 시작점 (월드 좌표)</param>
    /// <param name="beamDirection">빔 방향 (정규화)</param>
    /// <param name="beamWaist">빔 웨이스트 반경 (미터)</param>
    /// <param name="rayleighRange">레일리 거리 (미터)</param>
    /// <param name="beamLength">빔 총 길이 (미터)</param>
    /// <param name="radialDistance">중심축으로부터의 거리 (출력)</param>
    /// <param name="axialDistance">빔 시작점으로부터의 거리 (출력)</param>
    /// <returns>빔 내부 여부</returns>
    public static bool IsPointInBeam(Vector3 point, Vector3 beamOrigin, Vector3 beamDirection,
        float beamWaist, float rayleighRange, float beamLength,
        out float radialDistance, out float axialDistance)
    {
        // 빔 축 방향 투영
        Vector3 toPoint = point - beamOrigin;
        axialDistance = Vector3.Dot(toPoint, beamDirection);

        // 빔 길이 범위 체크
        if (axialDistance < 0 || axialDistance > beamLength)
        {
            radialDistance = 0;
            return false;
        }

        // 축에서 수직 거리 계산
        Vector3 closestPointOnAxis = beamOrigin + beamDirection * axialDistance;
        radialDistance = Vector3.Distance(point, closestPointOnAxis);

        // 현재 위치에서의 빔 반경 계산 (PDF Equation 6)
        float w_z = CalculateBeamRadius(beamWaist, rayleighRange, axialDistance);

        // 1/e² 경계 기준 (PDF Figure 1)
        return radialDistance <= w_z;
    }

    /// <summary>
    /// 특정 위치에서의 빔 강도 계산 (PDF Equation 1)
    /// I(r,z) = I_0 * exp(-2r²/w(z)²) * (2P / πw(z)²)
    /// </summary>
    /// <param name="point">확인할 점</param>
    /// <param name="beamOrigin">빔 시작점</param>
    /// <param name="beamDirection">빔 방향</param>
    /// <param name="beamWaist">빔 웨이스트 반경</param>
    /// <param name="rayleighRange">레일리 거리</param>
    /// <param name="beamLength">빔 길이</param>
    /// <param name="totalPower">총 빔 파워 (Watts)</param>
    /// <returns>해당 위치의 강도 (W/m²), 빔 밖이면 0</returns>
    public static float CalculateIntensityAtPoint(Vector3 point, Vector3 beamOrigin, Vector3 beamDirection,
        float beamWaist, float rayleighRange, float beamLength, float totalPower)
    {
        if (!IsPointInBeam(point, beamOrigin, beamDirection, beamWaist, rayleighRange, beamLength,
            out float r, out float z))
        {
            return 0f;
        }

        // PDF Equation 1: I(r,z) = (2P / πw(z)²) * exp(-2r²/w(z)²)
        float w_z = CalculateBeamRadius(beamWaist, rayleighRange, z);
        float I_0 = (2f * totalPower) / (Mathf.PI * w_z * w_z);
        float intensity = I_0 * Mathf.Exp(-2f * r * r / (w_z * w_z));

        return intensity;
    }

    /// <summary>
    /// 정규화된 강도 계산 (0~1 범위)
    /// </summary>
    public static float CalculateNormalizedIntensity(float radialDistance, float beamRadius)
    {
        return CalculateIntensity(radialDistance, beamRadius);
    }

}