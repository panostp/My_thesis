using Godot;
using System;
using System.Collections.Generic;

public partial class Frontier : Node
{
    ChunckContainer chunkContainer;

    private List<Vector2I> FindFrontierCells()
    {
        var frontiers = new List<Vector2I>();
        Vector2[] neighborOffsets = {
            new Vector2(1,0), new Vector2(-1,0),
            new Vector2(0,1), new Vector2(0,-1)
        };

        foreach (var kvp in chunkContainer.Chunks)
        {
            Vector2I chunkPos = kvp.Key;
            int[,] chunk = kvp.Value;
            int cs = chunkContainer.ChunkSize;
            for (int x = 0; x < cs; x++)
            {
                for (int y = 0; y < cs; y++)
                {
                    if (chunk[x, y] != 1)
                        continue;

                    Vector2 worldPos = new Vector2(chunkPos.X * cs + x, chunkPos.Y * cs + y);
                    bool isFrontier = false;
                    foreach (var off in neighborOffsets)
                    {
                        if (GetCellWorld(worldPos + off) == 0)
                        {
                            isFrontier = true;
                            break;
                        }
                    }
                    if (isFrontier)
                        frontiers.Add(new Vector2I((int)worldPos.X, (int)worldPos.Y));
                }
            }
        }
        return frontiers;
    }

    private List<List<Vector2I>> ClusterFrontiers(List<Vector2I> frontiers)
    {
        var frontierSet = new HashSet<Vector2I>(frontiers);
        var visited = new HashSet<Vector2I>();
        var clusters = new List<List<Vector2I>>();
        Vector2I[] dirs8 = {
            new Vector2I(1,0), new Vector2I(-1,0),
            new Vector2I(0,1), new Vector2I(0,-1),
            new Vector2I(1,1), new Vector2I(1,-1),
            new Vector2I(-1,1), new Vector2I(-1,-1)
        };
        foreach (var f in frontiers)
        {
            if (visited.Contains(f)) continue;
            var cluster = new List<Vector2I>();
            var queue = new Queue<Vector2I>();
            queue.Enqueue(f);
            visited.Add(f);

            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                cluster.Add(cur);

                foreach (var d in dirs8)
                {
                    var next = cur + d;
                    if (frontierSet.Contains(next) && !visited.Contains(next))
                    {
                        visited.Add(next);
                        queue.Enqueue(next);
                    }
                }
            }
            clusters.Add(cluster);
        }
        return clusters;
    }

    private Vector2I? FindNearestPointInCluster(List<Vector2I> cluster, Vector2 target, int maxPenalty = 10)
    {
        Vector2I best = default;
        float bestDist = float.MaxValue;
        bool found = false;

        foreach (var p in cluster)
        {
            int penalty = chunkContainer.getPenelty(p)[
                chunkContainer.WorldToLocal(p).X,
                chunkContainer.WorldToLocal(p).Y
            ];

            if (penalty > maxPenalty)
                continue; // too close to an obstacle, skip this candidate cell

            float d = ((Vector2)p).DistanceSquaredTo(target);
            if (d < bestDist)
            {
                bestDist = d;
                best = p;
                found = true;
            }
        }

        return found ? best : (Vector2I?)null;
    }
    private float AngleDiffToTarget(Vector2 carPos, float carHeadingDeg, Vector2 target)
    {
        Vector2 toTarget = target - carPos;
        if (toTarget.LengthSquared() < 0.0001f)
            return 0f;

        float targetAngleDeg = Mathf.RadToDeg(Mathf.Atan2(toTarget.Y, toTarget.X));
        float diff = Mathf.Wrap(targetAngleDeg - carHeadingDeg, -180f, 180f);
        return Mathf.Abs(diff);
    }

    public Vector2? PickExplorationGoal(ChunckContainer chunk, Vector2 carPos, float carHeadingDeg,float preferredConeDeg = 45f, int maxPenalty = 10)
    {
        chunkContainer = chunk;
        var frontiers = FindFrontierCells();
        if (frontiers.Count == 0)
            return null;

        var clusters = ClusterFrontiers(frontiers);

        Vector2I bestPoint = default;
        float bestScore = float.NegativeInfinity;
        bool foundInCone = false;

        // Pass 1: only consider clusters within the preferred heading cone
        foreach (var cluster in clusters)
        {
            if (cluster.Count < 3)
                continue;

            Vector2 centroid = Vector2.Zero;
            foreach (var c in cluster) centroid += c;
            centroid /= cluster.Count;

            float angleDiff = AngleDiffToTarget(carPos, carHeadingDeg, centroid);
            if (angleDiff > preferredConeDeg)
                continue;

            Vector2I? candidate = FindNearestPointInCluster(cluster, centroid, maxPenalty);
            if (candidate == null)
                continue; // entire cluster was too close to obstacles, skip it

            float dist = ((Vector2)candidate.Value).DistanceTo(carPos);
            float score = cluster.Count - dist * 0.1f - angleDiff * 0.05f;

            if (score > bestScore)
            {
                bestScore = score;
                bestPoint = candidate.Value;
                foundInCone = true;
            }
        }

        // Pass 2: fallback — consider everything if nothing usable was in the cone
        if (!foundInCone)
        {
            foreach (var cluster in clusters)
            {
                if (cluster.Count < 3)
                    continue;

                Vector2 centroid = Vector2.Zero;
                foreach (var c in cluster) centroid += c;
                centroid /= cluster.Count;

                float angleDiff = AngleDiffToTarget(carPos, carHeadingDeg, centroid);

                Vector2I? candidate = FindNearestPointInCluster(cluster, centroid, maxPenalty);
                if (candidate == null)
                    continue;

                float dist = ((Vector2)candidate.Value).DistanceTo(carPos);
                float score = cluster.Count - dist * 0.1f - angleDiff * 0.1f;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestPoint = candidate.Value;
                }
            }
        }

        return bestScore == float.NegativeInfinity ? (Vector2?)null : (Vector2)bestPoint;
    }

    private int GetCellWorld(Vector2 worldPos)
    {
        return chunkContainer.GetCell(worldPos);
    }
}