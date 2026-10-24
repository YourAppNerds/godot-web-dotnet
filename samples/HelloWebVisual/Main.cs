using System.Runtime.InteropServices;
using Godot;

public partial class Main : Control
{
    private Label _output;
    private string _runtime;
    private int _frames;
    private double _elapsed;

    public override void _Ready()
    {
        _output = GetNode<Label>("Output");
        _runtime = $"{RuntimeInformation.FrameworkDescription} on {RuntimeInformation.OSDescription}";
        GD.Print("Hello from C# _Ready");
    }

    public override void _Process(double delta)
    {
        _frames++;
        _elapsed += delta;
        _output.Text = $"Hello from C#\n{_runtime}\nFrames: {_frames}\nTime: {_elapsed:F1} s";
    }
}
