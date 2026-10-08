using System;
using System.Collections.Generic;
using UnityEngine;

// Places a floor's enemy plan at clear spawn markers in a handcrafted room.
public class RoomEncounter : MonoBehaviour
{
    // Enemy prefabs
    public EnemyHealth crawlerPrefab;
    public EnemyHealth shooterPrefab;
    public EnemyHealth chargerPrefab;

    // Spawn locations, parent and player clearance
    public Transform[] spawnPoints;
    public Transform enemyRoot;
    public float minimumPlayerDistance = 3f;

    public EnemyHealth[] Spawn(RoomController room)
    {
        FloorEncounter plan = RunState.EncounterForRoom(room.RoomId);

        if (plan == null)
            throw new InvalidOperationException("No encounter assigned to " + room.RoomId);

        var player = room.hud.player.GetComponent<PlayerHealth>();
        var positions = new List<Vector2>();

        Physics2D.SyncTransforms();

        int walls = LayerMask.GetMask("Walls");

        foreach (var point in spawnPoints)
            if (point != null && Vector2.Distance(point.position, player.transform.position) >= minimumPlayerDistance
                && Physics2D.OverlapCircle(point.position, 0.45f, walls) == null)
                positions.Add(point.position);

        var random = new System.Random(plan.Seed ^ 0x5a17);

        for (int i = positions.Count - 1; i > 0; i--)
        {
            int other = random.Next(i + 1);
            Vector2 temporary = positions[i];

            positions[i] = positions[other];
            positions[other] = temporary;
        }

        if (positions.Count < plan.Enemies.Count)
            throw new InvalidOperationException("Not enough clear enemy spawn points in " + room.RoomId);

        var enemies = new EnemyHealth[plan.Enemies.Count];

        for (int i = 0; i < enemies.Length; i++)
        {
            EnemyHealth prefab = plan.Enemies[i] == EnemyKind.Crawler ? crawlerPrefab
                : plan.Enemies[i] == EnemyKind.Shooter ? shooterPrefab : chargerPrefab;

            var enemy = Instantiate(prefab, positions[i], Quaternion.identity, enemyRoot);
            enemy.room = room;

            EnemyController controller = enemy.GetComponent<EnemyController>();
            controller.room = room;
            controller.player = player;

            enemies[i] = enemy;
        }

        return enemies;
    }
}
