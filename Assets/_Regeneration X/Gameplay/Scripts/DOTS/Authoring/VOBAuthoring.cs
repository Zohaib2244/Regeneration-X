using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using Unity.VisualScripting;
using UnityEngine;

public class VOBAuthoring : MonoBehaviour
{
    public GameObject VOBYParent;
    public int VOBIndex = 0;
    public class Baker : Baker<VOBAuthoring>
    {
        public override void Bake(VOBAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new VOBComponent
            {
                VOBYParent = GetEntity(authoring.VOBYParent, TransformUsageFlags.Dynamic),
                Position = authoring.transform.position,
                Rotation = authoring.transform.rotation,
                VOBIndex = authoring.VOBIndex
            });
            AddComponent(entity, new Parent
            {
                Value = GetEntity(authoring.VOBYParent, TransformUsageFlags.Dynamic)
            });
            AddComponent(entity, new VOBMagneticForce
            {
                Force = float3.zero,
                Velocity = float3.zero
            });
        }
    }
}