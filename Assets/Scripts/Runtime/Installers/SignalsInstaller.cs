using Zenject;
using RestaurantLoop.Signals;

namespace RestaurantLoop.Installers
{
    public class SignalsInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            SignalBusInstaller.Install(Container);

            Container.DeclareSignal<LevelEndedSignal>();
            Container.DeclareSignal<PlayRequestedSignal>();
            Container.DeclareSignal<RestartLevelSignal>();
            Container.DeclareSignal<LoadingStartedSignal>();
            Container.DeclareSignal<GameplaySceneReadySignal>();
            Container.DeclareSignal<MainMenuReadySignal>();
            Container.DeclareSignal<MenuReadySignal>();
            Container.DeclareSignal<SaveRequestedSignal>();
            Container.DeclareSignal<LoadRequestedSignal>();

            // conveyor feel layer (logical fires, ConveyorVisualService replays)
            Container.DeclareSignal<FoodQueuedForEntrySignal>().OptionalSubscriber();
            Container.DeclareSignal<FoodEnteredBeltSignal>().OptionalSubscriber();
            Container.DeclareSignal<FoodBeltEntryLandedSignal>().OptionalSubscriber();
            Container.DeclareSignal<FoodDockedSignal>().OptionalSubscriber();
            Container.DeclareSignal<FoodLoopedSignal>().OptionalSubscriber();
            Container.DeclareSignal<FoodConsumedSignal>().OptionalSubscriber();
            Container.DeclareSignal<FoodDeliveredToCustomerSignal>().OptionalSubscriber();
            Container.DeclareSignal<BeltClearedSignal>().OptionalSubscriber();

            Container.DeclareSignal<TutorialStepActivatedSignal>().OptionalSubscriber();
            Container.DeclareSignal<TutorialEndedSignal>().OptionalSubscriber();
            Container.DeclareSignal<TutorialActionPerformedSignal>().OptionalSubscriber();
        }
    }
}
