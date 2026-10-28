using ScooterSound.Core;
using Android.Content;

namespace ScooterSound;

public sealed class MainPage : ContentPage
{
    readonly Label speed = Text("0", 76, "#F4F8FC");
    readonly Label rpm = Text("LEERLAUF", 20, "#5EE6B0");
    readonly Label state = Text("Bereit", 16, "#B5C3D7");
    readonly Label signal = Text("Fahrt starten oder im Testmodus ausprobieren", 13, "#B5C3D7");
    readonly Label description = Text("", 14, "#B5C3D7");
    readonly Label testValue = Text("0 km/h", 16, "#5EE6B0");
    readonly Button start = new() { Text = "Fahrt starten", BackgroundColor = Color.FromArgb("#5EE6B0"), TextColor = Color.FromArgb("#101827"), HeightRequest = 58, CornerRadius = 16 };
    readonly Switch test = new();
    readonly Picker motors = new() { Title = "Motor wählen", TextColor = Colors.White, TitleColor = Colors.White };
    readonly Slider testSlider = new() { Minimum = 0, Maximum = 35, MinimumTrackColor = Color.FromArgb("#5EE6B0") };
    readonly IDispatcherTimer timer;
    bool busy;

    public MainPage()
    {
        Title = "Scooter Sound";
        BackgroundColor = Color.FromArgb("#101827");
        motors.ItemsSource = EngineProfile.All.Select(p => p.Name).ToList();
        motors.SelectedIndex = Math.Clamp(Preferences.Default.Get("motor", 0), 0, 4);
        SelectMotor();
        motors.SelectedIndexChanged += (_, _) => SelectMotor();
        test.Toggled += (_, e) => { testSlider.IsEnabled = e.Value; testSlider.Value = 0; RideSession.TestSpeed = 0; };
        testSlider.IsEnabled = false;
        testSlider.ValueChanged += (_, e) => { RideSession.TestSpeed = e.NewValue; testValue.Text = $"{e.NewValue:F0} km/h"; };
        var volume = new Slider { Minimum = 0, Maximum = 1, Value = RideSession.Volume, MinimumTrackColor = Color.FromArgb("#5EE6B0") };
        volume.ValueChanged += (_, e) => RideSession.Volume = e.NewValue;
        volume.DragCompleted += (_, _) => Preferences.Default.Set("volume", RideSession.Volume);
        start.Clicked += async (_, _) => await ToggleRide();
        var content = new VerticalStackLayout
        {
            Padding = new Thickness(24, 32, 24, 24), Spacing = 18,
            Children =
            {
                Text("SCOOTER / SOUND LAB", 12, "#5EE6B0"),
                Text("Deine Fahrt. Dein Motor.", 28, "#F4F8FC"),
                motors, description,
                new Border
                {
                    BackgroundColor = Color.FromArgb("#192539"), StrokeThickness = 0,
                    Padding = 24, StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 22 },
                    Content = new VerticalStackLayout { Spacing = 6, Children = { speed, Text("km/h", 16, "#B5C3D7"), rpm, state } }
                },
                signal, start,
                Text("LAUTSTÄRKE", 12, "#B5C3D7"), volume,
                new HorizontalStackLayout { Spacing = 16, Children = { Text("Testmodus", 18, "#F4F8FC"), test } },
                testValue, testSlider,
                Text("Synthetische Motorprofile · GPS bleibt auf deinem Gerät", 12, "#8293AB")
            }
        };
        Content = new ScrollView { Content = content };
        timer = Dispatcher.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(100);
        timer.Tick += (_, _) => Refresh();
    }

    static Label Text(string text, double size, string color) => new() { Text = text, FontSize = size, TextColor = Color.FromArgb(color) };
    void SelectMotor()
    {
        if (motors.SelectedIndex < 0) return;
        RideSession.Profile = EngineProfile.All[motors.SelectedIndex];
        description.Text = RideSession.Profile.Description;
        Preferences.Default.Set("motor", motors.SelectedIndex);
    }

    async Task ToggleRide()
    {
        if (busy) return;
        busy = true;
        start.IsEnabled = false;
        try
        {
            var context = global::Android.App.Application.Context;
            if (RideSession.Running)
                context.StopService(new Intent(context, typeof(RideService)));
            else
            {
                if (!test.IsToggled)
                {
                    var permission = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                    if (permission != PermissionStatus.Granted)
                    {
                        await DisplayAlertAsync("Standort benötigt", "Bitte Standortzugriff erlauben oder den Testmodus verwenden.", "OK");
                        return;
                    }
                    var manager = (global::Android.Locations.LocationManager?)context.GetSystemService(Context.LocationService);
                    if (manager?.IsProviderEnabled(global::Android.Locations.LocationManager.GpsProvider) != true)
                    {
                        await DisplayAlertAsync("GPS ausgeschaltet", "Bitte GPS in den Android-Einstellungen einschalten oder den Testmodus verwenden.", "OK");
                        return;
                    }
                }
                if (OperatingSystem.IsAndroidVersionAtLeast(33))
                    await Permissions.RequestAsync<Permissions.PostNotifications>();
                var intent = new Intent(context, typeof(RideService));
                intent.PutExtra("test", test.IsToggled);
                context.StartForegroundService(intent);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Fahrt konnte nicht gestartet werden", ex.Message, "OK");
        }
        finally { busy = false; start.IsEnabled = true; Refresh(); }
    }

    void Refresh()
    {
        bool active = RideSession.Running;
        var s = RideSession.Snapshot;
        speed.Text = active ? s.SpeedKmh.ToString("F0") : "0";
        rpm.Text = active ? $"{s.Rpm:F0} U/min{(s.Gear > 0 ? $" · Gang {s.Gear}" : "")}" : "LEERLAUF";
        state.Text = !active ? "Bereit" : s.State switch
        {
            DrivingState.Accelerating => "Beschleunigen", DrivingState.Decelerating => "Verzögern",
            DrivingState.Cruising => "Gleichmäßige Fahrt", _ => "Leerlauf"
        };
        signal.Text = RideSession.Status;
        start.Text = active ? "Fahrt stoppen" : "Fahrt starten";
        test.IsEnabled = !active;
    }
    protected override void OnAppearing() { base.OnAppearing(); timer.Start(); Refresh(); }
    protected override void OnDisappearing() { timer.Stop(); base.OnDisappearing(); }
}
