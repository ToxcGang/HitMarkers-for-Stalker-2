#include "HitMarkersSubsystem.h"

#include "Blueprint/UserWidget.h"
#include "Engine/World.h"
#include "GameFramework/Actor.h"
#include "GameFramework/PlayerController.h"
#include "HitMarkerWidget.h"

void UHitMarkersSubsystem::Initialize(FSubsystemCollectionBase& Collection)
{
    Super::Initialize(Collection);
    HitMarkerWidgetClass = UHitMarkerWidget::StaticClass();
}

void UHitMarkersSubsystem::Deinitialize()
{
    if (HitMarkerWidget.IsValid())
    {
        HitMarkerWidget->RemoveFromParent();
    }

    HitMarkerWidget.Reset();
    Super::Deinitialize();
}

void UHitMarkersSubsystem::NotifyPlayerEnemyHit(AActor* DamagedActor, AController* DamageInstigator, bool bKilled)
{
    if (!IsLocalPlayerInstigator(DamageInstigator) || !IsEligibleEnemyTarget(DamagedActor))
    {
        return;
    }

    EnsureWidget();

    if (HitMarkerWidget.IsValid())
    {
        HitMarkerWidget->TriggerMarker(bKilled ? EHitMarkerKind::Kill : EHitMarkerKind::Hit);
    }
}

void UHitMarkersSubsystem::EnsureWidget()
{
    if (HitMarkerWidget.IsValid())
    {
        return;
    }

    UWorld* World = GetWorld();
    if (!World)
    {
        return;
    }

    APlayerController* PlayerController = World->GetFirstPlayerController();
    if (!PlayerController)
    {
        return;
    }

    UHitMarkerWidget* Widget = CreateWidget<UHitMarkerWidget>(
        PlayerController,
        HitMarkerWidgetClass ? HitMarkerWidgetClass : UHitMarkerWidget::StaticClass());

    if (Widget)
    {
        Widget->AddToViewport(10000);
        HitMarkerWidget = Widget;
    }
}

bool UHitMarkersSubsystem::IsLocalPlayerInstigator(const AController* DamageInstigator) const
{
    return DamageInstigator && DamageInstigator->IsLocalController();
}

bool UHitMarkersSubsystem::IsEligibleEnemyTarget(const AActor* DamagedActor) const
{
    if (!DamagedActor)
    {
        return false;
    }

    return DamagedActor->ActorHasTag(TEXT("Enemy")) || DamagedActor->ActorHasTag(TEXT("Hostile"));
}
