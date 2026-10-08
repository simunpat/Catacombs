using UnityEngine;

// Narrow, independently clipped strips avoid bridging across a grave corner.
// Both damage and rendering use the same full-width strip casts, not centre rays.
public sealed class BossBeamSurface
{
    private const int Strips = 48;
    private const int GlowSegments = 12;
    private readonly Transform owner;
    private readonly Mesh beamMesh;
    private readonly Mesh glowMesh;
    private readonly MeshRenderer beamRenderer;
    private readonly MeshRenderer glowRenderer;
    private readonly Vector3[] vertices = new Vector3[Strips * 6];
    private readonly Color[] colors = new Color[Strips * 6];
    private readonly Vector3[] glowVertices = new Vector3[Strips * (GlowSegments + 2)];
    private readonly Color[] glowColors = new Color[Strips * (GlowSegments + 2)];
    private readonly float[] lengths = new float[Strips];
    private readonly RaycastHit2D[] hits = new RaycastHit2D[Strips];
    private Vector2 origin;
    private Vector2 direction;
    private Vector2 side;
    private float width;
    private float stripWidth;
    private float angle;

    public BossBeamSurface(Transform parent, Material material, int sortingLayer, int index)
    {
        owner = parent;
        beamMesh = MakeMesh("Beam surface " + index, material, sortingLayer, 8, out beamRenderer);
        glowMesh = MakeMesh("Beam surface glow " + index, material, sortingLayer, 10, out glowRenderer);

        var triangles = new int[Strips * 12];

        // Each strip has two quads: the solid beam and its short fading tip.
        for (int i = 0; i < Strips; i++)
        {
            int stripVertex = i * 6;
            int stripTriangle = i * 12;

            for (int segment = 0; segment < 2; segment++)
            {
                int vertexIndex = stripVertex + segment * 2;
                int triangleIndex = stripTriangle + segment * 6;

                triangles[triangleIndex] = vertexIndex;
                triangles[triangleIndex + 1] = vertexIndex + 2;
                triangles[triangleIndex + 2] = vertexIndex + 1;
                triangles[triangleIndex + 3] = vertexIndex + 1;
                triangles[triangleIndex + 4] = vertexIndex + 2;
                triangles[triangleIndex + 5] = vertexIndex + 3;
            }
        }

        beamMesh.vertices = vertices;
        beamMesh.triangles = triangles;

        var glowTriangles = new int[Strips * GlowSegments * 3];

        for (int i = 0; i < Strips; i++)
            for (int j = 0; j < GlowSegments; j++)
            {
                int triangleIndex = (i * GlowSegments + j) * 3;
                int centerVertex = i * (GlowSegments + 2);

                glowTriangles[triangleIndex] = centerVertex;
                glowTriangles[triangleIndex + 1] = centerVertex + j + 1;
                glowTriangles[triangleIndex + 2] = centerVertex + j + 2;
            }

        glowMesh.vertices = glowVertices;
        glowMesh.triangles = glowTriangles;
    }

    private Mesh MakeMesh(string name, Material material, int layer, int order, out MeshRenderer renderer)
    {
        var go = new GameObject(name);
        go.transform.SetParent(owner, false);

        var mesh = new Mesh { name = name };

        mesh.MarkDynamic();

        go.AddComponent<MeshFilter>().sharedMesh = mesh;

        renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.sortingLayerID = layer;
        renderer.sortingOrder = order;
        renderer.enabled = false;

        return mesh;
    }

    public void Sample(Vector2 beamDirection, float beamWidth, float reach, int walls)
    {
        origin = owner.position;
        direction = beamDirection.normalized;
        side = new Vector2(-direction.y, direction.x);

        width = Mathf.Max(.01f, beamWidth);
        stripWidth = width / Strips;

        angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        for (int i = 0; i < Strips; i++)
        {
            Vector2 start = origin + side * (-width * .5f + (i + .5f) * stripWidth);

            hits[i] = Physics2D.BoxCast(start, new Vector2(.02f, stripWidth), angle, direction, reach, walls);
            lengths[i] = hits[i].collider != null ? Mathf.Max(0, hits[i].distance) : reach;
        }
    }

