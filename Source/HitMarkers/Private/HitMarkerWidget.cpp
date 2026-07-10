#include "HitMarkerWidget.h"

#include "Rendering/DrawElements.h"

void UHitMarkerWidget::TriggerMarker(EHitMarkerKind Kind)
{
    LastMarkerKind = Kind;

    if (const UWorld* World = GetWorld())
    {
        LastTriggerTimeSeconds = World->GetRealTimeSeconds();
    }

    InvalidateLayoutAndVolatility();
}

int32 UHitMarkerWidget::NativePaint(
    const FPaintArgs& Args,
    const FGeometry& AllottedGeometry,
    const FSlateRect& MyCullingRect,
    FSlateWindowElementList& OutDrawElements,
    int32 LayerId,
    const FWidgetStyle& InWidgetStyle,
    bool bParentEnabled) const
{
    const int32 ResultLayer = Super::NativePaint(
        Args,
        AllottedGeometry,
        MyCullingRect,
        OutDrawElements,
        LayerId,
        InWidgetStyle,
        bParentEnabled);

    const UWorld* World = GetWorld();
    if (!World || LastTriggerTimeSeconds < 0.0)
    {
        return ResultLayer;
    }

    const double AgeSeconds = World->GetRealTimeSeconds() - LastTriggerTimeSeconds;
    if (AgeSeconds > MarkerDurationSeconds)
    {
        return ResultLayer;
    }

    const float Alpha = 1.0f - static_cast<float>(AgeSeconds / MarkerDurationSeconds);
    FLinearColor Color = LastMarkerKind == EHitMarkerKind::Kill
        ? FLinearColor(1.0f, 0.05f, 0.05f, Alpha)
        : FLinearColor(1.0f, 1.0f, 1.0f, Alpha);

    Color.A *= InWidgetStyle.GetColorAndOpacityTint().A;

    const FVector2D Center = AllottedGeometry.GetLocalSize() * 0.5f;
    const float Inner = MarkerGap;
    const float Outer = MarkerGap + MarkerArmLength;

    auto DrawLine = [&](const FVector2D& Start, const FVector2D& End)
    {
        TArray<FVector2D> Points;
        Points.Add(Start);
        Points.Add(End);

        FSlateDrawElement::MakeLines(
            OutDrawElements,
            ResultLayer + 1,
            AllottedGeometry.ToPaintGeometry(),
            Points,
            ESlateDrawEffect::None,
            Color,
            true,
            MarkerThickness);
    };

    DrawLine(Center + FVector2D(-Outer, -Outer), Center + FVector2D(-Inner, -Inner));
    DrawLine(Center + FVector2D(Outer, -Outer), Center + FVector2D(Inner, -Inner));
    DrawLine(Center + FVector2D(-Outer, Outer), Center + FVector2D(-Inner, Inner));
    DrawLine(Center + FVector2D(Outer, Outer), Center + FVector2D(Inner, Inner));

    return ResultLayer + 1;
}

void UHitMarkerWidget::NativeTick(const FGeometry& MyGeometry, float InDeltaTime)
{
    Super::NativeTick(MyGeometry, InDeltaTime);

    if (const UWorld* World = GetWorld())
    {
        const double AgeSeconds = World->GetRealTimeSeconds() - LastTriggerTimeSeconds;
        if (AgeSeconds <= MarkerDurationSeconds)
        {
            InvalidateLayoutAndVolatility();
        }
    }
}
