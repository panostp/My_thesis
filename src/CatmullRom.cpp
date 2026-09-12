#include "CatmullRom.h"


using namespace godot;

//=========================================================

splinePoint createSplinePoint(const vector2f& p0, const vector2f& p1, const vector2f& p2, const vector2f& p3){
    splinePoint spl;
    spl.p0 = p0;
    spl.p1 = p1;
    spl.p2 = p2;
    spl.p3 = p3;
    return spl;
}

double distance(const vector2f& a, const vector2f& b) {
    return std::sqrt(std::pow(b.x - a.x, 2) + std::pow(b.y - a.y, 2));
}
double lerp(double a, double b, double t) {
    return a + t * (b - a);
}
double findAngle(const vector2f& from, const vector2f& to) {
    double magn1 = std::sqrt(from.x * from.x + from.y * from.y);
    double magn2 = std::sqrt(to.x * to.x + to.y * to.y);
    const double EPS = 1e-9;
    if (magn1 < EPS || magn2 < EPS)
        return 0.0;
    double dot = from.x * to.x + from.y * to.y;
    // double angle = std::acos(dot / (magn1 * magn2));
    // const double theta = angle * (180.0 / M_PI);
    // return theta;
    double cosAngle = dot / (magn1 * magn2);
    cosAngle = std::max(-1.0, std::min(1.0, cosAngle));
    double angle = std::acos(cosAngle);
    return angle * (180.0 / M_PI);
}
vector2f createOptimalVectorClock(const vector2f& vector){
    vector2f vector2;
    vector2.x = (std::sqrt(2)/2) * vector.x - (std::sqrt(2)/2) * vector.y;
    vector2.y = (std::sqrt(2)/2) * vector.x + (std::sqrt(2)/2) * vector.y;
    return vector2;
}
vector2f createOptimalVectorCounterClock(const vector2f& vector){
    vector2f vector2;
    vector2.x = (std::sqrt(2)/2) * vector.x + (std::sqrt(2)/2) * vector.y;
    vector2.y = -(std::sqrt(2)/2) * vector.x + (std::sqrt(2)/2) * vector.y;
    return vector2;
}
bool checkClockwise(const vector2f& from, const vector2f& to) {
    double cross = from.x * to.y - from.y * to.x;
    return cross < 0;
}
inline float getT(float t, const vector2f& p0, const vector2f& p1, float alpha) {
    float a = std::pow(distance(p0, p1), alpha);
    return a + t;
}

vector2f calculateSplinePoint(const splinePoint& spl, float t , float alpha=.5f) //returns point on curve
{
    float t0 = 0.0f;
    float t1 = getT( t0, spl.p0, spl.p1 , alpha);
    float t2 = getT( t1, spl.p1, spl.p2 , alpha);
    float t3 = getT( t2, spl.p2, spl.p3 , alpha);
    t = lerp( t1, t2, t );
    vector2f A1 = 
       {( t1-t )/( t1-t0 )*spl.p0.x + ( t-t0 )/( t1-t0 )*spl.p1.x ,
        ( t1-t )/( t1-t0 )*spl.p0.y + ( t-t0 )/( t1-t0 )*spl.p1.y };

    vector2f A2 = 
       {( t2-t )/( t2-t1 )*spl.p1.x + ( t-t1 )/( t2-t1 )*spl.p2.x ,
        ( t2-t )/( t2-t1 )*spl.p1.y + ( t-t1 )/( t2-t1 )*spl.p2.y };

    vector2f A3 = 
       {( t3-t )/( t3-t2 )*spl.p2.x + ( t-t2 )/( t3-t2 )*spl.p3.x ,
        ( t3-t )/( t3-t2 )*spl.p2.y + ( t-t2 )/( t3-t2 )*spl.p3.y };
        //=========================================================
    vector2f B1 = 
       {( t2-t )/( t2-t0 )*A1.x + ( t-t0 )/( t2-t0 )*A2.x ,
        ( t2-t )/( t2-t0 )*A1.y + ( t-t0 )/( t2-t0 )*A2.y };

    vector2f B2 = 
       {( t3-t )/( t3-t1 )*A2.x + ( t-t1 )/( t3-t1 )*A3.x ,
        ( t3-t )/( t3-t1 )*A2.y + ( t-t1 )/( t3-t1 )*A3.y };

    vector2f C = 
       {( t2-t )/( t2-t1 )*B1.x + ( t-t1 )/( t2-t1 )*B2.x ,
        ( t2-t )/( t2-t1 )*B1.y + ( t-t1 )/( t2-t1 )*B2.y };
    // vector2f A2 = ( t2-t )/( t2-t1 )*p1 + ( t-t1 )/( t2-t1 )*p2;
    // vector2f A3 = ( t3-t )/( t3-t2 )*p2 + ( t-t2 )/( t3-t2 )*p3;
    // vector2f B1 = ( t2-t )/( t2-t0 )*A1 + ( t-t0 )/( t2-t0 )*A2;
    // vector2f B2 = ( t3-t )/( t3-t1 )*A2 + ( t-t1 )/( t3-t1 )*A3;
    // vector2f C  = ( t2-t )/( t2-t1 )*B1 + ( t-t1 )/( t2-t1 )*B2;
    return C;
}
inline vector2f getEndPoint(vector2f& start, vector2f& end)
{
    vector2f ending={end.x + start.x, end.y + start.y};
    return ending;
}

