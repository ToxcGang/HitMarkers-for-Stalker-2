#pragma once

#include "Modules/ModuleManager.h"

class FHitMarkersModule : public IModuleInterface
{
public:
    virtual void StartupModule() override;
    virtual void ShutdownModule() override;
};
