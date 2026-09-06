#include <WiFi.h>
#include <WebSocketsServer.h>
#include <Wire.h>
#include <VL53L1X.h>
#include <ArduinoJson.h>
#include <WiFiUdp.h>

//web
const char* ssid = "LAPTOPTEST";
const char* password = "12345678";
WebSocketsServer webSocket = WebSocketsServer(81);
unsigned long lastSend = 0;
const unsigned long sendInterval = 50;
bool discoveryEnabled = true;
//UDP search
WiFiUDP udp;
const int UDP_PORT = 4210;
//UDP search-
//web-

#define SDA 21
#define SCL 22
#define SERVO_PIN 27      
#define PAN_SERVO_PIN 14  

bool connected = false;

//ToF
const uint8_t XSHUT_A = 33;
const uint8_t XSHUT_B = 32;
const uint8_t ADDR_A = 0x2A;
const uint8_t ADDR_B = 0x2B;

VL53L1X sensorA;
VL53L1X sensorB; 

const int MOUNT_ANGLE_A = -30;
const int MAX_VALID_RANGE_MM = 4000;

int lastDistA = -1;
int lastDistB = -1; 
//ToF==

//====== WHEEL ODOMETRY ======
const int SLITS_PER_REV = 2;
const float WHEEL_CIRCUMFERENCE_CM = 21.3f;

const int SLIT_FAR_MM  = 65; 
const int SLIT_NEAR_MM = 60; 

bool inSlit = false;
volatile unsigned long slitCount = 0; 

void updateSlitCounter()
{
  if (!sensorB.dataReady())
    return;

  int dist = sensorB.read(false);
  if (sensorB.ranging_data.range_status != 0)
    return; 


  if (!inSlit && dist >= SLIT_FAR_MM)
  {
    inSlit = true;
    slitCount++;
  }
  else if (inSlit && dist <= SLIT_NEAR_MM)
  {
    inSlit = false; 
  }
}

float getDistanceTraveledCm()
{
  return (slitCount / (float)SLITS_PER_REV) * WHEEL_CIRCUMFERENCE_CM;
}
//====== WHEEL ODOMETRY ======

//====== SERVOS ======
int currentSteerAngle = 0;
int currentPanAngle = 0;
const int STEER_DUTY_MIN = 5100;
const int STEER_DUTY_CENTER = 6200;
const int STEER_DUTY_MAX = 7300;

const int PAN_CENTER = 90;
const int PAN_MIN = 0;
const int PAN_MAX = 180;
//=====================
int panDirection = 1;
unsigned long lastPanUpdate = 0;
const unsigned long PAN_UPDATE_INTERVAL_MS = 30;
const int PAN_STEP_DEG = 2;
//=====================

//====== MOTOR DRIVER ======
#define IN1 4
#define IN2 5
#define IN3 16
#define IN4 17
#define MOTOR_SPEED 200
//=========================================================================

//====== PATH QUEUE ======
struct PathPoint {
  float distance;
  float angle;
};
const int ObstacleStopDis = 200;
const int ObstacleLimit =15;
bool obstacleStopped = false;
const int MAX_PATH_POINTS = 64;
PathPoint pathQueue[MAX_PATH_POINTS];
int pathQueueCount = 0;
int pathQueueIndex = 0;

enum DriveState {IDLE,DRIVING};
DriveState driveState = IDLE;
unsigned long segmentStartTime = 0;
unsigned long segmentDurationMs = 0;
float segmentStartDistanceCm = 0; 
const float STEERING_SMOOTH_SPEED = 160.0f;
const float CM_PER_SECOND = 33.4f; 
//=========================

void initTofSensor(VL53L1X &sensor, uint8_t newAddress, unsigned long timingBudgetUs, unsigned long continuousPeriodMs, bool isB = false)
{
  sensor.setTimeout(500);
  if (!sensor.init())
  {
    Serial.print("Failed to init TOF sensor at address ");
    Serial.println(newAddress, HEX);
    while (1) delay(10);
  }
  sensor.setAddress(newAddress);
  sensor.setDistanceMode(VL53L1X::Short);
  if(isB)
    sensor.setROISize(4, 4);
  sensor.setMeasurementTimingBudget(timingBudgetUs);
  sensor.startContinuous(continuousPeriodMs);
}

