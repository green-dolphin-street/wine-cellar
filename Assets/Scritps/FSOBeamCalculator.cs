using UnityEngine;

public static class GaussianBeamCalculator
{
    /// <summary>
    /// Calculate the Rayleigh range of a Gaussian beam. (Edmund Optics Equation 5, Page 2)
    /// </summary>
    /// <param name="beamWaist">The beam waist radius [meters].</param>
    /// <param name="wavelength">The wavelength of the beam [meters].</param>
    /// <returns>The Rayleigh range, which is the distance at which the beam waist radius has increased by a factor of e [meters].</returns>
    public static float CalculateRayleighRange(float beamWaist, float wavelength)
    {
        return Mathf.PI * beamWaist * beamWaist / wavelength;
    }

    /// <summary>
    /// Calculate the divergence angle of a Gaussian beam. (Edmund Optics Equation 3, Page 2)
    /// </summary>
    /// <param name="wavelength">The wavelength of the beam [meters].</param>
    /// <param name="beamWaist">The beam waist radius [meters].</param>
    /// <returns>The divergence angle [radians].</returns>
    public static float CalculateDivergenceAngle(float wavelength, float beamWaist)
    {
        return wavelength / (Mathf.PI * beamWaist);
    }

    /// <summary>
    /// Calculate the beam radius at a given propagation distance. (Edmund Optics Equation 6, Page 2)
    /// w(z) = w_0 * √(1 + (z/z_R)^2)
    /// </summary>
    /// <param name="beamWaist">The beam waist radius [meters].</param>
    /// <param name="rayleighRange">The Rayleigh range [meters].</param>
    /// <param name="propagationDistance">The propagation distance [meters].</param>
    /// <returns>The beam radius at the given propagation distance [meters].</returns>
    public static float CalculateBeamRadius(float beamWaist, float rayleighRange, float propagationDistance)
    {
        return beamWaist * Mathf.Sqrt(1.0f + (propagationDistance * propagationDistance) / (rayleighRange * rayleighRange));
    }

    /// <summary>
    /// Calculate the intensity of a Gaussian beam at a given radial distance. (Edmund Optics Equation 1, Page 1)
    /// I(r,z) = I_0(z) * exp(-2r^2/w(z)^2)
    /// </summary>
    /// <param name="radialDistance">The radial distance from the beam axis [meters].</param>
    /// <param name="beamRadius">The beam radius at the given radial distance [meters].</param>
    /// <returns>The intensity of the beam at the given radial distance [dimensionless].</returns>
    public static float CalculateIntensity(float radialDistance, float beamRadius)
    {
        if (beamRadius <= 0) return 0f;
        
        float exponent = -2.0f * radialDistance * radialDistance / (beamRadius * beamRadius);
        return Mathf.Exp(exponent);
    }

    /// <summary>
    /// Sample the beam profile at a given radial distance. (Edmund Optics Equation 6, Page 2)
    /// </summary>
    /// <param name="beamWaist">The beam waist radius [meters].</param>
    /// <param name="rayleighRange">The Rayleigh range [meters].</param>
    /// <param name="maxDistance">The maximum propagation distance [meters].</param>
    /// <param name="sampleCount">The number of samples to generate.</param>
    /// <returns>An array of (distance, radius) pairs.</returns>

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
    /// Determine if a point is inside a Gaussian beam and return the radial and axial distances.
    /// (Edmund Optics - Beam waist에서 1/e² (13.5%) intensity까지를 빔 경계로 정의)
    /// </summary>
    /// <param name="point">The point to check (world coordinates)</param>
    /// <param name="beamOrigin">The origin of the beam (world coordinates)</param>
    /// <param name="beamDirection">The direction of the beam (normalized)</param>
    /// <param name="beamWaist">The beam waist radius [meters]</param>
    /// <param name="rayleighRange">The Rayleigh range [meters]</param>
    /// <param name="beamLength">The length of the beam [meters]</param>
    /// <param name="radialDistance">The radial distance from the beam axis [meters]</param>
    /// <param name="axialDistance">The axial distance from the beam origin [meters]</param>
    /// <returns>True if the point is inside the beam, false otherwise</returns>
    public static bool IsPointInBeam(Vector3 point, Vector3 beamOrigin, Vector3 beamDirection,
        float beamWaist, float rayleighRange, float beamLength,
        out float radialDistance, out float axialDistance)
    {
        // Calculate the axial distance from the beam origin
        Vector3 toPoint = point - beamOrigin;
        axialDistance = Vector3.Dot(toPoint, beamDirection);

        // Check if the point is outside the beam length range
        if (axialDistance < 0 || axialDistance > beamLength)
        {
            radialDistance = 0;
            return false;
        }

        // Calculate the radial distance from the beam axis
        Vector3 closestPointOnAxis = beamOrigin + beamDirection * axialDistance;
        radialDistance = Vector3.Distance(point, closestPointOnAxis);

        // Calculate the beam radius at the current axial distance
        float w_z = CalculateBeamRadius(beamWaist, rayleighRange, axialDistance);

        // Check if the point is inside the beam based on the 1/e² boundary (PDF Figure 1)
        return radialDistance <= w_z;
    }

    /// <summary>
    /// Calculate the intensity at a specific point
    /// I(r,z) = I_0 * exp(-2r²/w(z)²) * (2P / πw(z)²)
    /// </summary>
    /// <param name="point">The point to calculate the intensity at</param>
    /// <param name="beamOrigin">The origin of the beam</param>
    /// <param name="beamDirection">The direction of the beam</param>
    /// <param name="beamWaist">The beam waist radius [meters]</param>
    /// <param name="rayleighRange">The Rayleigh range [meters]</param>
    /// <param name="beamLength">The length of the beam [meters]</param>
    /// <param name="totalPower">The total power of the beam [Watts]</param>
    /// <returns>The intensity at the given point [W/m²], 0 if the point is outside the beam</returns>
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
    /// Calculate the normalized intensity (0~1 range)
    /// </summary>
    public static float CalculateNormalizedIntensity(float radialDistance, float beamRadius)
    {
        return CalculateIntensity(radialDistance, beamRadius);
    }

}