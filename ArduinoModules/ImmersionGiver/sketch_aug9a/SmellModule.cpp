#include "SmellModule.h"
SmellModule::SmellModule(int p)
{
  pin = p;
  pinMode(pin, OUTPUT);
  digitalWrite(pin, LOW);
}
void SmellModule::SmellLoop(unsigned long now)
{
  digitalWrite(pin, (now < endTime) ? HIGH : LOW);
}