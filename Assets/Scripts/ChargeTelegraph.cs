using System.Collections.Generic;
using UnityEngine;

// Shared by Chargers and bosses. Every piece uses the existing square sprite.
public sealed class ChargeTelegraph
{
    // Warning visuals and line width
    private readonly SpriteRenderer lane;
    private readonly SpriteRenderer arrowLeft;
    private readonly SpriteRenderer arrowRight;
    private readonly float width;

    // Collision checks that limit the warning to the reachable charge distance
    private readonly Collider2D bodyCollider;
    private readonly ContactFilter2D wallFilter;
    private readonly List<RaycastHit2D> wallHits = new List<RaycastHit2D>(8);

    public ChargeTelegraph(SpriteRenderer warning, Collider2D collider, float lineWidth)
    {
        lane = warning;
        bodyCollider = collider;
        width = lineWidth;

        wallFilter = new ContactFilter2D { useTriggers = false };
        wallFilter.SetLayerMask(LayerMask.GetMask("Walls"));

        if (lane == null)
            return;

        arrowLeft = MakeArrow("Charge arrow left");
        arrowRight = MakeArrow("Charge arrow right");

        Hide();
    }

    private SpriteRenderer MakeArrow(string name)
    {
        var go = new GameObject(name);
        go.layer = lane.gameObject.layer;
        go.transform.SetParent(lane.transform.parent, false);

        var sprite = go.AddComponent<SpriteRenderer>();
        sprite.sprite = lane.sprite;
        sprite.sharedMaterial = lane.sharedMaterial;
        sprite.sortingLayerID = lane.sortingLayerID;
        sprite.sortingOrder = lane.sortingOrder + 1;

        return sprite;
    }

    public void Show(Vector2 direction, float chargeDistance)
    {
        if (lane == null || direction.sqrMagnitude < 0.001f)
        {
            Hide();
            return;
        }

        direction.Normalize();

        float length = ReachableDistance(direction, chargeDistance);

        if (length <= 0.01f)
        {
            Hide();
            return;
        }

        Vector2 origin = lane.transform.parent.position;
        Vector2 side = new Vector2(-direction.y, direction.x);
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        Draw(lane, origin + direction * (length * 0.5f), angle, new Vector2(length, width), new Color(1f, 0.12f, 0.1f, 0.45f));

        // Keep a readable arrowhead on the narrow line; its tip stays at the endpoint.
        float arrowWidth = Mathf.Min(Mathf.Max(width * 2.2f, 0.5f), length);
        float thickness = arrowWidth * 0.09f;
        float halfWing = arrowWidth * 0.2f;

        Vector2 tip = origin + direction * (length - thickness * 0.5f);
        Vector2 wingSize = new Vector2(halfWing * 2f * Mathf.Sqrt(2f), thickness);

        var red = new Color(1f, 0.12f, 0.1f, 0.95f);

        Draw(arrowLeft, tip - direction * halfWing + side * halfWing, angle - 45f, wingSize, red);
        Draw(arrowRight, tip - direction * halfWing - side * halfWing, angle + 45f, wingSize, red);
    }

    private float ReachableDistance(Vector2 direction, float distance)
    {
        if (bodyCollider == null)
            return distance;

        int count = bodyCollider.Cast(direction, wallFilter, wallHits, distance);

        for (int i = 0; i < count; i++)
        {
            var hit = wallHits[i];

            if (hit.distance <= 0.001f)
            {
                // Like the charge's wall check, ignore overlaps when moving away.
                Vector2 center = bodyCollider.bounds.center;
                Vector2 away = center - hit.collider.ClosestPoint(center);

                if (away.sqrMagnitude < 0.000001f)
                {
                    var separation = bodyCollider.Distance(hit.collider);

                    if (separation.isValid)
                        away = separation.normal * (separation.isOverlapped ? -1f : 1f);
                }

                if (away.sqrMagnitude > 0.000001f && Vector2.Dot(direction, away.normalized) >= -0.001f)
                    continue;
            }

            distance = Mathf.Min(distance, hit.distance);
        }

        return distance;
    }

    private static void Draw(SpriteRenderer sprite, Vector2 position, float angle, Vector2 size, Color color)
    {
        sprite.enabled = true;
        sprite.transform.position = position;
        sprite.transform.rotation = Quaternion.Euler(0, 0, angle);

        Vector3 parentScale = sprite.transform.parent.lossyScale;
        Vector3 spriteSize = sprite.sprite.bounds.size;

        sprite.transform.localScale = new Vector3(size.x / (spriteSize.x * Mathf.Abs(parentScale.x)), size.y / (spriteSize.y * Mathf.Abs(parentScale.y)), 1);
        sprite.color = color;
    }

    public void Hide()
    {
        if (lane != null)
            lane.enabled = false;

        if (arrowLeft != null)
            arrowLeft.enabled = false;

        if (arrowRight != null)
            arrowRight.enabled = false;
    }
}
