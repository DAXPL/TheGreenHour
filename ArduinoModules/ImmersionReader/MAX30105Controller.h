#ifndef MAX30105Controller_H
#define MAX30105Controller_H

#define bufferLength 50
#include <Wire.h>
#include "MAX30105.h"
#include "spo2_algorithm.h"

void setupController();
void loopController();
class MAX30105Controller 
{
  public:
    MAX30105Controller();
    void setupController();
    void loopController();
    int GetSPO2();
    int GetHR();
  private:
    MAX30105 particleSensor;
    
    uint32_t irBuffer[bufferLength]; //infrared LED sensor data
    uint32_t redBuffer[bufferLength];  //red LED sensor data
    
    int spo2Value = 0;
    int heartRateValue = 0;
};
#endif