//===========================================================




void CatmullRom::_bind_methods() 
{
    //ClassDB::bind_method(D_METHOD("hello", "word"), &CatmullRom::hello);
    //ClassDB::bind_method(D_METHOD("_process", "delta"), &CatmullRom::_process);
    ClassDB::bind_method(D_METHOD("createSpline", "Xcord", "Ycord", "fixCurvesBool"), &CatmullRom::createSpline);
    ClassDB::bind_method(D_METHOD("createSplineWithHeading", "Xcord", "Ycord", "fixCurvesBool", "startHeadingDeg", "controlDistance"),&CatmullRom::createSplineWithHeading);
    ClassDB::bind_method(D_METHOD("returnControlPoints"), &CatmullRom::returnControlPoints);
}
CatmullRom::CatmullRom() 
{
    UtilityFunctions::print("CatmullRom addon loaded fine.");
    if(Engine::get_singleton()->is_editor_hint()) 
    {
        UtilityFunctions::print("Running inside the editor");
        set_process_mode(Node::PROCESS_MODE_DISABLED);
    } 
}
CatmullRom::~CatmullRom() 
{
    
}

void CatmullRom::_process(double delta) 
{
    // You can add code here that needs to be executed every frame
} 
void CatmullRom::setPoints(std::vector<vector2f>& pathPoints)
{
    
    if(pathPoints.size() < 4)
    {
        UtilityFunctions::print("Not enough points to create a spline.");
        return;
    }
    int density; //number of points between each control point
    for(int i = 0; i < pathPoints.size() - 3; i++)
    {
        density = floor(distance(pathPoints[i+1], pathPoints[i+2])/2.0f);
        if(density < 1) 
            density = 1;
        splinePoint spl = createSplinePoint(pathPoints[i], pathPoints[i+1], pathPoints[i+2], pathPoints[i+3]);
        drawSpline(spl, density);
        
        
    }
    //splinePoint spl;
    
}
Array CatmullRom::returnControlPoints()
{
    Array result;
    result.resize(controlPoints.size());
    for (int i = 0; i < controlPoints.size(); i++) 
    {
        double x = controlPoints[i].x;
        double y = controlPoints[i].y;
        double z = std::round(controlPoints[i].z * 100.0) / 100.0;
        result[i] = Vector3(x, y, z);
    }
    controlPoints.clear(); // Clear the controlPoints vector after returning the result
    return result;
}
void CatmullRom::drawSpline(const splinePoint& pathPoint, int& density)
{
    auto tIncrement = 1.0f/density;
    auto t = tIncrement;
    vector2f pt;
    for(int i = 0; i < density; i++)
    {
        pt = calculateSplinePoint(pathPoint, t);
        road.push_back(pt);
        t += tIncrement;
    }
}
void addExtraPoints(std::vector<vector2f>& Tempvector){
    
    vector2f first = {Tempvector[1].x - Tempvector[0].x,
                        Tempvector[1].y - Tempvector[0].y};
    vector2f newFirst = {2*Tempvector[0].x -Tempvector[1].x, 2*Tempvector[0].y -Tempvector[1].y };
    int count = Tempvector.size();
    vector2f last = {Tempvector[count].x - Tempvector[count-1].x,
                        Tempvector[count].y - Tempvector[count-1].y};
    vector2f newlast =  {2*Tempvector[count].x -Tempvector[count-1].x, 2*Tempvector[count].y -Tempvector[count-1].y};
    UtilityFunctions::print(Tempvector[0].x);
    UtilityFunctions::print(Tempvector[0].y);
    // UtilityFunctions::print(newFirst.x);
    // UtilityFunctions::print(newFirst.y);

    
    Tempvector.insert(Tempvector.begin(),newFirst);
    UtilityFunctions::print("X : ");
    UtilityFunctions::print(Tempvector[0].x);
    UtilityFunctions::print("Y : ");
    UtilityFunctions::print(Tempvector[0].y);
    Tempvector.push_back(newlast);

}
std::vector<vector2f> CatmullRom::createSamplePoints()
{
    std::vector<vector2f> samplePathPoints;
    if (road.empty())
        return samplePathPoints;

    const double minSampleDistance = 20.0; 

    samplePathPoints.push_back(road[0]); 
    double accumulated = 0.0;
    for (size_t i = 1; i < road.size(); i++)
    {
        accumulated += distance(road[i - 1], road[i]);
        bool isLast = (i == road.size() - 1);
        if (accumulated >= minSampleDistance || isLast)
        {
            samplePathPoints.push_back(road[i]);
            accumulated = 0.0;
        }
    }
    return samplePathPoints;
}

