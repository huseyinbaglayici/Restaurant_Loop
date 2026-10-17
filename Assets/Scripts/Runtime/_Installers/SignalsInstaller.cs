using Zenject;
using RestaurantLoop.Runtime.Signals;

namespace RestaurantLoop.Runtime.Installers
{
    public class SignalsInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            SignalBusInstaller.Install(Container);

            Container.DeclareSignal<LevelEndedSignal>();
            Container.DeclareSignal<PlayRequestedSignal>();
            Container.DeclareSignal<RestartLevelSignal>();
            Container.DeclareSignal<LevelResumedSignal>().OptionalSubscriber();
            Container.DeclareSignal<LoadingStartedSignal>();
            Container.DeclareSignal<GameplaySceneReadySignal>();
            Container.DeclareSignal<MainMenuReadySignal>();
            Container.DeclareSignal<MenuReadySignal>();

            // conveyor feel layer (logical fires, ConveyorVisualService replays)
            Container.DeclareSignal<FoodQueuedForEntrySignal>().OptionalSubscriber();
            Container.DeclareSignal<FoodEnteredBeltSignal>().OptionalSubscriber();
            Container.DeclareSignal<FoodBeltEntryLandedSignal>().OptionalSubscriber();
            Container.DeclareSignal<FoodDockedSignal>().OptionalSubscriber();
            Container.DeclareSignal<FoodDockLandedSignal>().OptionalSubscriber();
            Container.DeclareSignal<FoodLoopedSignal>().OptionalSubscriber();
            Container.DeclareSignal<FoodConsumedSignal>().OptionalSubscriber();
            Container.DeclareSignal<FoodDeliveredToCustomerSignal>().OptionalSubscriber();
            Container.DeclareSignal<BeltClearedSignal>().OptionalSubscriber();
            Container.DeclareSignal<ExitStackOverflowSignal>().OptionalSubscriber();

            Container.DeclareSignal<TutorialStepActivatedSignal>().OptionalSubscriber();
            Container.DeclareSignal<TutorialEndedSignal>().OptionalSubscriber();
            Container.DeclareSignal<TutorialActionPerformedSignal>().OptionalSubscriber();
        }
    }
}
