#pragma once

#include "CoreMinimal.h"
#include "HitMarkerTypes.generated.h"

UENUM(BlueprintType)
enum class EHitMarkerKind : uint8
{
    Hit UMETA(DisplayName = "Hit"),
    Kill UMETA(DisplayName = "Kill")
};
