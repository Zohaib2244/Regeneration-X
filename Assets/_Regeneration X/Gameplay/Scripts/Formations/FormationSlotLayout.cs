using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Builds the ordered list of spots a VOBY can put cubes in: shape slots, then hidden reserve slots
/// inside the shape, then a halo of spare cubes floating around it.
/// </summary>
public static class FormationSlotLayout
{
    /// <param name="shape">Shape slots (local). Count = cube cost.</param>
    /// <param name="shapeRotations">Optional per-slot rotations; null or short means identity.</param>
    /// <param name="reserve">Hidden interior slots, shallowest first. May be null.</param>
    /// <param name="capacity">Total number of slots to produce (halo fills whatever reserve doesn't cover).</param>
    /// <param name="shapeRadius">Radius around the origin that contains the shape.</param>
    public static List<VOBYSlot> Build(IList<Vector3> shape, IList<Quaternion> shapeRotations, IList<Vector3> reserve,
        float cubeSize, int capacity, out float shapeRadius)
    {
        int shapeCount = shape != null ? shape.Count : 0;
        var list = new List<VOBYSlot>(math.max(capacity, shapeCount));

        shapeRadius = 0f;
        for (int i = 0; i < shapeCount; i++)
        {
            quaternion rot = shapeRotations != null && i < shapeRotations.Count
                ? (quaternion)shapeRotations[i]
                : quaternion.identity;
            list.Add(new VOBYSlot { Position = shape[i], Rotation = rot });
            shapeRadius = math.max(shapeRadius, math.length((float3)shape[i]));
        }
        shapeRadius += cubeSize * 0.5f;

        if (reserve != null)
        {
            for (int i = 0; i < reserve.Count && list.Count < capacity; i++)
                list.Add(new VOBYSlot { Position = reserve[i], Rotation = quaternion.identity });
        }

        if (list.Count < capacity)
            AppendHalo(list, shapeRadius, cubeSize, capacity - list.Count);

        return list;
    }

    /// <summary>
    /// Concentric shells of points around the shape. Points within a shell are laid out so that any
    /// prefix is spread over the whole sphere, so a half-filled halo still looks even.
    /// </summary>
    public static void AppendHalo(List<VOBYSlot> list, float innerRadius, float cubeSize, int count)
    {
        float spacing = math.max(cubeSize * 1.6f, 1e-3f);
        float radius = innerRadius + cubeSize * 2f;
        const float goldenAngle = 2.39996323f;   // pi * (3 - sqrt 5)
        const float goldenFraction = 0.618033988f;

        while (count > 0)
        {
            int shellCapacity = math.max(8, (int)(4f * math.PI * radius * radius / (spacing * spacing)));
            int take = math.min(shellCapacity, count);
            for (int k = 0; k < take; k++)
            {
                float u = math.frac((k + 0.5f) * goldenFraction);
                float y = 1f - 2f * u;
                float ring = math.sqrt(math.max(0f, 1f - y * y));
                float theta = k * goldenAngle;
                float3 dir = new float3(math.cos(theta) * ring, y, math.sin(theta) * ring);
                list.Add(new VOBYSlot { Position = dir * radius, Rotation = quaternion.identity });
            }
            count -= take;
            radius += spacing;
        }
    }
}