void setupTofSensors()
{
  pinMode(XSHUT_A, OUTPUT);
  pinMode(XSHUT_B, OUTPUT);
  digitalWrite(XSHUT_A, LOW);
  digitalWrite(XSHUT_B, LOW);
  delay(10); 

  digitalWrite(XSHUT_A, HIGH);
  delay(10);
  initTofSensor(sensorA, ADDR_A, 33000, 33); 

  digitalWrite(XSHUT_B, HIGH); 
  delay(10);
  initTofSensor(sensorB, ADDR_B, 20000, 20, true); 

  Serial.println("Both TOF sensors initialized.");
}

int readValidDistance(VL53L1X &sensor, bool SensorB=false) {
  if (!sensor.dataReady())
    return -2;

  int dist = sensor.read(false);
  uint8_t status = sensor.ranging_data.range_status;
  if (status != 0) return -1;
  if (dist <= 0 || dist > MAX_VALID_RANGE_MM )
    return -1;

  return dist;
}
void writeServoAngle(int pin, int angle, int dutyMin, int dutyCenter, int dutyMax) {
  angle = constrain(angle, -45, 45);
  int duty;
  if (angle >= 0) {
    duty = map(angle, 0, 45, dutyCenter, dutyMax);
  } else {
    duty = map(angle, -45, 0, dutyMin, dutyCenter);
  }

  ledcWrite(pin, duty);
}

void servoWriteAngle(int angle) {
  currentSteerAngle = constrain(angle, -45, 45);
  int corrected = currentSteerAngle;
  corrected = constrain(corrected, -45, 45);
  writeServoAngle(SERVO_PIN, corrected, STEER_DUTY_MIN, STEER_DUTY_CENTER, STEER_DUTY_MAX);
}

void panServoWriteAngle(int relativeAngle)
{
    relativeAngle = constrain(relativeAngle, -90, 90);
    int physicalAngle = PAN_CENTER + relativeAngle;
    physicalAngle = constrain(physicalAngle, PAN_MIN, PAN_MAX);
    int pulseWidth = map(physicalAngle,0, 180,500, 2500);

    int duty = (pulseWidth * 65535L) / 20000L;
    ledcWrite(PAN_SERVO_PIN, duty);
    currentPanAngle = relativeAngle;
}

void updatePanSweep()
{
    unsigned long now = millis();

    if (now - lastPanUpdate < PAN_UPDATE_INTERVAL_MS)
        return;

    lastPanUpdate = now;

    int sweepLimit = (driveState == DRIVING) ? 15 : 45;

    int nextOffset = currentPanAngle + PAN_STEP_DEG * panDirection;

    if (nextOffset >= sweepLimit)
    {
        nextOffset = sweepLimit;
        panDirection = -1;
    }
    else if (nextOffset <= -sweepLimit)
    {
        nextOffset = -sweepLimit;
        panDirection = 1;
    }
    int physicalAngle = -(currentSteerAngle + nextOffset);
    physicalAngle = constrain(physicalAngle, -90, 90);
    int servoAngle = PAN_CENTER + physicalAngle;
    int pulseWidth = map(servoAngle, 0, 180, 500, 2500);
    int duty = (pulseWidth * 65535L) / 20000L;
    ledcWrite(PAN_SERVO_PIN, duty);
    currentPanAngle = nextOffset;
}

//====== MOTOR CONTROL ======
void motorStop() {
  digitalWrite(IN1, LOW);
  digitalWrite(IN2, LOW);
  digitalWrite(IN3, LOW);
  digitalWrite(IN4, LOW);
}

