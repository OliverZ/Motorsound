namespace ScooterSound;

public sealed class App : Application
{
    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new MainPage());
        window.Deactivated += (_, _) => RideSession.Throttle = 0;
        window.Stopped += (_, _) => RideSession.Throttle = 0;
        return window;
    }
}
