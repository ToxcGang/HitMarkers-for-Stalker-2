#pragma once

#include "CoreMinimal.h"
#include "Blueprint/UserWidget.h"
#include "HitMarkerTypes.h"
#include "HitMarkerWidget.generated.h"

UCLASS()
class HITMARKERS_API UHitMarkerWidget : public UUserWidget
{
    GENERATED_BODY()

public:
    UFUNCTION(BlueprintCallable, Category = "HitMarkers")
    void TriggerMarker(EHitMarkerKind Kind);

protected:
    virtual int32 NativePaint(
        const FPaintArgs& Args,
        const FGeometry& AllottedGeometry,
        const FSlateRect& MyCullingRect,
        FSlateWindowElementList& OutDrawElements,
        int32 LayerId,
        const FWidgetStyle& InWidgetStyle,
        bool bParentEnabled) const override;

    virtual void NativeTick(const FGeometry& MyGeometry, float InDeltaTime) override;

private:
    UPROPERTY(EditDefaultsOnly, Category = "HitMarkers")
    float MarkerDurationSeconds = 0.18f;

    UPROPERTY(EditDefaultsOnly, Category = "HitMarkers")
    float MarkerGap = 7.0f;

    UPROPERTY(EditDefaultsOnly, Category = "HitMarkers")
    float MarkerArmLength = 10.0f;

    UPROPERTY(EditDefaultsOnly, Category = "HitMarkers")
    float MarkerThickness = 2.0f;

    double LastTriggerTimeSeconds = -1.0;
    EHitMarkerKind LastMarkerKind = EHitMarkerKind::Hit;
};