void CatmullRom::fixCurves() //old
{
    std::vector<vector2f> samplePathPoints = createSamplePoints();
    controlPoints.clear();
    //int p=1;
    if (samplePathPoints.size() < 3)
    {
        UtilityFunctions::print("Not enough sample points to fix curves, skipping.");
        return; 
    }
    for(size_t i = 1; i + 1 < samplePathPoints.size(); i++)
    {
        vector2f from = {samplePathPoints[i].x - samplePathPoints[i-1].x,
                         samplePathPoints[i].y - samplePathPoints[i-1].y};
        vector2f to   = {samplePathPoints[i+1].x - samplePathPoints[i].x,
                         samplePathPoints[i+1].y - samplePathPoints[i].y};
        float angle = findAngle(from, to);
        if(angle > 45.0f)
        {
            bool clockwise = checkClockwise(from, to);
            vector2f optimalVector;
            if(!clockwise)
            {
                optimalVector = createOptimalVectorClock(from);
            }
            else
            {
                optimalVector = createOptimalVectorCounterClock(from);
            }
            samplePathPoints[i+1]=getEndPoint(samplePathPoints[i], optimalVector);
            // float magn = std::sqrt(optimalVector.x * optimalVector.x + optimalVector.y * optimalVector.y);
            // optimalVector.x /= magn;
            // optimalVector.y /= magn;
            // optimalVector.x *= 20.0f;
            // optimalVector.y *= 20.0f;
            // samplePathPoints[i].x += optimalVector.x;
            // samplePathPoints[i].y += optimalVector.y;
        }
        from = {samplePathPoints[i].x - samplePathPoints[i-1].x,
                         samplePathPoints[i].y - samplePathPoints[i-1].y};
        to   = {samplePathPoints[i+1].x - samplePathPoints[i].x,
                         samplePathPoints[i+1].y - samplePathPoints[i].y};
        angle = findAngle(from, to);
        bool clockwise2 = checkClockwise(from, to);
        if(!clockwise2)
        {
            angle = -angle;
        }
        controlPoints.push_back(Vector3((float)samplePathPoints[i].x, (float)samplePathPoints[i].y, angle));
        // float angle = findAngle(pathPoints[i+1], pathPoints[i+2]);
        // controlPoints.push_back(Vector3((float)pathPoints[i+1].x, (float)pathPoints[i+1].y, angle));
    }
    
    road.clear();
    setPoints(samplePathPoints);
}
Array CatmullRom::createSpline(Array Xcord, Array Ycord, bool fixCurvesBool)
{
    std::vector<vector2f> Tempvector;
    int count = MIN(Xcord.size(), Ycord.size());
    Vector2 firstPoint = {Xcord[0], Ycord[0]};
    Vector2 lastPoint = {Xcord[count-1], Ycord[count-1]};
    Tempvector.reserve(count);
    for (int i = 0; i < count; i++) 
    {
        double x = Xcord[i];
        double y = Ycord[i];
        Tempvector.emplace_back(x, y);
    }
    //addExtraPoints(Tempvector);
    setPoints(Tempvector);//gives the road vector
    Tempvector.clear();
    if(fixCurvesBool)
    {
        fixCurves();
    }
    int count2 = road.size();
    Array result;
    result.resize(count2);

    for (int i = 0; i < count2; i++) 
    {
        double x = road[i].x;
        double y = road[i].y;
        result[i] = Vector2(x, y);
    }
    //result.insert(0,firstPoint);
    result.append(lastPoint);
    road.clear();//dude they stay in memory otherwise
    return result;

}
Array CatmullRom::createSplineWithHeading(Array Xcord, Array Ycord, bool fixCurvesBool,float startHeadingDeg, float controlDistance)
{
    int count = MIN(Xcord.size(), Ycord.size());
    if (count < 1)
        return Array();

    Vector2 firstPoint = { (float)(double)Xcord[0], (float)(double)Ycord[0] };
    Vector2 lastPoint  = { (float)(double)Xcord[count - 1], (float)(double)Ycord[count - 1] };
    double headingRad = (double)startHeadingDeg * M_PI / 180.0;
    vector2f headingDir = { std::cos(headingRad), std::sin(headingRad) };
    vector2f carPoint = { (double)Xcord[0], (double)Ycord[0] };
    vector2f phantom = {
        carPoint.x - headingDir.x * controlDistance,
        carPoint.y - headingDir.y * controlDistance
    };
    vector2f phantom0 = {
        phantom.x - headingDir.x * controlDistance ,
        phantom.y - headingDir.y * controlDistance 
    };

    //test
    vector2f secondLastPoint = {
        (double)Xcord[count - 2],
        (double)Ycord[count - 2]
    };

    vector2f endPoint = {
        (double)Xcord[count - 1],
        (double)Ycord[count - 1]
    };
    vector2f endHeadingDir = {
        endPoint.x - secondLastPoint.x,
        endPoint.y - secondLastPoint.y
    };

    double endHeadingLength = std::sqrt(
        endHeadingDir.x * endHeadingDir.x +
        endHeadingDir.y * endHeadingDir.y
    );

    if (endHeadingLength > 0.000001)
    {
        endHeadingDir.x /= endHeadingLength;
        endHeadingDir.y /= endHeadingLength;
    }
    else
    {
        endHeadingDir = headingDir;
    }
    vector2f endPhantom = {
        endPoint.x + endHeadingDir.x * controlDistance,
        endPoint.y + endHeadingDir.y * controlDistance
    };

    vector2f endPhantom0 = {
        endPhantom.x + endHeadingDir.x * controlDistance,
        endPhantom.y + endHeadingDir.y * controlDistance
    };
    //test--

    std::vector<vector2f> Tempvector;
    Tempvector.reserve(count+2);
    Tempvector.push_back(phantom);


    for (int i = 0; i < count; i++)
    {
        double x = Xcord[i];
        double y = Ycord[i];
        Tempvector.emplace_back(x, y);
    }
    Tempvector.push_back(endPhantom0);
    if ((int)Tempvector.size() < 4)
    {
        Array result;
        result.append(firstPoint);
        result.append(lastPoint);
        return result;
    }

    setPoints(Tempvector); // fills road
    Tempvector.clear();
    // road.insert(road.begin(), phantom);
    // road.push_back(endPhantom0);
    //road.insert(road.begin(), phantom0);
    if (fixCurvesBool)
        fixCurves();

    int count2 = road.size();
    Array result;
    result.resize(count2);
    for (int i = 0; i < count2; i++)
        result[i] = Vector2(road[i].x, road[i].y);

    //result.insert(0, firstPoint); 
    //result.append(lastPoint);
    road.clear();
    return result;
}