void motorForward(int speed) {
  digitalWrite(IN1, HIGH);
  digitalWrite(IN2, LOW);
  digitalWrite(IN3, HIGH);
  digitalWrite(IN4, LOW);
}
//============================
void sendSegmentComplete(int completedIndex, bool pathComplete)
{
  String json = "{";
  json += "\"type\":\"segment_complete\",";
  json += "\"index\":" + String(completedIndex) + ",";
  json += "\"pathComplete\":" + String(pathComplete ? "true" : "false");
  json += "}";
  webSocket.broadcastTXT(json);
}
void startNextPathPoint()
{
  if (pathQueueIndex >= pathQueueCount)
  {
    driveState = IDLE;
    motorStop();

    Serial.println("Path complete.");
    return;
  }
  PathPoint &pt = pathQueue[pathQueueIndex];
  Serial.printf(
    "Segment %d: distance=%.2f cm, target angle=%.2f\n",
    pathQueueIndex,
    pt.distance,
    pt.angle
  );
  segmentStartDistanceCm = getDistanceTraveledCm();
  segmentDurationMs =
    (unsigned long)((pt.distance / CM_PER_SECOND) * 1000.0f); 
  segmentStartTime = millis();
  driveState = DRIVING;

  motorForward(MOTOR_SPEED);
}

void updateDriving()
{
  if (driveState != DRIVING)
    return;

  bool sensorLookingForward =abs(currentPanAngle) <= ObstacleLimit;
  if (sensorLookingForward && lastDistA > 0 && lastDistA <= ObstacleStopDis)
  {
    emergencyStop();
    return;
  }
  if (pathQueueIndex >= pathQueueCount)
  {
    driveState = IDLE;
    motorStop();
    return;
  }
  PathPoint &pt = pathQueue[pathQueueIndex];
  updateSteering(pt.angle);

  float traveledThisSegment = getDistanceTraveledCm() - segmentStartDistanceCm;
  bool distanceReached = traveledThisSegment >= pt.distance;
  bool stalled = (millis() - segmentStartTime) >= (segmentDurationMs * 3);

  if (distanceReached || stalled)
  {
    if (stalled && !distanceReached)
      Serial.println("Segment timed out ");

    int completedIndex = pathQueueIndex;
    pathQueueIndex++;
    bool pathComplete = (pathQueueIndex >= pathQueueCount);
    sendSegmentComplete(completedIndex, pathComplete);

    if (pathComplete)
    {
      driveState = IDLE;
      motorStop();
      Serial.println("Path compl");
      return;
    }
    startNextPathPoint();
  }
}
void emergencyStop()
{
  motorStop();
  driveState = IDLE;
  obstacleStopped = true;
  Serial.println("OBSTACLE! Car stopped");
  webSocket.broadcastTXT("{\"type\":\"emergency_stop\"}");
}
unsigned long lastSteeringUpdate = 0;

void updateSteering(float targetAngle)
{
  unsigned long now = millis();
  if (lastSteeringUpdate == 0)
  {
    lastSteeringUpdate = now;
    return;
  }
  float dt = (now - lastSteeringUpdate) / 1000.0f;
  lastSteeringUpdate = now;
  targetAngle = constrain(targetAngle, -40.0f, 40.0f);
  float difference = targetAngle - currentSteerAngle;
  float maxStep = STEERING_SMOOTH_SPEED * dt;
  if (abs(difference) <= maxStep)
  {
    servoWriteAngle((int)targetAngle);
  }
  else if (difference > 0)
  {
    servoWriteAngle(currentSteerAngle + (int)maxStep);
  }
  else
  {
    servoWriteAngle(currentSteerAngle - (int)maxStep);
  }
}
void ConnectUDP(){
  int packetSize = udp.parsePacket();

  if (packetSize)
  {
    char packet[256];
    int len = udp.read(packet, sizeof(packet) - 1);

    if (len > 0)
        packet[len] = '\0';

    if (strcmp(packet, "DISCOVER") == 0)
    {
      String response ="ESP32_CAR|" +WiFi.localIP().toString();
      udp.beginPacket(udp.remoteIP(),udp.remotePort());
      udp.print(response);
      udp.endPacket();
      Serial.println("Discovery response");

    }
  }
}
void handlePathMessage(JsonDocument &doc) {
  JsonArray points = doc["points"].as<JsonArray>();
  motorStop();
  driveState = IDLE;
  obstacleStopped = false;
  pathQueueCount = 0;
  for (JsonVariant p : points)
  {
    if (pathQueueCount >= MAX_PATH_POINTS)
    {
      Serial.println("Path too long ");
      break;
    }
    JsonArray pair = p.as<JsonArray>();
    if (pair.size() < 2) continue;

    pathQueue[pathQueueCount].distance = pair[0].as<float>();
    pathQueue[pathQueueCount].angle = pair[1].as<float>();
    pathQueueCount++;
  }
  pathQueueIndex = 0;

  if (pathQueueCount > 0)
  {
    startNextPathPoint(); 
  }
}

