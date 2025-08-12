#ifndef SmellModule_H
#define SmellModule_H

#include <Arduino.h>
class SmellModule 
{
  public:
  SmellModule(int p);
  int pin;
  unsigned long endTime;
  void SmellLoop(unsigned long now);
};
#endif