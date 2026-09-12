#include "pathFinding.h"
#include <queue>
#include <unordered_map>
#include <unordered_set>
#include <algorithm>
 
using namespace godot;
 
namespace std {
    template<>
    struct hash<godot::Vector2i> {
        size_t operator()(const godot::Vector2i& v) const {
            return ((size_t)v.x << 32) ^ (size_t)v.y;
        }
    };
}
 
 
void PathFinding::_bind_methods() {
    ClassDB::bind_method(D_METHOD("findPath", "start", "goal", "chunks", "penaltyChunks", "chunkSize","startHeadingDeg"), &PathFinding::findPath);
}
PathFinding::PathFinding() {
    UtilityFunctions::print("PathFinding addon loaded fine.");
    if(Engine::get_singleton()->is_editor_hint())
    {
        UtilityFunctions::print("Running inside the editor");
        set_process_mode(Node::PROCESS_MODE_DISABLED);
    }
}
PathFinding::~PathFinding() {
}
struct StateKey {
    Vector2i pos;
    int heading; // 0-7, index into dirs[]
 
    bool operator==(const StateKey& o) const {
        return pos == o.pos && heading == o.heading;
    }
};
namespace std {
    template<>
    struct hash<StateKey> {
        size_t operator()(const StateKey& s) const {
            size_t h1 = ((size_t)s.pos.x << 32) ^ (size_t)s.pos.y;
            return h1 ^ (std::hash<int>()(s.heading) << 1);
        }
    };
}
int headingFromAngleDeg(float angleDeg) {
    float a = fmod(angleDeg, 360.0f);
    if (a < 0) a += 360.0f;
    int idx = (int)std::round(a / 45.0f) % 8;
    return idx;
}
Vector2i worldToChunk(Vector2i pos, int ChunkSize) {
    return Vector2i(floor((float)pos.x / ChunkSize),floor((float)pos.y / ChunkSize));
}
 