    public bool HitsPlayer(PlayerHealth player, int playerLayer)
    {
        for (int i = 0; i < Strips; i++)
        {
            if (lengths[i] <= 0)
                continue;

            Vector2 center = origin + side * (-width * .5f + (i + .5f) * stripWidth) + direction * (lengths[i] * .5f);

            var hit = Physics2D.OverlapBox(center, new Vector2(lengths[i], stripWidth), angle, playerLayer);

            if (hit != null && hit.GetComponent<PlayerHealth>() == player)
                return true;
        }

        return false;
    }

    public void Draw(bool warning, float opacity, float time)
    {
        float flicker = .8f + .07f * Mathf.Sin(time * 43f);
        bool hasLength = false, hasGlow = false;

        for (int i = 0; i < Strips; i++)
        {
            float length = lengths[i];

            hasLength |= length > .01f;

            float softTip = Mathf.Min(length, warning ? .035f : .12f);

            for (int row = 0; row < 3; row++)
            {
                float distance = row == 0 ? 0 : row == 1 ? length - softTip : length;

                for (int edge = 0; edge < 2; edge++)
                {
                    int vertexIndex = i * 6 + row * 2 + edge;

                    // Identical arithmetic on shared edges prevents tiny raster seams.
                    float offset = -width * .5f + (i + edge) * stripWidth;

                    vertices[vertexIndex] = owner.InverseTransformPoint(origin + side * offset + direction * distance);

                    Color color = BeamColor(offset, warning, flicker);
                    color.a *= opacity * (row == 2 ? 0f : 1f);
                    colors[vertexIndex] = color;
                }
            }

            // A soft elliptical patch follows the actual surface normal at each
            // contact. Overlapping patches form a glow along the wall, not a cap.
            bool contact = !warning && hits[i].collider != null && length > .01f;

            hasGlow |= contact;

            int first = i * (GlowSegments + 2);

            Vector2 normal = contact ? hits[i].normal : -direction;
            Vector2 tangent = new Vector2(-normal.y, normal.x);
            Vector2 point = contact ? hits[i].point + normal * .02f : origin;

            float stretch = Mathf.Clamp(stripWidth / Mathf.Max(.12f, Mathf.Abs(Vector2.Dot(normal, direction))) + .065f, .09f, .25f);

            glowVertices[first] = owner.InverseTransformPoint(point);
            glowColors[first] = new Color(1f, .88f, .3f, contact ? opacity * (.38f + flicker * .2f) : 0f);

            for (int j = 0; j <= GlowSegments; j++)
            {
                float a = j * Mathf.PI * 2 / GlowSegments;

                Vector2 rim = point + tangent * (Mathf.Cos(a) * stretch) + normal * (Mathf.Sin(a) * .11f);

                glowVertices[first + j + 1] = owner.InverseTransformPoint(rim);
                glowColors[first + j + 1] = new Color(1f, .32f, .025f, 0);
            }
        }

        beamMesh.vertices = vertices;
        beamMesh.colors = colors;
        beamMesh.RecalculateBounds();

        glowMesh.vertices = glowVertices;
        glowMesh.colors = glowColors;
        glowMesh.RecalculateBounds();

        beamRenderer.enabled = hasLength && opacity > 0;
        glowRenderer.enabled = hasGlow && opacity > 0;
    }

    private Color BeamColor(float offset, bool warning, float flicker)
    {
        float distance = Mathf.Abs(offset);

        if (warning)
        {
            if (distance <= .03f)
                return new Color(1f, .9f, .4f, .6f);

            if (distance >= width * .5f - .03f)
                return new Color(1f, .62f, .12f, .35f);

            return new Color(1f, .36f, .035f, .08f);
        }

        float blend = Mathf.InverseLerp(width * .25f, width * .34f, distance);
        return Color.Lerp(new Color(1f, .9f, .4f, .95f), new Color(1f, .36f, .035f, flicker), blend);
    }

    public void Hide()
    {
        beamRenderer.enabled = glowRenderer.enabled = false;
    }

    public void Dispose()
    {
        Object.Destroy(beamMesh);
        Object.Destroy(glowMesh);
    }
}
