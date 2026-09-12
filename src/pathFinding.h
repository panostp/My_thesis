#ifndef PATHFINDING_H
#define PATHFINDING_H

#include <godot_cpp/classes/node2d.hpp>
#include <godot_cpp/classes/engine.hpp>
#include <vector>
#define _USE_MATH_DEFINES
#include <math.h>
#include <godot_cpp/core/class_db.hpp>
#include <godot_cpp/godot.hpp>
#include <godot_cpp/classes/ref_counted.hpp>
#include <godot_cpp/variant/array.hpp>
#include <godot_cpp/variant/vector2i.hpp>
#include <ostream>
#include <godot_cpp/variant/utility_functions.hpp>
#include <cmath>

namespace godot 
{
    struct vectorf2{
        double x;
        double y;
        vectorf2(double px = 0.0, double py = 0.0) : x(px), y(py) {}
    };
    struct vectori2{
        int x;
        int y;
        vectori2(int px = 0, int py = 0) : x(px), y(py) {}
    };

    struct Map{
        Vector2i pos;
        int goal, cost, startHeading;
        int score() const { 
            return goal + cost; 
        }

        bool operator>(const Map &other) const {
            return score() > other.score();
        }
        Map(Vector2i p_pos, int p_goal, int p_cost, int p_startHeading) : pos(p_pos), goal(p_goal), cost(p_cost), startHeading(p_startHeading) {}
        Map(Vector2i p_pos, int p_goal, int p_cost) : pos(p_pos), goal(p_goal), cost(p_cost) {}
    };


    class PathFinding: public Node2D 
    {
        GDCLASS(PathFinding, Node2D);

        private:
            //void aStar(std::vector<vectorf2> &pathPoints, vectorf2 start, vectorf2 end);
            std::vector<int> visited;//maybe
        protected:
            static void _bind_methods();
        public:
            PathFinding();
            ~PathFinding();
            Array findPath(Vector2i start, Vector2i goal, Dictionary chunks,Dictionary penaltyChunks, int chunk_size, float startHeadingDeg);
            
    };
}


#endif