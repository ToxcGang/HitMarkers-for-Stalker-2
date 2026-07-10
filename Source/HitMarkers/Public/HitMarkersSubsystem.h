#pragma once

#include "CoreMinimal.h"
#include "Subsystems/GameInstanceSubsystem.h"
#include "HitMarkerTypes.h"
#include "HitMarkersSubsystem.generated.h"

class UHitMarkerWidget;

UCLASS()
class HITMARKERS_API UHitMarkersSubsystem : public UGameInstanceSubsystem
{
    GENERATED_BODY()

public:
    virtual void Initialize(FSubsystemCollectionBase& Collection) override;
    virtual void Deinitialize() override;

    UFUNCTION(BlueprintCallable, Category = "HitMarkers")
    void NotifyPlayerEnemyHit(AActor* DamagedActor, AController* DamageInstigator, bool bKilled);

private:
    UPROPERTY()
    TWeakObjectPtr<UHitMarkerWidget> HitMarkerWidget;

    UPROPERTY()
    TSubclassOf<UHitMarkerWidget> HitMarkerWidgetClass;

    void EnsureWidget();
    bool IsLocalPlayerInstigator(const AController* DamageInstigator) const;
    bool IsEligibleEnemyTarget(const AActor* DamagedActor) const;
};
