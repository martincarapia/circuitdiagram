namespace CircuitDiagram.UI.Hybrid;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new MainPage()) 
        { 
            Title = "Circuit Diagram",
            MinimumWidth = 1024,
            MinimumHeight = 768
        };
    }
}
