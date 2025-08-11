#include <WiFi.h>
#include <WebSocketsServer.h>
#include <ESPmDNS.h>
#include <WiFiManager.h>
#include "SmellModule.h"
WebSocketsServer webSocket(81);

SmellModule smells[] = {
  SmellModule(32),
  SmellModule(25),
  SmellModule(27)
};


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

  if (!MDNS.begin("ImmersionGiver")) 
  {
    Serial.println("Error setting up MDNS!");
  } 
  webSocket.begin();
  webSocket.onEvent(onWsEvent);
}

void loop() 
{
  webSocket.loop();
  unsigned long now = millis();

  for (int i = 0; i < (sizeof(smells) / sizeof(smells[0])); i++) 
  {
    smells[i].SmellLoop(now);
  }
}

void onWsEvent(uint8_t num, WStype_t type, uint8_t * payload, size_t length) 
{
  if (type == WStype_TEXT) 
  {
    String data = String((char *)payload);
    Serial.println("Client send: " + data);

    int separatorIndex = data.indexOf(':');
    String smell = data;
    int duration = 0;

    if (separatorIndex != -1) 
    {
      smell = data.substring(0, separatorIndex);
      duration = data.substring(separatorIndex + 1).toInt();
    } 

    int smellIndex = -1;
    if (smell == "Forrest") smellIndex = 0;
    else if (smell == "Flowers") smellIndex = 1;
    else if (smell == "Other") smellIndex = 2;

    if (smellIndex < 0 || smellIndex >= (sizeof(smells) / sizeof(smells[0]))) return;
    smells[smellIndex].endTime= millis() + (duration * 1000UL);
  }
}