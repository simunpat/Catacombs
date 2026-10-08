using System;
using System.Collections.Generic;

// An encounter belongs to a run/floor, not to a room layout.
public sealed class FloorEncounter
{
    public int Seed { get; }
    public IReadOnlyList<EnemyKind> Enemies { get; }

    public FloorEncounter(int floor, int seed, int roomsCleared = 0)
    {
        if (floor < 1)
            throw new ArgumentOutOfRangeException(nameof(floor));

        Seed = seed;
        Random random = new Random(seed);

        // Keep these rolls in order so the same seed produces the same encounter.
        int enemyCount = ChooseEnemyCount(floor, random);
        int shooterCount = ChooseShooterCount(floor, roomsCleared, random);
        int chargerCount = ChooseChargerCount(floor, random);

        // Introduce the Charger without also increasing the size of the fight.
        if (floor == 3 && roomsCleared == 0)
        {
            enemyCount = 5;
            shooterCount = 1;
        }

        EnemyKind[] enemies = new EnemyKind[enemyCount];

        for (int i = 0; i < enemyCount; i++)
        {
            if (i < chargerCount)
                enemies[i] = EnemyKind.Charger;
            else if (i < chargerCount + shooterCount)
                enemies[i] = EnemyKind.Shooter;
            else
                enemies[i] = EnemyKind.Crawler;
        }

        // Shuffle the types so Chargers do not always use the first spawn points.
        for (int i = enemies.Length - 1; i > 0; i--)
        {
            int other = random.Next(i + 1);
            EnemyKind temporary = enemies[i];

            enemies[i] = enemies[other];
            enemies[other] = temporary;
        }

        Enemies = Array.AsReadOnly(enemies);
    }

    private static int ChooseEnemyCount(int floor, Random random)
    {
        if (floor == 1)
            return 3 + random.Next(2);

        if (floor <= 3)
            return 5 + random.Next(3);

        if (floor == 4)
            return 8 + random.Next(3);

        return 11 + random.Next(4);
    }

    private static int ChooseShooterCount(int floor, int roomsCleared, Random random)
    {
        // The first two rooms introduce movement and Crawlers before any Shooters.
        if (floor == 1)
            return roomsCleared < 2 ? 0 : 1;

        if (floor <= 3)
            return 1 + random.Next(2);

        if (floor == 4)
            return 2 + random.Next(2);

        return 3 + random.Next(2);
    }

    private static int ChooseChargerCount(int floor, Random random)
    {
        if (floor < 3)
            return 0;

        if (floor == 3)
            return 1;

        if (floor == 4)
            return 2 + random.Next(2);

        return 3 + random.Next(2);
    }
}
