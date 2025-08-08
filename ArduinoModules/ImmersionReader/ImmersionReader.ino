#include <WiFi.h>
#include <WebSocketsServer.h>
#include <OneWire.h>
#include <DallasTemperature.h>
#include "DFRobot_Heartrate.h"
#include <WiFiManager.h> 
#include <ESPmDNS.h>
#include "MAX30105Controller.h"

#define ONE_WIRE_BUS 32
#define GSR_PIN 35
#define MAX_BRIGHTNESS 255

WebSocketsServer webSocket(81);
OneWire oneWire(ONE_WIRE_BUS);
DallasTemperature sensors(&oneWire);
MAX30105Controller max30105;

void setup() 
{
  Serial.begin(9600);
  Serial.println("Initializing...");

  WiFiManager wm;
  if (!wm.autoConnect()) 
  {
    Serial.println("Cant connect to WiFi");
    ESP.restart();
  }
  Serial.println(WiFi.localIP().toString());

  if (!MDNS.begin("ImmersionReader")) 
  {
    Serial.println("Error setting up MDNS!");
  } 

  webSocket.begin();
  sensors.begin();
  max30105.setupController();
}

void loop() 
{
  webSocket.loop();
  sensors.requestTemperatures();
  max30105.loopController();

  String payload = String("{\"Temp\":") + sensors.getTempCByIndex(0)
                     + ",\"GSR\":" + analogRead(GSR_PIN)
                     + ",\"HR\":" + max30105.GetHR()
                     + ",\"SPO\":" + max30105.GetSPO2() +"}";
  Serial.println(payload);
  webSocket.broadcastTXT(payload);
}