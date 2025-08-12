#include "MAX30105Controller.h"

MAX30105Controller::MAX30105Controller()
{

}

void MAX30105Controller::setupController()
{
  if (!particleSensor.begin(Wire, I2C_SPEED_FAST)) //Use default I2C port, 400kHz speed
  {
    Serial.println(F("MAX30105 was not found."));
  }

  //ledBrightness Options: 0=Off to 255=50mA
  //sampleAverage Options: 1, 2, 4, 8, 16, 32
  //ledMode Options: 1 = Red only, 2 = Red + IR, 3 = Red + IR + Green
  //sampleRate Options: 50, 100, 200, 400, 800, 1000, 1600, 3200
  //pulseWidth Options: 69, 118, 215, 411
  //adcRange Options: 2048, 4096, 8192, 16384
  particleSensor.setup(80, 4, 2, bufferLength, 215, 4096);
}

void MAX30105Controller::loopController()
{
  if (particleSensor.available()) //do we have new data?
  {
    for (byte i=0; i<bufferLength-1;i++)
    {
      redBuffer[i] = redBuffer[i+1];
      irBuffer[i] = irBuffer[i+1];
    }

    redBuffer[bufferLength-1] = particleSensor.getRed();
    irBuffer[bufferLength-1] = particleSensor.getIR();

    int8_t validSPO2;
    int8_t validHeartRate;
    int32_t spo2;
    int32_t heartRate;
    maxim_heart_rate_and_oxygen_saturation(irBuffer, bufferLength, redBuffer, &spo2, &validSPO2, &heartRate, &validHeartRate);

    if(validHeartRate) heartRateValue = heartRate;
    if(validSPO2) spo2Value = spo2;
  }
  else
  {
    particleSensor.check(); //Check the sensor for new data
  }
}

int MAX30105Controller::GetSPO2(){return spo2Value;}
int MAX30105Controller::GetHR(){return heartRateValue;}