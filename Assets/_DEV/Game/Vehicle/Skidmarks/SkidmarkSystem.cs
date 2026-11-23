using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

// Shared tyre-track mesh for every car in the scene: one mesh, one material, ONE draw call.
//
// A ring buffer of Capacity segments (quads). Each new segment joins the previous one of the same track,
// so a track is continuous. When the buffer is full the oldest segments are reused. Only the segments
// written this frame are uploaded to the GPU. Ageing/fading happens in the shader (CTF/Skidmark).
//
// Visual only, local to each peer. Created on demand by the first CarSkidmarks that needs it.
[DefaultExecutionOrder(1000)] // upload after the cars have added this frame's segments
public sealed class SkidmarkSystem : MonoBehaviour
{
    public const int Capacity = 2048; // segments; 4 vertices each (8192 < 65535, so 16-bit indices)

    [StructLayout(LayoutKind.Sequential)]
    struct Vertex
    {
        public Vector3 position;
        public Color32 color;
        public Vector2 uv;   // x: across the tyre 0..1, y: metres along the track
        public Vector2 uv2;  // x: time laid
    }

    struct Section
    {
        public Vector3 left, right;
        public float distance;
        public byte alpha;
    }

    static SkidmarkSystem instance;

    Mesh mesh;
    NativeArray<Vertex> vertices;
    readonly Section[] sections = new Section[Capacity];
    long nextSerial;               // total segments ever added; slot = serial % Capacity
    int dirtyFrom = int.MaxValue;  // vertex range to upload this frame
    int dirtyTo = -1;

    /// <summary>The scene's skid-mark system, created with this material on first use.</summary>
    public static SkidmarkSystem Get(Material material)
    {
        if (instance != null) return instance;

        var go = new GameObject("SkidmarkSystem");
        instance = go.AddComponent<SkidmarkSystem>();
        instance.Init(material);
        return instance;
    }

    void Init(Material material)
    {
        vertices = new NativeArray<Vertex>(Capacity * 4, Allocator.Persistent, NativeArrayOptions.ClearMemory);

        mesh = new Mesh { name = "Skidmarks" };
        mesh.MarkDynamic();
        mesh.SetVertexBufferParams(Capacity * 4,
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 2));
        mesh.SetVertexBufferData(vertices, 0, 0, vertices.Length);

        // Fixed index buffer: quad i = vertices 4i..4i+3.
        var indices = new NativeArray<ushort>(Capacity * 6, Allocator.Temp);
        for (int i = 0; i < Capacity; i++)
        {
            int v = i * 4, t = i * 6;
            indices[t] = (ushort)v;       indices[t + 1] = (ushort)(v + 2); indices[t + 2] = (ushort)(v + 1);
            indices[t + 3] = (ushort)(v + 1); indices[t + 4] = (ushort)(v + 2); indices[t + 5] = (ushort)(v + 3);
        }
        mesh.SetIndexBufferParams(indices.Length, IndexFormat.UInt16);
        mesh.SetIndexBufferData(indices, 0, 0, indices.Length);
        indices.Dispose();

        // Tracks can be anywhere in the arena: huge bounds, never culled, never recalculated.
        var bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
        mesh.SetSubMesh(0, new SubMeshDescriptor(0, Capacity * 6) { bounds = bounds, vertexCount = Capacity * 4 },
                        MeshUpdateFlags.DontRecalculateBounds);
        mesh.bounds = bounds;

        gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = gameObject.AddComponent<MeshRenderer>();
        r.sharedMaterial = material;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.lightProbeUsage = LightProbeUsage.Off;
        r.reflectionProbeUsage = ReflectionProbeUsage.Off;
        r.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        r.allowOcclusionWhenDynamic = false;
    }

    /// <summary>
    /// Adds one track section and the segment joining it to <paramref name="previous"/> (the serial
    /// returned by the last call for this tyre, or -1 to start a new track). Returns this section's serial.
    /// </summary>
    public long Add(Vector3 position, Vector3 normal, Vector3 direction, float halfWidth, float distance, long previous)
    {
        long serial = nextSerial++;
        int slot = (int)(serial % Capacity);

        Vector3 side = Vector3.Cross(direction, normal);
        side = side.sqrMagnitude > 1e-6f ? side.normalized * halfWidth : Vector3.zero;

        // A track starts transparent, so it fades in instead of beginning with a hard edge.
        bool continues = previous >= 0 && previous > serial - Capacity; // previous still in the buffer
        var section = new Section
        {
            left = position - side,
            right = position + side,
            distance = distance,
            alpha = continues ? (byte)255 : (byte)0,
        };
        sections[slot] = section;

        Section from = continues ? sections[(int)(previous % Capacity)] : section;
        float now = Time.timeSinceLevelLoad;
        int v = slot * 4;
        vertices[v]     = Vert(from.left,     from.alpha,    0f, from.distance,    now);
        vertices[v + 1] = Vert(from.right,    from.alpha,    1f, from.distance,    now);
        vertices[v + 2] = Vert(section.left,  section.alpha, 0f, section.distance, now);
        vertices[v + 3] = Vert(section.right, section.alpha, 1f, section.distance, now);

        if (v < dirtyFrom) dirtyFrom = v;
        if (v + 3 > dirtyTo) dirtyTo = v + 3;
        return serial;
    }

    static Vertex Vert(Vector3 p, byte alpha, float across, float along, float time) => new Vertex
    {
        position = p,
        color = new Color32(255, 255, 255, alpha),
        uv = new Vector2(across, along),
        uv2 = new Vector2(time, 0f),
    };

    void LateUpdate()
    {
        if (dirtyTo < dirtyFrom) return;

        int count = dirtyTo - dirtyFrom + 1;
        mesh.SetVertexBufferData(vertices, dirtyFrom, dirtyFrom, count, 0,
            MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers);

        dirtyFrom = int.MaxValue;
        dirtyTo = -1;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
        if (vertices.IsCreated) vertices.Dispose();
        if (mesh != null) Destroy(mesh);
    }
}