Vector2i worldToLocal(Vector2i pos, int ChunkSize) {
    int lx = pos.x % ChunkSize;
    int ly = pos.y % ChunkSize;
    if (lx < 0)
    {
        lx += ChunkSize;
    }
    if (ly < 0)
    {
        ly += ChunkSize;
    }
    return Vector2i(lx, ly);
}
int getCell(Dictionary &chunks, Vector2i world, int ChunkSize) {
    Vector2i chunk_pos = worldToChunk(world, ChunkSize);
    if (!chunks.has(chunk_pos))
        return 0; // unknown = free
 
    Array chunk_array = chunks[chunk_pos];
    Vector2i local = worldToLocal(world, ChunkSize);
    return (int)chunk_array[local.x * ChunkSize + local.y];
}
int clearancePenalty(Dictionary &chunks, Vector2i pos, int chunkSize) //heavy time complexity change to grid penalty later
{
    int penalty = 0;
    for (int dx = -4; dx <= 4; dx++)
    {
        for (int dy = -4; dy <= 4; dy++)
        {
            if((dx==-4 || dx==4 || dy==-4 || dy==4) && getCell(chunks, pos + Vector2i(dx, dy), chunkSize) == 2 )
                return 100;
            if (dx == 0 && dy == 0)
                continue;
            if (getCell(chunks, pos + Vector2i(dx, dy), chunkSize) == 2)
            {
                int d2 = dx*dx + dy*dy;
                penalty += (25 / (d2 + 1));
            }
        }
    }
    return penalty;
}
int getPenalty(Dictionary &penaltyChunks, Vector2i world, int ChunkSize) {
    Vector2i chunk_pos = worldToChunk(world, ChunkSize);
    if (!penaltyChunks.has(chunk_pos))
        return 0;
 
    Array chunk_array = penaltyChunks[chunk_pos];
    Vector2i local = worldToLocal(world, ChunkSize);
    return (int)chunk_array[local.x * ChunkSize + local.y];
}
bool hasMinimumClearance(Dictionary &chunks, Vector2i cellPos, Vector2i moveDir, int chunkSize, int minGapCells)
{
    Vector2i perp(-moveDir.y, moveDir.x);
 
    int halfGap = (minGapCells + 1) / 2; 
 
    int leftDist = -1;  
    int rightDist = -1; 
 
    for (int s = 1; s <= halfGap; s++)
    {
        if (leftDist == -1 && getCell(chunks, cellPos + perp * s, chunkSize) == 2)
            leftDist = s;
        if (rightDist == -1 && getCell(chunks, cellPos - perp * s, chunkSize) == 2)
            rightDist = s;
        if (leftDist != -1 && rightDist != -1)
            break; // found both sides, no need to search further
    }
 
    // only a problem if obstacles exist on BOTH sides — a wall on only one
    // side (open area on the other) isn't a "gap" at all
    if (leftDist != -1 && rightDist != -1)
    {
        int gapWidth = leftDist + rightDist;
        return gapWidth >= minGapCells;
    }
 
    return true; // no squeeze detected within range, allow it
}
Array PathFinding::findPath(Vector2i start, Vector2i goal, Dictionary chunks,Dictionary penaltyChunks, int chunkSize, float startHeadingDeg) {
 
    if (getCell(chunks, goal, chunkSize) == 2)
    {
        Array path;
        path.push_front(start);
        return path; // cant reach
    }
 
    std::priority_queue<Map, std::vector<Map>, std::greater<Map>> open;
    std::unordered_map<Vector2i, Vector2i> cameFrom;
    std::unordered_map<Vector2i, int> cost;
    std::unordered_set<Vector2i> closed;
    auto CalculateDistance = [](Vector2i p1, Vector2i p2)
    {
        int dx = std::abs(p2.x - p1.x);
        int dy = std::abs(p2.y - p1.y);
        int mx = std::max(dx, dy);
        int mn = std::min(dx, dy);
        return 10 * mx + 4 * mn;
    };
    int dis = CalculateDistance(start, goal);
    open.push(Map{start, 0, dis});
    cost[start] = 0;
 
    Vector2i dirs[8] = {
        Vector2i(1,0), Vector2i(1,1), Vector2i(0,1), Vector2i(-1,1),
        Vector2i(-1,0), Vector2i(-1,-1), Vector2i(0,-1), Vector2i(1,-1)
    };
    const int costLine = 10;
    const int costDiagonal = 14;   // ~10*sqrt(2)
    const int maxIterations=100000;
     int startHeading = headingFromAngleDeg(startHeadingDeg);
    int p=0;
    while (!open.empty())
    {
        if(p>maxIterations)
        {
            UtilityFunctions::print("No possible path maxed after 100000");
            Array path;
            path.push_front(start);
            return path; // cant reach
        }
        Map current = open.top();
        open.pop();
        if (closed.count(current.pos))
            continue;
        closed.insert(current.pos);
 
        if (current.pos == goal)
            break;
 
        int currentCost = cost[current.pos];
        bool axisBlocked[4];
        for (int a = 0; a < 4; a++)
            axisBlocked[a] = (getCell(chunks, current.pos + dirs[a * 2], chunkSize) == 2);
 
        for (int i = 0; i < 8; i++)
        //for(Vector2i &d : dirs)
        {
            Vector2i d = dirs[i];
            Vector2i next = current.pos + d;
 
            if (getCell(chunks, next, chunkSize) == 2)
                continue;
            if (d.x != 0 && d.y != 0)
            {
                if (axisBlocked[((i - 1 + 8) % 8) / 2] || axisBlocked[((i + 1) % 8) / 2])
                    continue;
            }
            const int minGapCells = 10;
           // if (!hasMinimumClearance(chunks, next, d, chunkSize, minGapCells))
            //    continue;
            int stepCost = (d.x != 0 && d.y != 0) ? costDiagonal : costLine;
            int penalty = getPenalty(penaltyChunks, next, chunkSize);
            if(penalty>10)
            {
                continue;
            }
            int new_cost = currentCost + stepCost + penalty;
 
            auto it = cost.find(next);
            if (it == cost.end() || new_cost < it->second)
            {
                if (it == cost.end())
                    cost.emplace(next, new_cost);
                else
                    it->second = new_cost;
                dis = CalculateDistance(next, goal);
                open.push(Map{next, new_cost, dis});
                cameFrom[next] = current.pos;
            }
        }
        p++;
    }
    Array path;
    Vector2i current = goal;
    while (current != start && cameFrom.count(current))
    {
        path.push_front(current);
        current = cameFrom[current];
    }
    path.push_front(start);
    return path;
}