void Movement(uint8_t * payload, size_t length) {
  JsonDocument doc;
  DeserializationError err = deserializeJson(doc, payload, length);
  if (err) {
    Serial.print("JSON parse failed: ");
    Serial.println(err.c_str());
    return;
  }

  const char* type = doc["type"];
  if (type == nullptr) {
    return;
  }

  if (strcmp(type, "path") == 0) {
    handlePathMessage(doc);
  }
  else {
    Serial.printf("Unknown message type: %s\n", type);
  }
}

void webSocketEvent(uint8_t num, WStype_t type, uint8_t * payload, size_t length)
{
  switch (type)
   {
    case WStype_DISCONNECTED:
      Serial.printf("[%u] Disconnected\n", num);
      discoveryEnabled=true;
      break;
    case WStype_CONNECTED:
    {
      IPAddress ip = webSocket.remoteIP(num);
      Serial.printf("[%u] Connected from %d.%d.%d.%d\n", num, ip[0], ip[1], ip[2], ip[3]);
      connected = true;
      discoveryEnabled=false;
      break;
    }
    case WStype_TEXT:
      Serial.printf("[%u] Received: %s\n", num, payload);
      Movement(payload, length);
      break;
    default:
      break;
  }
}

void sendSensorData() {
  int totalAngleA =currentSteerAngle + currentPanAngle;

  String json = "{";
  json += "\"type\":\"sensor_data\",";
  json += "\"angleA\":" + String(totalAngleA) + ",";
  json += "\"distanceA\":" + String(lastDistA);
  json += "}";

  webSocket.broadcastTXT(json);
}

void setup()
{
  Serial.begin(115200);
  //motor driver
  pinMode(IN1, OUTPUT);
  pinMode(IN2, OUTPUT);
  pinMode(IN3, OUTPUT);
  pinMode(IN4, OUTPUT);
  motorStop();
  //websocket
  WiFi.begin(ssid, password);
  while (WiFi.status() != WL_CONNECTED)
  {
    delay(500);
    Serial.print("-");
  }
  Serial.println();
  Serial.print("connected with IP : \n");
  Serial.println(WiFi.localIP());
  webSocket.begin();
  webSocket.onEvent(webSocketEvent);

  //servos
  ledcAttach(SERVO_PIN, 50, 16);
  ledcAttach(PAN_SERVO_PIN, 50, 16);
  servoWriteAngle(0);
  panServoWriteAngle(0);



  connected = true;

  //ToF
  Wire.begin(SDA, SCL);
  setupTofSensors();

  udp.begin(UDP_PORT);
  Serial.println("UDP discovery started");
}

void loop()
{
  webSocket.loop();

  if(discoveryEnabled)
    ConnectUDP();

  updateDriving();
  updatePanSweep();

  int distA = readValidDistance(sensorA);
  if (distA != -2)
    lastDistA = distA;

  updateSlitCounter();

  unsigned long now = millis();
  if (now - lastSend >= sendInterval)
  {
    lastSend = now;
    sendSensorData();
  }
}
