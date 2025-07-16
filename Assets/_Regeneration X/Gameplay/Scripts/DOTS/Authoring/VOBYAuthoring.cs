using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;

public class VOBYAuthoring : MonoBehaviour
{
    public Vector3 epicenter;
    public class Baker : Baker<VOBYAuthoring>
    {
        public override void Bake(VOBYAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new VOBYComponent { epicenter = authoring.epicenter });
        }
    }
}