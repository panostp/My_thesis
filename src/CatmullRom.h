#ifndef CATMULLROM_H
#define CATMULLROM_H

#include <godot_cpp/classes/node2d.hpp>
#include <godot_cpp/classes/engine.hpp>
#include <vector>
#define _USE_MATH_DEFINES
#include <math.h>
#include <godot_cpp/core/class_db.hpp>
#include <godot_cpp/godot.hpp>
#include <ostream>
#include <godot_cpp/variant/utility_functions.hpp>
#include <cmath>





struct vector2f{
    double x;
    double y;
    vector2f(double px = 0.0, double py = 0.0) : x(px), y(py) {}
};
struct vector3f{
    double x;
    double y;
    double z;
    vector3f(double px = 0.0, double py = 0.0, double pz = 0.0) : x(px), y(py), z(pz) {}
};
struct splinePoint{
    vector2f p0;
    vector2f p1;
    vector2f p2;
    vector2f p3;
};
namespace godot 
{

    class CatmullRom: public Node2D 
    {
        GDCLASS(CatmullRom, Node2D);

        private:
            std::vector<vector2f> road; //avoid
            std::vector<Vector3> controlPoints; //scary
        protected:
            static void _bind_methods();
        public:
            CatmullRom();
            ~CatmullRom();
            //void hello(String word);
            //float distance();
            void _process(double delta);
            void setPoints(std::vector<vector2f>& pathPoints);
            void drawSpline(const splinePoint& pathPoint, int& density);
            void fixCurves();
            Array returnControlPoints();
            Array createSpline(Array Xcord, Array Ycord, bool fixCurvesBool);
            Array createSplineWithHeading(Array Xcord, Array Ycord, bool fixCurvesBool,float startHeadingDeg, float controlDistance);
            std::vector<vector2f> createSamplePoints();
    };
}
#endif // CATMULLROM_H